using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ServicioReintegros.AssistCard.Aplicacion.Dtos;
using ServicioReintegros.AssistCard.Dominio.Entidades;

namespace ServicioReintegros.AssistCard.Aplicacion.Servicios.StateHandlers
{
    public sealed class PendingDocsActionHandler : IStateHandler
    {
        public bool CanHandle(EstadoConversacion estado) => estado == EstadoConversacion.ReintegroPendingDocsAction;

        public async Task<StateResult> HandleAsync(StateHandlerContext ctx, CancellationToken ct)
        {
            var opciones = BotMessages.OpcionesAccionDoc(ctx.Locale);
            var elegido = OptionSelector.ElegirOpcion(ctx.TextoUsuario, opciones);

            if (elegido == null)
                return new StateResult { Mensaje = BotMessages.MenuAccionDocumento(ctx.Locale, null) };

            var idx = opciones.FindIndex(o => string.Equals(o, elegido, StringComparison.OrdinalIgnoreCase));

            // Volver a docs
            if (idx == 1)
            {
                ctx.Sesion.Estado = EstadoConversacion.ReintegroPendingDocsSelect;
                await ctx.GuardarSesion(ctx.Telefono, ctx.Sesion, ct);
                var reintegro = await ctx.ObtenerReintegroActual(ctx.Telefono, ct);
                return new StateResult
                {
                    Mensaje = reintegro == null
                        ? BotMessages.MensajeSinReintegro(ctx.Locale)
                        : BotMessages.MenuDocumentosPendientes(reintegro, ctx.Locale)
                };
            }

            // Cerrar
            if (idx == 2)
            {
                ctx.Sesion.Estado = EstadoConversacion.Ended;
                await ctx.GuardarSesion(ctx.Telefono, ctx.Sesion, ct);
                return new StateResult { Mensaje = BotMessages.ConversacionFinalizada(ctx.Locale) };
            }

            // Cargar documento (idx == 0)
            var reintegroActual = await ctx.ObtenerReintegroActual(ctx.Telefono, ct);
            if (reintegroActual == null)
                return new StateResult { Mensaje = BotMessages.MensajeSinReintegro(ctx.Locale) };

            if (ctx.Sesion.PendingDocIdSeleccionado.HasValue && int.TryParse(reintegroActual.BenefitId, out var brId))
            {
                var up = new UploadContext { BenefitRequestId = brId, DocumentId = ctx.Sesion.PendingDocIdSeleccionado.Value };
                try
                {
                    await ctx.GuardarUploadContext(ctx.Telefono, up, ct);
                }
                catch (Exception ex)
                {
                    ctx.Log.LogWarning(ex, "Cache.GuardarUploadCtx fallo telefono={Telefono}", ctx.Telefono);
                }
            }

            await ctx.GuardarSesion(ctx.Telefono, ctx.Sesion, ct);
            return new StateResult { Mensaje = BotMessages.PedirUploadDocumento(ctx.Locale) };
        }
    }
}
