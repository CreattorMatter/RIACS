using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ServicioReintegros.AssistCard.Dominio.Entidades;

namespace ServicioReintegros.AssistCard.Aplicacion.Servicios.StateHandlers
{
    public sealed class ReintegroFinancialMenuHandler : IStateHandler
    {
        public bool CanHandle(EstadoConversacion estado) => estado == EstadoConversacion.ReintegroFinancialMenu;

        public async Task<StateResult> HandleAsync(StateHandlerContext ctx, CancellationToken ct)
        {
            var opciones = BotMessages.OpcionesFinanciero(ctx.Locale);
            var elegido = OptionSelector.ElegirOpcion(ctx.TextoUsuario, opciones);

            if (elegido == null)
                return new StateResult { Mensaje = BotMessages.MenuFinanciero(ctx.Locale) };

            var idx = opciones.FindIndex(o => string.Equals(o, elegido, StringComparison.OrdinalIgnoreCase));

            // Volver
            if (idx == opciones.Count - 1)
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

            // Detalle aprobación
            if (idx == 0)
            {
                var reintegro = await ctx.ObtenerReintegroActual(ctx.Telefono, ct);
                if (reintegro != null && ReintegroStatusHelper.EstaEnRevision(reintegro))
                {
                    return new StateResult
                    {
                        Mensaje = BotMessages.AprobacionNoDisponiblePorRevision(ctx.Locale) + "\n\n" + BotMessages.MenuFinanciero(ctx.Locale)
                    };
                }
            }

            // Motivo denegación
            if (idx == 1)
            {
                var reintegro = await ctx.ObtenerReintegroActual(ctx.Telefono, ct);
                if (reintegro != null)
                    await ctx.Localizer.LocalizarAsync(reintegro, ctx.Locale, ct);

                var det = reintegro == null ? null : BotMessages.DetalleDenegacion(reintegro, ctx.Locale);
                if (!string.IsNullOrWhiteSpace(det))
                {
                    ctx.Sesion.EstadoVolver = EstadoConversacion.ReintegroFinancialMenu;
                    ctx.Sesion.Estado = EstadoConversacion.ReintegroExit;
                    await ctx.GuardarSesion(ctx.Telefono, ctx.Sesion, ct);
                    return new StateResult { Mensaje = det + "\n\n" + BotMessages.ConstruirExit(ctx.Locale) };
                }
            }

            // Fallback IA para aprobación/denegación
            var prompt = idx == 0
                ? PromptTemplates.ExplicarAprobacion(ctx.Locale)
                : PromptTemplates.ExplicarDenegacion(ctx.Locale);

            var respIa = await ctx.Ia.ResponderFaqAsync(prompt, ctx.Locale, ct);
            ctx.Sesion.EstadoVolver = EstadoConversacion.ReintegroFinancialMenu;
            ctx.Sesion.Estado = EstadoConversacion.ReintegroExit;
            await ctx.GuardarSesion(ctx.Telefono, ctx.Sesion, ct);
            return new StateResult { Mensaje = respIa + "\n\n" + BotMessages.ConstruirExit(ctx.Locale) };
        }
    }
}
