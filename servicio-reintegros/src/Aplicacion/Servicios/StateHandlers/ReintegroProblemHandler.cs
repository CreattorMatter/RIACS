using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ServicioReintegros.AssistCard.Aplicacion.Dtos;
using ServicioReintegros.AssistCard.Dominio.Entidades;

namespace ServicioReintegros.AssistCard.Aplicacion.Servicios.StateHandlers
{
    public sealed class ReintegroProblemHandler : IStateHandler
    {
        public bool CanHandle(EstadoConversacion estado) => estado == EstadoConversacion.ReintegroProblem;

        public async Task<StateResult> HandleAsync(StateHandlerContext ctx, CancellationToken ct)
        {
            // Volver/Cerrar
            var opcionesExit = BotMessages.OpcionesExit(ctx.Locale);
            var elegidoExit = OptionSelector.ElegirOpcion(ctx.TextoUsuario, opcionesExit);
            if (elegidoExit != null)
            {
                var idxExit = opcionesExit.FindIndex(o => string.Equals(o, elegidoExit, StringComparison.OrdinalIgnoreCase));
                if (idxExit == 1) // Cerrar
                {
                    ctx.Sesion.Estado = EstadoConversacion.Ended;
                    await ctx.GuardarSesion(ctx.Telefono, ctx.Sesion, ct);
                    return new StateResult { Mensaje = BotMessages.ConversacionFinalizada(ctx.Locale) };
                }
                // Volver → menú del reintegro
                ctx.Sesion.Estado = EstadoConversacion.ReintegroMenu;
                await ctx.GuardarSesion(ctx.Telefono, ctx.Sesion, ct);
                var reintegroVolver = await ctx.ObtenerReintegroActual(ctx.Telefono, ct);
                return new StateResult
                {
                    Mensaje = reintegroVolver == null
                        ? BotMessages.MensajeSinReintegro(ctx.Locale)
                        : BotMessages.ConstruirDetalleYMenuReintegro(reintegroVolver, ctx.Locale, ctx.Sesion.Nombre)
                };
            }

            var reintegro = await ctx.ObtenerReintegroActual(ctx.Telefono, ct);
            var email = LocaleHelper.EmailSoporte(ctx.Locale);
            var detalle = ctx.TextoUsuario;

            // Reclamo de demora
            if (CommandDetector.EsReclamoDemoraPago(detalle) && reintegro != null)
            {
                var dias = ReintegroStatusHelper.DiasDesdeCreacion(reintegro);
                var dentroPrimeros20 = dias.HasValue && dias.Value <= 20;

                var prompt = PromptTemplates.ReclamoDemoraPago(ctx.Locale, email);
                var contexto = new IaRequestContext
                {
                    Intent = "ReclamoDemoraPago",
                    Question = detalle,
                    Nombre = ctx.Sesion.Nombre,
                    Reintegro = reintegro,
                    DiasDesdeCarga = dias,
                    DentroPrimeros20Dias = dentroPrimeros20,
                    Email = email,
                    Locale = ctx.Locale
                };

                var respIa = await ctx.Ia.EnviarAsync(ctx.Telefono, ctx.Locale, prompt, contexto, ct);
                var fallback = BotMessages.FallbackDemora(ctx.Locale, dentroPrimeros20, dias, email);

                string respuesta;
                if (respIa?.Error == "FoundryKnowledgeIndexDuplicated" || string.IsNullOrWhiteSpace(respIa?.Message))
                {
                    ctx.Log.LogWarning("Fallback(DemoraPago/ReintegroProblem): Foundry falló.");
                    respuesta = fallback;
                }
                else
                {
                    var gr = GuardrailService.AplicarGuardrails(respIa.Message, ctx.Locale, fallback, GuardrailPolicy.NoAceleracion);
                    if (gr.UsedFallback)
                        ctx.Log.LogWarning("Guardrail(DemoraPago/ReintegroProblem): {Reason}", gr.Reason);
                    respuesta = gr.Mensaje;
                }

                ctx.Sesion.Estado = EstadoConversacion.ReintegroExit;
                ctx.Sesion.LastOptions = BotMessages.OpcionesExit(ctx.Locale);
                await ctx.GuardarSesion(ctx.Telefono, ctx.Sesion, ct);
                return new StateResult { Mensaje = respuesta + "\n\n" + BotMessages.ConstruirExit(ctx.Locale) };
            }

            // Consulta plazos de pago
            if (CommandDetector.EsConsultaPlazosPago(detalle) && ReintegroStatusHelper.EsEstadoPagoPendiente(reintegro))
            {
                var prompt = PromptTemplates.PlazosPago(ctx.Locale);
                var contexto = new IaRequestContext
                {
                    Intent = "PlazosPagoPendiente",
                    Question = detalle,
                    Nombre = ctx.Sesion.Nombre,
                    Reintegro = reintegro,
                    Locale = ctx.Locale
                };
                var respIa = await ctx.Ia.EnviarAsync(ctx.Telefono, ctx.Locale, prompt, contexto, ct);

                var respuesta = (respIa?.Error == "FoundryKnowledgeIndexDuplicated" || string.IsNullOrWhiteSpace(respIa?.Message))
                    ? BotMessages.FallbackPlazosPago(ctx.Locale)
                    : respIa!.Message;

                ctx.Sesion.Estado = EstadoConversacion.ReintegroExit;
                await ctx.GuardarSesion(ctx.Telefono, ctx.Sesion, ct);
                return new StateResult { Mensaje = respuesta + "\n\n" + BotMessages.ConstruirExit(ctx.Locale) };
            }

            // Consulta moneda de pago
            if (CommandDetector.EsConsultaMonedaPago(detalle) && ReintegroStatusHelper.EsEstadoPagoPendiente(reintegro))
            {
                var prompt = PromptTemplates.MonedaPago(ctx.Locale);
                var contexto = new IaRequestContext
                {
                    Intent = "MonedaPagoPendiente",
                    Question = detalle,
                    Nombre = ctx.Sesion.Nombre,
                    Reintegro = reintegro,
                    Locale = ctx.Locale
                };
                var respIa = await ctx.Ia.EnviarAsync(ctx.Telefono, ctx.Locale, prompt, contexto, ct);

                var respuesta = (respIa?.Error == "FoundryKnowledgeIndexDuplicated" || string.IsNullOrWhiteSpace(respIa?.Message))
                    ? BotMessages.FallbackMonedaPago(ctx.Locale)
                    : respIa!.Message;

                ctx.Sesion.Estado = EstadoConversacion.ReintegroExit;
                await ctx.GuardarSesion(ctx.Telefono, ctx.Sesion, ct);
                return new StateResult { Mensaje = respuesta + "\n\n" + BotMessages.ConstruirExit(ctx.Locale) };
            }

            // Consulta tipo de cambio
            if (CommandDetector.EsConsultaTipoCambio(detalle) && ReintegroStatusHelper.EsEstadoPagoPendiente(reintegro))
            {
                var prompt = PromptTemplates.TipoCambioReintegro(ctx.Locale);
                var contexto = new IaRequestContext
                {
                    Intent = "TipoCambioPagoPendiente",
                    Question = detalle,
                    Nombre = ctx.Sesion.Nombre,
                    Reintegro = reintegro,
                    Locale = ctx.Locale
                };
                var respIa = await ctx.Ia.EnviarAsync(ctx.Telefono, ctx.Locale, prompt, contexto, ct);

                var respuesta = (respIa?.Error == "FoundryKnowledgeIndexDuplicated" || string.IsNullOrWhiteSpace(respIa?.Message))
                    ? BotMessages.FallbackTipoCambio(ctx.Locale)
                    : respIa!.Message;

                ctx.Sesion.Estado = EstadoConversacion.ReintegroExit;
                await ctx.GuardarSesion(ctx.Telefono, ctx.Sesion, ct);
                return new StateResult { Mensaje = respuesta + "\n\n" + BotMessages.ConstruirExit(ctx.Locale) };
            }

            // Problemas con la App
            if (CommandDetector.EsConsultaProblemasApp(detalle))
            {
                var prompt = PromptTemplates.ProblemasApp(ctx.Locale, email);
                var contexto = new IaRequestContext
                {
                    Intent = "ProblemasAppMyAC",
                    Question = detalle,
                    Nombre = ctx.Sesion.Nombre,
                    Reintegro = reintegro,
                    Locale = ctx.Locale,
                    Problem = detalle
                };
                var respIa = await ctx.Ia.EnviarAsync(ctx.Telefono, ctx.Locale, prompt, contexto, ct);
                var fallback = BotMessages.FallbackProblemasApp(ctx.Locale, email);

                string respuesta;
                if (respIa?.Error == "FoundryKnowledgeIndexDuplicated" || string.IsNullOrWhiteSpace(respIa?.Message))
                {
                    ctx.Log.LogWarning("Fallback(ProblemasApp/ReintegroProblem): Foundry falló.");
                    respuesta = fallback;
                }
                else
                {
                    var gr = GuardrailService.AplicarGuardrails(respIa.Message, ctx.Locale, fallback);
                    if (gr.UsedFallback)
                        ctx.Log.LogWarning("Guardrail(ProblemasApp/ReintegroProblem): {Reason}", gr.Reason);
                    respuesta = gr.Mensaje;
                }

                ctx.Sesion.Estado = EstadoConversacion.ReintegroExit;
                await ctx.GuardarSesion(ctx.Telefono, ctx.Sesion, ct);
                return new StateResult { Mensaje = respuesta + "\n\n" + BotMessages.ConstruirExit(ctx.Locale) };
            }

            // Problema genérico → IA
            var promptGen = PromptTemplates.AnalizarProblemaGeneral(ctx.Locale);
            var contextoGen = new IaRequestContext
            {
                Intent = "ProblemaGeneral",
                Question = detalle,
                Reintegro = reintegro,
                Problem = detalle,
                Locale = ctx.Locale,
                Nombre = ctx.Sesion.Nombre
            };
            var respGen = await ctx.Ia.EnviarAsync(ctx.Telefono, ctx.Locale, promptGen, contextoGen, ct);
            var respuestaGen = respGen?.Message;
            if (string.IsNullOrWhiteSpace(respuestaGen))
            {
                ctx.Log.LogWarning("Fallback(ProblemaGeneral/ReintegroProblem): IA sin respuesta. error={Error}", respGen?.Error ?? "<empty>");
                respuestaGen = BotMessages.RespuestaIaVacia(ctx.Locale);
            }

            ctx.Sesion.Estado = EstadoConversacion.ReintegroExit;
            await ctx.GuardarSesion(ctx.Telefono, ctx.Sesion, ct);
            return new StateResult { Mensaje = respuestaGen + "\n\n" + BotMessages.ConstruirExit(ctx.Locale) };
        }
    }
}
