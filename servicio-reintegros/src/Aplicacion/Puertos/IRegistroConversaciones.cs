using System;
using System.Threading;
using System.Threading.Tasks;

namespace ServicioReintegros.AssistCard.Aplicacion.Puertos
{
    public interface IRegistroConversaciones
    {
        Task RegistrarAsync(
            string userChat,
            string assistanceChat,
            string identifier,
            string conversationId,
            string language,
            DateTime startSend,
            DateTime resultSend,
            CancellationToken ct = default);
    }
}
