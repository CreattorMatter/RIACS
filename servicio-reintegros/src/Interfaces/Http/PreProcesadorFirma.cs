using FastEndpoints;
using ServicioReintegros.AssistCard.Infraestructura.Seguridad.Firmas;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ServicioReintegros.AssistCard.Configuracion;

namespace ServicioReintegros.AssistCard.Interfaces.Http
{
    public sealed class PreProcesadorFirma : PreProcessor<Aplicacion.Dtos.WebhookRequest, object>
    {
        private readonly WhatsAppMetaSignatureVerifier _verificador;
        private readonly IHostEnvironment _env;
        private readonly WabaOpciones _waba;

        public PreProcesadorFirma(WhatsAppMetaSignatureVerifier verificador, IHostEnvironment env, IOptions<WabaOpciones> waba)
        {
            _verificador = verificador;
            _env = env;
            _waba = waba.Value;
        }

        public override Task PreProcessAsync(IPreProcessorContext<Aplicacion.Dtos.WebhookRequest> ctx, object state, CancellationToken ct)
        {
            var http = ctx.HttpContext;
            var rawBody = http.Items.TryGetValue("RawBody", out var val) ? (val as string ?? string.Empty) : string.Empty;

            if (_env.IsDevelopment() || string.IsNullOrWhiteSpace(_waba.AppSecret))
            {
                // En desarrollo o sin AppSecret configurado, se omite la verificación de firma.
                return Task.CompletedTask;
            }

            _verificador.Verificar(http.Request.Headers, rawBody);
            return Task.CompletedTask;
        }
    }
}
