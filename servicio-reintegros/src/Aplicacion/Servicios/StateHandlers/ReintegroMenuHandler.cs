using System;
using System.Threading;
using System.Threading.Tasks;
using ServicioReintegros.AssistCard.Dominio.Entidades;

namespace ServicioReintegros.AssistCard.Aplicacion.Servicios.StateHandlers
{
    public sealed class ReintegroMenuHandler : IStateHandler
    {
        public bool CanHandle(EstadoConversacion estado) => estado == EstadoConversacion.ReintegroMenu;

        public async Task<StateResult> HandleAsync(StateHandlerContext ctx, CancellationToken ct)
        {
            var reintegro = await ctx.ObtenerReintegroActual(ctx.Telefono, ct);
            if (reintegro == null)
            {
                ctx.Sesion.Estado = EstadoConversacion.AwaitingIdentifier;
                ctx.Sesion.CurrentBenefitId = null;
                ctx.Sesion.CurrentCaseId = null;
                await ctx.GuardarSesion(ctx.Telefono, ctx.Sesion, ct);
                return new StateResult { Mensaje = BotMessages.PedidoIdentificador(ctx.Locale, ctx.Sesion.Nombre) };
            }

            var opciones = BotMessages.ConstruirOpcionesReintegroMenu(reintegro, ctx.Locale);
            var elegido = OptionSelector.ElegirOpcion(ctx.TextoUsuario, opciones);

            if (elegido == null)
                return new StateResult { Mensaje = BotMessages.ConstruirDetalleYMenuReintegro(reintegro, ctx.Locale, ctx.Sesion.Nombre) };

            // Volver
            if (string.Equals(elegido, BotMessages.OpcionVolverMenuAnterior(ctx.Locale), StringComparison.OrdinalIgnoreCase))
            {
                ctx.Sesion.Estado = EstadoConversacion.MenuPrincipal;
                ctx.Sesion.CurrentBenefitId = null;
                ctx.Sesion.CurrentCaseId = null;
                await ctx.GuardarSesion(ctx.Telefono, ctx.Sesion, ct);
                return new StateResult { Mensaje = BotMessages.MenuPrincipalHeader(ctx.Locale, ctx.Sesion.Nombre) };
            }

            // Detalle financiero
            if (string.Equals(elegido, BotMessages.OpcionDetalleFinanciero(ctx.Locale), StringComparison.OrdinalIgnoreCase))
            {
                ctx.Sesion.Estado = EstadoConversacion.ReintegroFinancialMenu;
                await ctx.GuardarSesion(ctx.Telefono, ctx.Sesion, ct);
                return new StateResult { Mensaje = BotMessages.MenuFinanciero(ctx.Locale) };
            }

            // Detalle pagos
            if (string.Equals(elegido, BotMessages.OpcionDetallePagos(ctx.Locale), StringComparison.OrdinalIgnoreCase))
            {
                ctx.Sesion.Estado = EstadoConversacion.ReintegroPaymentsMenu;
                await ctx.GuardarSesion(ctx.Telefono, ctx.Sesion, ct);
                return new StateResult { Mensaje = BotMessages.MenuPagos(ctx.Locale) };
            }

            // Documentación pendiente
            if (string.Equals(elegido, BotMessages.OpcionDocumentacionPendiente(ctx.Locale), StringComparison.OrdinalIgnoreCase))
            {
                ctx.Sesion.Estado = EstadoConversacion.ReintegroPendingDocsSelect;
                await ctx.GuardarSesion(ctx.Telefono, ctx.Sesion, ct);
                return new StateResult { Mensaje = BotMessages.MenuDocumentosPendientes(reintegro, ctx.Locale) };
            }

            // Detallar problema
            if (string.Equals(elegido, BotMessages.OpcionDetallarProblemaReintegro(ctx.Locale), StringComparison.OrdinalIgnoreCase))
            {
                ctx.Sesion.Estado = EstadoConversacion.ReintegroProblem;
                await ctx.GuardarSesion(ctx.Telefono, ctx.Sesion, ct);
                return new StateResult { Mensaje = BotMessages.PedirDetalleProblemaReintegro(ctx.Locale) };
            }

            // Detalle estado
            if (string.Equals(elegido, BotMessages.OpcionDetalleEstado(ctx.Locale), StringComparison.OrdinalIgnoreCase))
            {
                var statusRaw = reintegro.Status ?? reintegro.StatusOriginal ?? string.Empty;
                var typeRaw = reintegro.BenefitType ?? reintegro.BenefitTypeOriginal ?? string.Empty;
                var expl = await ctx.Ia.ExplicarEstadoAsync(statusRaw, typeRaw, ctx.Locale, ct);
                ctx.Sesion.Estado = EstadoConversacion.ReintegroExit;
                await ctx.GuardarSesion(ctx.Telefono, ctx.Sesion, ct);
                return new StateResult { Mensaje = expl + "\n\n" + BotMessages.ConstruirExit(ctx.Locale) };
            }

            // Próximos pasos
            if (string.Equals(elegido, BotMessages.OpcionProximosPasos(ctx.Locale), StringComparison.OrdinalIgnoreCase))
            {
                var statusRaw = reintegro.Status ?? reintegro.StatusOriginal ?? string.Empty;
                var expl = await ctx.Ia.ExplicarProximosPasosAsync(statusRaw, ctx.Locale, ct);
                ctx.Sesion.Estado = EstadoConversacion.ReintegroExit;
                await ctx.GuardarSesion(ctx.Telefono, ctx.Sesion, ct);
                return new StateResult { Mensaje = expl + "\n\n" + BotMessages.ConstruirExit(ctx.Locale) };
            }

            return new StateResult { Mensaje = BotMessages.ConstruirDetalleYMenuReintegro(reintegro, ctx.Locale, ctx.Sesion.Nombre) };
        }
    }
}
