using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ServicioReintegros.AssistCard.Dominio.Entidades;

namespace ServicioReintegros.AssistCard.Aplicacion.Servicios.StateHandlers
{
    public sealed class SelectingReintegroHandler : IStateHandler
    {
        public bool CanHandle(EstadoConversacion estado) => estado == EstadoConversacion.SelectingReintegro;

        public async Task<StateResult> HandleAsync(StateHandlerContext ctx, CancellationToken ct)
        {
            var lista = await ctx.ObtenerListaReintegros(ctx.Telefono, ct);
            if (lista.Count == 0)
            {
                ctx.Sesion.Estado = EstadoConversacion.AwaitingIdentifier;
                await ctx.GuardarSesion(ctx.Telefono, ctx.Sesion, ct);
                return new StateResult { Mensaje = BotMessages.PedidoIdentificador(ctx.Locale, ctx.Sesion.Nombre) };
            }

            var opciones = BotMessages.ConstruirOpcionesSeleccionReintegro(lista, ctx.Locale);
            var elegido = OptionSelector.ElegirOpcion(ctx.TextoUsuario, opciones);

            if (elegido == null)
                return new StateResult { Mensaje = BotMessages.MensajeSeleccionReintegro(lista, ctx.Locale) };

            var idx = opciones.FindIndex(o => string.Equals(o, elegido, StringComparison.OrdinalIgnoreCase));

            // Última opción = Volver
            if (idx == opciones.Count - 1)
            {
                ctx.Sesion.Estado = EstadoConversacion.MenuPrincipal;
                await ctx.GuardarSesion(ctx.Telefono, ctx.Sesion, ct);
                return new StateResult { Mensaje = BotMessages.MenuPrincipalHeader(ctx.Locale, ctx.Sesion.Nombre) };
            }

            if (idx < 0 || idx >= lista.Count)
                return new StateResult { Mensaje = BotMessages.MensajeSeleccionReintegro(lista, ctx.Locale) };

            var r = lista[idx];
            await ctx.Localizer.LocalizarAsync(r, ctx.Locale, ct);
            await ctx.GuardarReintegroActual(ctx.Telefono, r, ct);

            ctx.Sesion.CurrentBenefitId = r.BenefitId;
            ctx.Sesion.CurrentCaseId = r.CaseId;
            ctx.Sesion.Estado = EstadoConversacion.ReintegroMenu;
            await ctx.GuardarSesion(ctx.Telefono, ctx.Sesion, ct);

            return new StateResult { Mensaje = BotMessages.ConstruirDetalleYMenuReintegro(r, ctx.Locale, ctx.Sesion.Nombre) };
        }
    }
}
