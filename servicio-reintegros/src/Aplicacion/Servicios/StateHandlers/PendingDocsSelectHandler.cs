using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ServicioReintegros.AssistCard.Dominio.Entidades;

namespace ServicioReintegros.AssistCard.Aplicacion.Servicios.StateHandlers
{
    public sealed class PendingDocsSelectHandler : IStateHandler
    {
        public bool CanHandle(EstadoConversacion estado) => estado == EstadoConversacion.ReintegroPendingDocsSelect;

        public async Task<StateResult> HandleAsync(StateHandlerContext ctx, CancellationToken ct)
        {
            var reintegro = await ctx.ObtenerReintegroActual(ctx.Telefono, ct);
            if (reintegro == null)
            {
                ctx.Sesion.Estado = EstadoConversacion.ReintegroMenu;
                await ctx.GuardarSesion(ctx.Telefono, ctx.Sesion, ct);
                return new StateResult { Mensaje = BotMessages.MensajeSinReintegro(ctx.Locale) };
            }

            var opciones = BotMessages.ConstruirOpcionesDocumentosPendientes(reintegro, ctx.Locale);
            var elegido = OptionSelector.ElegirOpcion(ctx.TextoUsuario, opciones);

            if (elegido == null)
                return new StateResult { Mensaje = BotMessages.MenuDocumentosPendientes(reintegro, ctx.Locale) };

            var idx = opciones.FindIndex(o => string.Equals(o, elegido, StringComparison.OrdinalIgnoreCase));

            // Volver
            if (idx == opciones.Count - 1)
            {
                ctx.Sesion.Estado = EstadoConversacion.ReintegroMenu;
                await ctx.GuardarSesion(ctx.Telefono, ctx.Sesion, ct);
                return new StateResult { Mensaje = BotMessages.ConstruirDetalleYMenuReintegro(reintegro, ctx.Locale, ctx.Sesion.Nombre) };
            }

            var doc = reintegro.PendingDocuments?
                .FirstOrDefault(d => d != null && BotMessages.FormatearDocOpcion(d, ctx.Locale).Equals(elegido, StringComparison.OrdinalIgnoreCase));

            if (doc == null)
                return new StateResult { Mensaje = BotMessages.MenuDocumentosPendientes(reintegro, ctx.Locale) };

            ctx.Sesion.PendingDocIdSeleccionado = doc.DocumentId;
            ctx.Sesion.Estado = EstadoConversacion.ReintegroPendingDocsAction;
            await ctx.GuardarSesion(ctx.Telefono, ctx.Sesion, ct);
            return new StateResult { Mensaje = BotMessages.MenuAccionDocumento(ctx.Locale, elegido) };
        }
    }
}
