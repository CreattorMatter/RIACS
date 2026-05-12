using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ServicioReintegros.AssistCard.Aplicacion.Dtos;
using ServicioReintegros.AssistCard.Dominio.Entidades;

namespace ServicioReintegros.AssistCard.Aplicacion.Servicios.StateHandlers
{
    public sealed class OtrasConsultasMenuHandler : IStateHandler
    {
        public bool CanHandle(EstadoConversacion estado) => estado == EstadoConversacion.OtrasConsultasMenu;

        public async Task<StateResult> HandleAsync(StateHandlerContext ctx, CancellationToken ct)
        {
            var opciones = BotMessages.OpcionesOtrasConsultas(ctx.Locale);
            var elegido = OptionSelector.ElegirOpcion(ctx.TextoUsuario, opciones);

            if (elegido == null)
                return new StateResult { Mensaje = BotMessages.MenuOtrasConsultasHeader(ctx.Locale, ctx.Sesion.Nombre) };

            var idx = opciones.FindIndex(o => string.Equals(o, elegido, StringComparison.OrdinalIgnoreCase));

            // Última opción → Volver
            if (idx == opciones.Count - 1)
            {
                ctx.Sesion.Estado = EstadoConversacion.MenuPrincipal;
                await ctx.GuardarSesion(ctx.Telefono, ctx.Sesion, ct);
                return new StateResult { Mensaje = BotMessages.MenuPrincipalHeader(ctx.Locale, ctx.Sesion.Nombre) };
            }

            // Detallar problema
            if (idx == 2)
            {
                ctx.Sesion.Estado = EstadoConversacion.OtrasConsultasProblem;
                await ctx.GuardarSesion(ctx.Telefono, ctx.Sesion, ct);
                return new StateResult { Mensaje = BotMessages.PedirDetalleProblemaGeneral(ctx.Locale) };
            }

            var email = LocaleHelper.EmailSoporte(ctx.Locale);

            // Requisitos y pasos
            if (idx == 0)
            {
                var contexto = new IaRequestContext
                {
                    Intent = "RequisitosYPasosIniciarReintegro",
                    Nombre = ctx.Sesion.Nombre,
                    Email = email,
                    Locale = ctx.Locale
                };
                var prompt = PromptTemplates.RequisitosReintegro(ctx.Locale, email);
                var respIa = await ctx.Ia.EnviarAsync(ctx.Telefono, ctx.Locale, prompt, contexto, ct);

                string respuesta;
                if (respIa?.Error == "FoundryKnowledgeIndexDuplicated" || string.IsNullOrWhiteSpace(respIa?.Message))
                {
                    ctx.Log.LogWarning("Fallback(Requisitos): Foundry falló, usando fallback hardcodeado.");
                    respuesta = BotMessages.FallbackRequisitosReintegro(ctx.Locale, email);
                }
                else
                {
                    var gr = GuardrailService.AplicarGuardrails(respIa.Message, ctx.Locale,
                        BotMessages.FallbackRequisitosReintegro(ctx.Locale, email));
                    if (gr.UsedFallback)
                        ctx.Log.LogWarning("Guardrail(Requisitos): {Reason}", gr.Reason);
                    respuesta = gr.Mensaje;
                }

                ctx.Sesion.Estado = EstadoConversacion.ReintegroExit;
                ctx.Sesion.LastOptions = BotMessages.OpcionesExit(ctx.Locale);
                await ctx.GuardarSesion(ctx.Telefono, ctx.Sesion, ct);
                return new StateResult { Mensaje = respuesta + "\n\n" + BotMessages.ConstruirExit(ctx.Locale) };
            }

            // Problemas con la App
            if (idx == 1)
            {
                var contexto = new IaRequestContext
                {
                    Intent = "ProblemasApp",
                    Nombre = ctx.Sesion.Nombre,
                    Email = email,
                    Locale = ctx.Locale
                };
                var prompt = PromptTemplates.ProblemasApp(ctx.Locale, email);
                var respIa = await ctx.Ia.EnviarAsync(ctx.Telefono, ctx.Locale, prompt, contexto, ct);

                string respuesta;
                var fallback = BotMessages.FallbackProblemasApp(ctx.Locale, email);

                if (respIa?.Error == "FoundryKnowledgeIndexDuplicated" || string.IsNullOrWhiteSpace(respIa?.Message))
                {
                    ctx.Log.LogWarning("Fallback(ProblemasApp): Foundry falló, usando fallback hardcodeado.");
                    respuesta = fallback;
                }
                else
                {
                    var gr = GuardrailService.AplicarGuardrails(respIa.Message, ctx.Locale, fallback);
                    if (gr.UsedFallback)
                        ctx.Log.LogWarning("Guardrail(ProblemasApp): {Reason}", gr.Reason);
                    respuesta = gr.Mensaje;
                }

                ctx.Sesion.Estado = EstadoConversacion.ReintegroExit;
                ctx.Sesion.LastOptions = BotMessages.OpcionesExit(ctx.Locale);
                await ctx.GuardarSesion(ctx.Telefono, ctx.Sesion, ct);
                return new StateResult { Mensaje = respuesta + "\n\n" + BotMessages.ConstruirExit(ctx.Locale) };
            }

            return new StateResult { Mensaje = BotMessages.MenuOtrasConsultasHeader(ctx.Locale, ctx.Sesion.Nombre) };
        }
    }
}
