using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ServicioReintegros.AssistCard.Dominio.Entidades;

namespace ServicioReintegros.AssistCard.Aplicacion.Servicios.StateHandlers
{
    /// <summary>
    /// Despacha el turno al IStateHandler correspondiente según el estado de la sesión.
    /// Si ningún handler coincide, devuelve null para que el orquestador use fallback.
    /// </summary>
    public sealed class StateDispatcher
    {
        private readonly List<IStateHandler> _handlers;

        public StateDispatcher()
        {
            _handlers = new List<IStateHandler>
            {
                new AwaitingNameHandler(),
                new MenuPrincipalHandler(),
                new OtrasConsultasMenuHandler(),
                new OtrasConsultasProblemHandler(),
                new AwaitingIdentifierHandler(),
                new SelectingReintegroHandler(),
                new ReintegroMenuHandler(),
                new ReintegroFinancialMenuHandler(),
                new ReintegroPaymentsMenuHandler(),
                new PendingDocsSelectHandler(),
                new PendingDocsActionHandler(),
                new PendingDocsAskContinueHandler(),
                new ReintegroProblemHandler(),
                new ReintegroExitHandler()
            };
        }

        public IStateHandler? Resolve(EstadoConversacion estado)
            => _handlers.FirstOrDefault(h => h.CanHandle(estado));

        public async Task<StateResult?> DispatchAsync(StateHandlerContext ctx, CancellationToken ct)
        {
            var handler = Resolve(ctx.Sesion.Estado);
            if (handler == null) return null;
            return await handler.HandleAsync(ctx, ct);
        }
    }
}
