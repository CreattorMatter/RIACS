using ServicioReintegros.AssistCard.Aplicacion.Dtos;

namespace ServicioReintegros.AssistCard.Aplicacion.Puertos
{
    public interface IProveedorIA
    {
        Task<AgentResponse> EnviarAsync(string userId, string locale, string textoEntrada, IaRequestContext? contexto = null, CancellationToken ct = default);
        Task<string> ExplicarEstadoAsync(string status, string benefitType, string locale, CancellationToken ct = default);
        Task<string> ExplicarProximosPasosAsync(string status, string locale, CancellationToken ct = default);
        Task<string> ExplicarPagoAsync(object detalles, string locale, CancellationToken ct = default);
        Task<string> ResponderFaqAsync(string pregunta, string locale, CancellationToken ct = default);
    }
}
