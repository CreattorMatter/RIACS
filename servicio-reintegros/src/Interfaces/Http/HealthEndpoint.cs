using FastEndpoints;
using System.Threading;
using System.Threading.Tasks;

namespace ServicioReintegros.AssistCard.Interfaces.Http
{
    // Endpoint de salud simple (GET /healthz) - también mapeado como health check
    public sealed class HealthEndpoint : EndpointWithoutRequest<string>
    {
        public override void Configure()
        {
            Get("/healthz-check");
            AllowAnonymous();
        }

        public override Task HandleAsync(CancellationToken ct)
        {
            return SendStringAsync("OK", cancellation: ct);
        }
    }
}
