using FastEndpoints;
using System.Threading;
using System.Threading.Tasks;

namespace ServicioReintegros.AssistCard.Interfaces.Http
{
    // Endpoint informativo (las métricas reales están en /metrics vía Prometheus)
    public sealed class MetricsEndpoint : EndpointWithoutRequest<string>
    {
        public override void Configure()
        {
            Get("/metrics-info");
            AllowAnonymous();
        }

        public override Task HandleAsync(CancellationToken ct)
        {
            return SendStringAsync("Use el endpoint /metrics para ver métricas Prometheus.", cancellation: ct);
        }
    }
}
