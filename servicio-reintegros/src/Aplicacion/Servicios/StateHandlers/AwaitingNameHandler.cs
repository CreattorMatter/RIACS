using System.Threading;
using System.Threading.Tasks;
using ServicioReintegros.AssistCard.Dominio.Entidades;

namespace ServicioReintegros.AssistCard.Aplicacion.Servicios.StateHandlers
{
    public sealed class AwaitingNameHandler : IStateHandler
    {
        public bool CanHandle(EstadoConversacion estado) => estado == EstadoConversacion.AwaitingName;

        public async Task<StateResult> HandleAsync(StateHandlerContext ctx, CancellationToken ct)
        {
            var nombre = (ctx.TextoUsuario ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(nombre))
            {
                return new StateResult { Mensaje = BotMessages.PedirNombre(ctx.Locale) };
            }

            if (!NameValidator.EsNombreProbable(nombre))
            {
                return new StateResult { Mensaje = BotMessages.PedirNombreTrasConsulta(ctx.Locale) };
            }

            ctx.Sesion.Nombre = nombre;
            ctx.Sesion.Estado = EstadoConversacion.MenuPrincipal;
            await ctx.GuardarSesion(ctx.Telefono, ctx.Sesion, ct);
            return new StateResult { Mensaje = BotMessages.MenuPrincipalHeader(ctx.Locale, ctx.Sesion.Nombre) };
        }
    }
}
