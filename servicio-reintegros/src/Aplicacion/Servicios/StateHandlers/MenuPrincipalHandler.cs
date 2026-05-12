using System;
using System.Threading;
using System.Threading.Tasks;
using ServicioReintegros.AssistCard.Dominio.Entidades;

namespace ServicioReintegros.AssistCard.Aplicacion.Servicios.StateHandlers
{
    public sealed class MenuPrincipalHandler : IStateHandler
    {
        public bool CanHandle(EstadoConversacion estado) => estado == EstadoConversacion.MenuPrincipal;

        public async Task<StateResult> HandleAsync(StateHandlerContext ctx, CancellationToken ct)
        {
            var opciones = BotMessages.OpcionesMenuPrincipal(ctx.Locale);
            var elegido = OptionSelector.ElegirOpcion(ctx.TextoUsuario, opciones);

            if (elegido == null)
                return new StateResult { Mensaje = BotMessages.MenuPrincipalHeader(ctx.Locale, ctx.Sesion.Nombre) };

            var idx = opciones.FindIndex(o => string.Equals(o, elegido, StringComparison.OrdinalIgnoreCase));

            if (idx == 0)
            {
                ctx.Sesion.Estado = EstadoConversacion.AwaitingIdentifier;
                await ctx.GuardarSesion(ctx.Telefono, ctx.Sesion, ct);
                return new StateResult { Mensaje = BotMessages.PedidoIdentificador(ctx.Locale, ctx.Sesion.Nombre) };
            }

            if (idx == 1)
            {
                ctx.Sesion.Estado = EstadoConversacion.OtrasConsultasMenu;
                await ctx.GuardarSesion(ctx.Telefono, ctx.Sesion, ct);
                return new StateResult { Mensaje = BotMessages.MenuOtrasConsultasHeader(ctx.Locale, ctx.Sesion.Nombre) };
            }

            return new StateResult { Mensaje = BotMessages.MenuPrincipalHeader(ctx.Locale, ctx.Sesion.Nombre) };
        }
    }
}
