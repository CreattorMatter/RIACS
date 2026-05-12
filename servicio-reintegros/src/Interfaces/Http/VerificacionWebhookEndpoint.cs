using FastEndpoints;
using Microsoft.Extensions.Options;
using ServicioReintegros.AssistCard.Configuracion;
using System.Threading;
using System.Threading.Tasks;

namespace ServicioReintegros.AssistCard.Interfaces.Http
{
    // Endpoint de verificación GET de Meta (devuelve hub.challenge si VERIFY_TOKEN coincide)
    public sealed class VerificacionWebhookEndpoint : EndpointWithoutRequest
    {
        private readonly WabaOpciones _waba;
        public VerificacionWebhookEndpoint(IOptions<WabaOpciones> waba) => _waba = waba.Value;

        public override void Configure()
        {
            Get("/webhook/whatsapp");
            AllowAnonymous();
        }

        public override async Task HandleAsync(CancellationToken ct)
        {
            var query = HttpContext.Request.Query;
            var mode = query["hub.mode"].ToString();
            var challenge = query["hub.challenge"].ToString();
            var verifyToken = query["hub.verify_token"].ToString();

            if (mode == "subscribe" && verifyToken == _waba.VerifyToken && !string.IsNullOrWhiteSpace(challenge))
            {
                await SendStringAsync(challenge, cancellation: ct);
                return;
            }

            await SendAsync("No autorizado", 403, ct);
        }
    }
}
