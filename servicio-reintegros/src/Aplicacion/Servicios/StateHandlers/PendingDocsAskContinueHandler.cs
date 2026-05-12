using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ServicioReintegros.AssistCard.Aplicacion.Dtos;
using ServicioReintegros.AssistCard.Dominio.Entidades;

namespace ServicioReintegros.AssistCard.Aplicacion.Servicios.StateHandlers
{
    public sealed class PendingDocsAskContinueHandler : IStateHandler
    {
        public bool CanHandle(EstadoConversacion estado) => estado == EstadoConversacion.ReintegroPendingDocsAskContinue;

        public async Task<StateResult> HandleAsync(StateHandlerContext ctx, CancellationToken ct)
        {
            var opciones = BotMessages.OpcionesSiNo(ctx.Locale);
            var elegido = OptionSelector.ElegirOpcion(ctx.TextoUsuario, opciones);

            if (elegido == null)
                return new StateResult { Mensaje = BotMessages.PreguntaContinuar(ctx.Locale) };

            var idx = opciones.FindIndex(o => string.Equals(o, elegido, StringComparison.OrdinalIgnoreCase));

            // SI → volver a docs
            if (idx == 0)
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

            // NO → refrescar reintegro y volver a menú
            if (int.TryParse(ctx.Sesion.CurrentBenefitId, out var brId))
            {
                var consulta = new ConsultaIdentificador { BenefitRequestId = brId };
                var items = await ctx.Reintegros.BuscarPorIdentificadorAsync(consulta, ct);
                var r = items?.OrderByDescending(x => x.CreateDate ?? DateTime.MinValue).FirstOrDefault();
                if (r != null && ReintegroStatusHelper.EsResumenValido(r))
                {
                    await ctx.Localizer.LocalizarAsync(r, ctx.Locale, ct);
                    await ctx.GuardarReintegroActual(ctx.Telefono, r, ct);
                    ctx.Sesion.Estado = EstadoConversacion.ReintegroMenu;
                    ctx.Sesion.CurrentBenefitId = r.BenefitId;
                    ctx.Sesion.CurrentCaseId = r.CaseId;
                    await ctx.GuardarSesion(ctx.Telefono, ctx.Sesion, ct);
                    return new StateResult { Mensaje = BotMessages.ConstruirDetalleYMenuReintegro(r, ctx.Locale, ctx.Sesion.Nombre) };
                }
            }

            ctx.Sesion.Estado = EstadoConversacion.ReintegroMenu;
            await ctx.GuardarSesion(ctx.Telefono, ctx.Sesion, ct);
            var reintegroActual = await ctx.ObtenerReintegroActual(ctx.Telefono, ct);
            return new StateResult
            {
                Mensaje = reintegroActual == null
                    ? BotMessages.MensajeSinReintegro(ctx.Locale)
                    : BotMessages.ConstruirDetalleYMenuReintegro(reintegroActual, ctx.Locale, ctx.Sesion.Nombre)
            };
        }
    }
}
