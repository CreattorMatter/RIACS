using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using ServicioReintegros.AssistCard.Aplicacion.Puertos;
using ServicioReintegros.AssistCard.Configuracion;

namespace ServicioReintegros.AssistCard.Infraestructura.Logs
{
    public sealed class SamuLoggerAdapter : IRegistroConversaciones
    {
        private readonly HttpClient _http;
        private readonly LoggerOpciones _cfg;

        public SamuLoggerAdapter(HttpClient http, IOptions<LoggerOpciones> cfg)
        {
            _http = http;
            _cfg = cfg.Value;
        }

        public async Task RegistrarAsync(
            string userChat,
            string assistanceChat,
            string identifier,
            string conversationId,
            string language,
            DateTime startSend,
            DateTime resultSend,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_cfg.BaseUrl) || string.IsNullOrWhiteSpace(_cfg.ApiKey))
                return;

            var convId = string.IsNullOrWhiteSpace(conversationId) ? identifier : conversationId;

            var startUtc = startSend.Kind == DateTimeKind.Utc
                ? startSend
                : startSend.ToUniversalTime();

            var resultUtc = resultSend.Kind == DateTimeKind.Utc
                ? resultSend
                : resultSend.ToUniversalTime();

            var payload = new
            {
                userChat,
                assistanceChat,
                identifier,
                conversationId = convId,
                language,
                startSend = startUtc,
                resultSend = resultUtc
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, _cfg.BaseUrl);
            req.Headers.Add("x-api-key", _cfg.ApiKey);
            var json = JsonSerializer.Serialize(payload);
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                using var resp = await _http.SendAsync(req, ct);
                // No lanzamos excepción si falla; el registro es best-effort
                _ = await resp.Content.ReadAsStringAsync(ct);
            }
            catch
            {
                // Ignorar errores de logging para no afectar el flujo principal
            }
        }
    }
}
