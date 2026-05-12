using System;
using System.Threading;
using System.Threading.Tasks;
using ServicioReintegros.AssistCard.Aplicacion.Dtos;
using Microsoft.Extensions.Logging;
using ServicioReintegros.AssistCard.Dominio.Entidades;

namespace ServicioReintegros.AssistCard.Aplicacion.Servicios.StateHandlers
{
    public sealed class ReintegroExitHandler : IStateHandler
    {
        public bool CanHandle(EstadoConversacion estado) => estado == EstadoConversacion.ReintegroExit;

        public async Task<StateResult> HandleAsync(StateHandlerContext ctx, CancellationToken ct)
        {
            var opciones = BotMessages.OpcionesExit(ctx.Locale);
            var elegido = OptionSelector.ElegirOpcion(ctx.TextoUsuario, opciones);

            // Texto libre → FAQ por IA
            if (elegido == null)
            {
                var reintegro = await ctx.ObtenerReintegroActual(ctx.Telefono, ct);
                var prompt = PromptTemplates.ConsultaFaq(ctx.Locale);
                var contexto = new IaRequestContext
                {
                    Intent = "ConsultaFAQ",
                    Question = ctx.TextoUsuario,
                    Nombre = ctx.Sesion.Nombre,
                    Reintegro = reintegro,
                    Locale = ctx.Locale
                };
                var respFaq = await ctx.Ia.EnviarAsync(ctx.Telefono, ctx.Locale, prompt, contexto, ct);

                ctx.Sesion.Estado = EstadoConversacion.ReintegroExit;
                await ctx.GuardarSesion(ctx.Telefono, ctx.Sesion, ct);
                return new StateResult { Mensaje = (respFaq?.Message ?? string.Empty) + "\n\n" + BotMessages.ConstruirExit(ctx.Locale) };
            }

            var idx = opciones.FindIndex(o => string.Equals(o, elegido, StringComparison.OrdinalIgnoreCase));

            // Cerrar
            if (idx == 1)
            {
                ctx.Sesion.Estado = EstadoConversacion.Ended;
                await ctx.GuardarSesion(ctx.Telefono, ctx.Sesion, ct);
                return new StateResult { Mensaje = BotMessages.ConversacionFinalizada(ctx.Locale) };
            }

            // Volver: usar EstadoVolver si existe
            if ((ctx.Sesion.CurrentBenefitId != null || ctx.Sesion.CurrentCaseId != null) && ctx.Sesion.EstadoVolver.HasValue)
            {
                var volver = ctx.Sesion.EstadoVolver.Value;
                ctx.Sesion.EstadoVolver = null;

                if (volver == EstadoConversacion.ReintegroFinancialMenu)
                {
                    ctx.Sesion.Estado = EstadoConversacion.ReintegroFinancialMenu;
                    await ctx.GuardarSesion(ctx.Telefono, ctx.Sesion, ct);
                    return new StateResult { Mensaje = BotMessages.MenuFinanciero(ctx.Locale) };
                }
            }

            // Volver al menú de reintegro si hay contexto
            if (ctx.Sesion.CurrentBenefitId != null || ctx.Sesion.CurrentCaseId != null)
            {
                ctx.Sesion.Estado = EstadoConversacion.ReintegroMenu;
                await ctx.GuardarSesion(ctx.Telefono, ctx.Sesion, ct);
                var reintegro = await ctx.ObtenerReintegroActual(ctx.Telefono, ct);
                return new StateResult
                {
                    Mensaje = reintegro == null
                        ? BotMessages.MensajeSinReintegro(ctx.Locale)
                        : BotMessages.ConstruirDetalleYMenuReintegro(reintegro, ctx.Locale, ctx.Sesion.Nombre)
                };
            }

            // Sin contexto de reintegro → otras consultas
            ctx.Sesion.Estado = EstadoConversacion.OtrasConsultasMenu;
            await ctx.GuardarSesion(ctx.Telefono, ctx.Sesion, ct);
            return new StateResult { Mensaje = BotMessages.MenuOtrasConsultasHeader(ctx.Locale, ctx.Sesion.Nombre) };
        }
    }
}
