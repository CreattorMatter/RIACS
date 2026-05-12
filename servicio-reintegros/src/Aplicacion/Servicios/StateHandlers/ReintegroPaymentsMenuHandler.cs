using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ServicioReintegros.AssistCard.Aplicacion.Dtos;
using ServicioReintegros.AssistCard.Dominio.Entidades;

namespace ServicioReintegros.AssistCard.Aplicacion.Servicios.StateHandlers
{
    public sealed class ReintegroPaymentsMenuHandler : IStateHandler
    {
        public bool CanHandle(EstadoConversacion estado) => estado == EstadoConversacion.ReintegroPaymentsMenu;

        public async Task<StateResult> HandleAsync(StateHandlerContext ctx, CancellationToken ct)
        {
            var reintegro = await ctx.ObtenerReintegroActual(ctx.Telefono, ct);
            var opciones = BotMessages.OpcionesPagos(ctx.Locale);
            var elegido = OptionSelector.ElegirOpcion(ctx.TextoUsuario, opciones);

            if (elegido == null)
                return new StateResult { Mensaje = BotMessages.MenuPagos(ctx.Locale) };

            var idx = opciones.FindIndex(o => string.Equals(o, elegido, StringComparison.OrdinalIgnoreCase));

            // Volver
            if (idx == opciones.Count - 1)
            {
                ctx.Sesion.Estado = EstadoConversacion.ReintegroMenu;
                await ctx.GuardarSesion(ctx.Telefono, ctx.Sesion, ct);
                return new StateResult
                {
                    Mensaje = reintegro == null
                        ? BotMessages.MensajeSinReintegro(ctx.Locale)
                        : BotMessages.ConstruirDetalleYMenuReintegro(reintegro, ctx.Locale, ctx.Sesion.Nombre)
                };
            }

            // Ver detalle de pago
            if (idx == 2)
            {
                var detalles = reintegro?.PaymentDetails ?? new List<DetallePago>();
                var respIa = await ctx.Ia.ExplicarPagoAsync(detalles, ctx.Locale, ct);
                ctx.Sesion.Estado = EstadoConversacion.ReintegroExit;
                await ctx.GuardarSesion(ctx.Telefono, ctx.Sesion, ct);
                return new StateResult { Mensaje = respIa + "\n\n" + BotMessages.ConstruirExit(ctx.Locale) };
            }

            // Tipo de cambio (0) o Problemas de pago (1)
            var prompt = idx == 0
                ? PromptTemplates.TipoCambioPagos(ctx.Locale)
                : PromptTemplates.ProblemasPago(ctx.Locale);

            var resp = await ctx.Ia.ResponderFaqAsync(prompt, ctx.Locale, ct);
            ctx.Sesion.Estado = EstadoConversacion.ReintegroExit;
            await ctx.GuardarSesion(ctx.Telefono, ctx.Sesion, ct);
            return new StateResult { Mensaje = resp + "\n\n" + BotMessages.ConstruirExit(ctx.Locale) };
        }
    }
}
