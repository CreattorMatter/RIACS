using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ServicioReintegros.AssistCard.Aplicacion.Dtos;
using ServicioReintegros.AssistCard.Dominio.Entidades;

namespace ServicioReintegros.AssistCard.Aplicacion.Servicios.StateHandlers
{
    public sealed class AwaitingIdentifierHandler : IStateHandler
    {
        public bool CanHandle(EstadoConversacion estado) => estado == EstadoConversacion.AwaitingIdentifier;

        public async Task<StateResult> HandleAsync(StateHandlerContext ctx, CancellationToken ct)
        {
            if (!IdentificadorParser.TryParse(ctx.TextoUsuario, out var consulta) || consulta == null)
            {
                return new StateResult { Mensaje = BotMessages.PedidoIdentificador(ctx.Locale, ctx.Sesion.Nombre) };
            }

            List<ResumenReintegro> resultados;
            var sw = Stopwatch.StartNew();
            try
            {
                var items = await ctx.Reintegros.BuscarPorIdentificadorAsync(consulta, ct);
                sw.Stop();
                resultados = items?.ToList() ?? new List<ResumenReintegro>();
            }
            catch (Exception ex)
            {
                sw.Stop();
                ctx.Log.LogError(ex,
                    "ConsultaReintegro.Error telefono={Telefono} elapsedMs={ElapsedMs}",
                    ctx.Telefono, sw.ElapsedMilliseconds);
                return new StateResult { Mensaje = BotMessages.ErrorConsultarReintegro(ctx.Locale) };
            }

            if (resultados.Count == 0)
            {
                ctx.Sesion.Estado = EstadoConversacion.AwaitingIdentifier;
                ctx.Sesion.CurrentBenefitId = null;
                ctx.Sesion.CurrentCaseId = null;
                await ctx.GuardarSesion(ctx.Telefono, ctx.Sesion, ct);
                return new StateResult { Mensaje = BotMessages.NoEncontreReintegro(ctx.Locale) };
            }

            if (resultados.Count > 1)
            {
                await ctx.GuardarListaReintegros(ctx.Telefono, resultados, ct);
                ctx.Sesion.Estado = EstadoConversacion.SelectingReintegro;
                ctx.Sesion.CurrentBenefitId = null;
                ctx.Sesion.CurrentCaseId = null;
                await ctx.GuardarSesion(ctx.Telefono, ctx.Sesion, ct);
                return new StateResult { Mensaje = BotMessages.MensajeSeleccionReintegro(resultados, ctx.Locale) };
            }

            var elegido = resultados.OrderByDescending(r => r.CreateDate ?? DateTime.MinValue).First();

            if (!ReintegroStatusHelper.EsResumenValido(elegido))
            {
                ctx.Sesion.Estado = EstadoConversacion.AwaitingIdentifier;
                ctx.Sesion.CurrentBenefitId = null;
                ctx.Sesion.CurrentCaseId = null;
                await ctx.GuardarSesion(ctx.Telefono, ctx.Sesion, ct);
                return new StateResult { Mensaje = BotMessages.NoEncontreRegistroValido(ctx.Locale) };
            }

            await ctx.Localizer.LocalizarAsync(elegido, ctx.Locale, ct);
            await ctx.GuardarReintegroActual(ctx.Telefono, elegido, ct);

            ctx.Sesion.Estado = EstadoConversacion.ReintegroMenu;
            ctx.Sesion.CurrentBenefitId = elegido.BenefitId;
            ctx.Sesion.CurrentCaseId = elegido.CaseId;
            await ctx.GuardarSesion(ctx.Telefono, ctx.Sesion, ct);

            return new StateResult { Mensaje = BotMessages.ConstruirDetalleYMenuReintegro(elegido, ctx.Locale, ctx.Sesion.Nombre) };
        }
    }
}
