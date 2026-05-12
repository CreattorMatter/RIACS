using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ServicioReintegros.AssistCard.Aplicacion.Dtos;
using ServicioReintegros.AssistCard.Dominio.Entidades;

namespace ServicioReintegros.AssistCard.Aplicacion.Servicios.StateHandlers
{
    public sealed class OtrasConsultasProblemHandler : IStateHandler
    {
        public bool CanHandle(EstadoConversacion estado) => estado == EstadoConversacion.OtrasConsultasProblem;

        public async Task<StateResult> HandleAsync(StateHandlerContext ctx, CancellationToken ct)
        {
            // Primero: chequear si eligió Volver/Cerrar
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
                // Volver
                ctx.Sesion.Estado = EstadoConversacion.OtrasConsultasMenu;
                await ctx.GuardarSesion(ctx.Telefono, ctx.Sesion, ct);
                return new StateResult { Mensaje = BotMessages.MenuOtrasConsultasHeader(ctx.Locale, ctx.Sesion.Nombre) };
            }

            var email = LocaleHelper.EmailSoporte(ctx.Locale);

            // Guardrail: reclamo de demora
            if (CommandDetector.EsReclamoDemoraPago(ctx.TextoUsuario))
            {
                var reintegro = await ctx.ObtenerReintegroActual(ctx.Telefono, ct);
                var dias = ReintegroStatusHelper.DiasDesdeCreacion(reintegro);
                var dentroPrimeros20 = dias.HasValue && dias.Value <= 20;

                var contexto = new IaRequestContext
                {
                    Intent = "ReclamoDemoraPago",
                    Question = ctx.TextoUsuario,
                    Nombre = ctx.Sesion.Nombre,
                    Reintegro = reintegro,
                    DiasDesdeCarga = dias,
                    DentroPrimeros20Dias = dentroPrimeros20,
                    Email = email,
                    Locale = ctx.Locale
                };

                var prompt = PromptTemplates.ReclamoDemoraPago(ctx.Locale, email);
                var respIa = await ctx.Ia.EnviarAsync(ctx.Telefono, ctx.Locale, prompt, contexto, ct);
                var fallback = BotMessages.FallbackDemora(ctx.Locale, dentroPrimeros20, dias, email);

                string respuesta;
                if (respIa?.Error == "FoundryKnowledgeIndexDuplicated" || string.IsNullOrWhiteSpace(respIa?.Message))
                {
                    ctx.Log.LogWarning("Fallback(DemoraPago/OtrasConsultas): Foundry falló.");
                    respuesta = fallback;
                }
                else
                {
                    var gr = GuardrailService.AplicarGuardrails(respIa.Message, ctx.Locale, fallback, GuardrailPolicy.NoAceleracion);
                    if (gr.UsedFallback)
                        ctx.Log.LogWarning("Guardrail(DemoraPago/OtrasConsultas): {Reason}", gr.Reason);
                    respuesta = gr.Mensaje;
                }

                ctx.Sesion.Estado = EstadoConversacion.ReintegroExit;
                ctx.Sesion.LastOptions = BotMessages.OpcionesExit(ctx.Locale);
                await ctx.GuardarSesion(ctx.Telefono, ctx.Sesion, ct);
                return new StateResult { Mensaje = respuesta + "\n\n" + BotMessages.ConstruirExit(ctx.Locale) };
            }

            // Problema genérico → IA
            var ctxGeneral = new IaRequestContext
            {
                Intent = "ProblemaGeneral",
                Question = ctx.TextoUsuario,
                Nombre = ctx.Sesion.Nombre,
                Email = email,
                Locale = ctx.Locale,
                Problem = ctx.TextoUsuario
            };

            var promptGen = PromptTemplates.AnalizarProblemaGeneral(ctx.Locale);
            var respGen = await ctx.Ia.EnviarAsync(ctx.Telefono, ctx.Locale, promptGen, ctxGeneral, ct);

            string msgGen;
            if (string.IsNullOrWhiteSpace(respGen?.Message))
            {
                ctx.Log.LogWarning("Fallback(ProblemaGeneral): IA sin respuesta.");
                msgGen = BotMessages.RespuestaIaVacia(ctx.Locale);
            }
            else
            {
                var gr = GuardrailService.AplicarGuardrails(respGen.Message, ctx.Locale, null);
                if (gr.UsedFallback)
                    ctx.Log.LogWarning("Guardrail(ProblemaGeneral): {Reason}", gr.Reason);
                msgGen = gr.Mensaje;
            }

            ctx.Sesion.Estado = EstadoConversacion.ReintegroExit;
            ctx.Sesion.LastOptions = BotMessages.OpcionesExit(ctx.Locale);
            await ctx.GuardarSesion(ctx.Telefono, ctx.Sesion, ct);
            return new StateResult { Mensaje = msgGen + "\n\n" + BotMessages.ConstruirExit(ctx.Locale) };
        }
    }
}
