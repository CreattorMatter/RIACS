using System.Threading;
using System.Threading.Tasks;
using ServicioReintegros.AssistCard.Dominio.Entidades;

namespace ServicioReintegros.AssistCard.Aplicacion.Servicios.StateHandlers
{
    /// <summary>
    /// Contrato que debe implementar cada handler de estado.
    /// </summary>
    public interface IStateHandler
    {
        bool CanHandle(EstadoConversacion estado);
        Task<StateResult> HandleAsync(StateHandlerContext ctx, CancellationToken ct);
    }
}
