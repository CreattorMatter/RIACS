using FastEndpoints;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ServicioReintegros.AssistCard.Aplicacion.Dtos;
using ServicioReintegros.AssistCard.Aplicacion.Servicios;
using ServicioReintegros.AssistCard.Interfaces.Http;

namespace ServicioReintegros.AssistCard.Interfaces.Http
{
    // Endpoint del Webhook de WhatsApp (POST)
    public sealed class WebhookEndpoint : Endpoint<WebhookRequest, WebhookResponse>
    {
        private readonly OrquestadorConversacion _orquestador;
        private readonly IServiceScopeFactory _scopeFactory;

        public WebhookEndpoint(OrquestadorConversacion orquestador, IServiceScopeFactory scopeFactory)
        {
            _orquestador = orquestador;
            _scopeFactory = scopeFactory;
        }

        public override void Configure()
        {
            Post("/webhook/whatsapp");
            AllowAnonymous();
            PreProcessor<PreProcesadorFirma>();
        }

        public override async Task HandleAsync(WebhookRequest req, CancellationToken ct)
        {
            await SendAsync(new WebhookResponse { Exito = true, Mensaje = "OK" }, cancellation: ct);

            _ = Task.Run(async () =>
            {
                using var scope = _scopeFactory.CreateScope();
                var orq = scope.ServiceProvider.GetRequiredService<OrquestadorConversacion>();
                await orq.ProcesarEventoAsync(req, CancellationToken.None);
            }, CancellationToken.None);
        }
    }
}
