using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServicioReintegros.AssistCard.Aplicacion.Puertos;
using ServicioReintegros.AssistCard.Configuracion;

namespace ServicioReintegros.AssistCard.Infraestructura.WhatsApp
{
    public sealed class MetaCloudAdapter : IProveedorWhatsApp
    {
        private const int MetaRecipientNotAllowed = 131030;
        private readonly HttpClient _http;
        private readonly WabaOpciones _waba;
        private readonly ILogger<MetaCloudAdapter> _logger;

        public MetaCloudAdapter(HttpClient http, IOptions<WabaOpciones> waba, ILogger<MetaCloudAdapter> logger)
        {
            _http = http;
            _waba = waba.Value;
            _logger = logger;
        }

        public async Task EnviarTextoAsync(string destino, string mensaje, CancellationToken ct = default)
        {
            var destinoNormalizado = NormalizarDestino(destino);
            await PostMensajeConFallbackAsync(destinoNormalizado, d => new
            {
                messaging_product = "whatsapp",
                to = d,
                type = "text",
                text = new { body = mensaje }
            }, ct);
        }

        public async Task EnviarPlantillaAsync(string destino, string plantillaId, string locale, IEnumerable<string> parametros, CancellationToken ct = default)
        {
            var bodyParams = parametros.Select(p => new { type = "text", text = p }).ToArray();
            var destinoNormalizado = NormalizarDestino(destino);
            await PostMensajeConFallbackAsync(destinoNormalizado, d => new
            {
                messaging_product = "whatsapp",
                to = d,
                type = "template",
                template = new
                {
                    name = plantillaId,
                    language = new { code = locale },
                    components = new object[]
                    {
                        new { type = "body", parameters = bodyParams }
                    }
                }
            }, ct);
        }

        public async Task<Stream> DescargarMediaAsync(string mediaId, CancellationToken ct = default)
        {
            // 1) Obtener URL del media
            using var reqMeta = new HttpRequestMessage(HttpMethod.Get, $"{mediaId}");
            reqMeta.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _waba.AccessToken);
            var respMeta = await _http.SendAsync(reqMeta, ct);
            respMeta.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await respMeta.Content.ReadAsStringAsync(ct));
            var url = doc.RootElement.GetProperty("url").GetString();
            if (string.IsNullOrWhiteSpace(url)) throw new InvalidOperationException("URL de media no encontrada");

            // 2) Descargar binario
            using var reqBin = new HttpRequestMessage(HttpMethod.Get, url);
            reqBin.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _waba.AccessToken);
            var respBin = await _http.SendAsync(reqBin, HttpCompletionOption.ResponseHeadersRead, ct);
            respBin.EnsureSuccessStatusCode();
            return await respBin.Content.ReadAsStreamAsync(ct);
        }

        public void VerificarFirma(Microsoft.AspNetCore.Http.IHeaderDictionary headers, Stream bodyStream)
        {
            // La verificación real se delega al verificador dedicado (inyectar y usar en el endpoint/pre-procesador)
            // Este método queda para cumplir el puerto. No implementa lógica directa aquí.
        }

        private async Task PostMensajeConFallbackAsync(string destinoNormalizado, Func<string, object> payloadFactory, CancellationToken ct)
        {
            var ex = await TryPostMensajeAsync(payloadFactory(destinoNormalizado), ct);
            if (ex == null) return;

            if (!HabilitarFallbackArgentina())
                throw ex;

            if (!EsErrorRecipientNotAllowed(ex))
                throw ex;

            var destinoFallback = QuitarNueveArgentina(destinoNormalizado);
            if (string.Equals(destinoFallback, destinoNormalizado, StringComparison.Ordinal))
                throw ex;

            _logger.LogWarning("Retry WhatsApp por 131030. to_original={ToOriginal} to_fallback={ToFallback}", destinoNormalizado, destinoFallback);

            var ex2 = await TryPostMensajeAsync(payloadFactory(destinoFallback), ct);
            if (ex2 != null)
                throw ex2;
        }

        private async Task<InvalidOperationException?> TryPostMensajeAsync(object payload, CancellationToken ct)
        {
            var url = $"{_waba.PhoneNumberId}/messages";
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _waba.AccessToken);
            var json = JsonSerializer.Serialize(payload);
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");
            var resp = await _http.SendAsync(req, ct);

            if (resp.IsSuccessStatusCode)
                return null;

            var body = await resp.Content.ReadAsStringAsync(ct);
            _logger.LogError("Error enviando mensaje WhatsApp. Status={StatusCode}. Body={Body}. Payload={Payload}", (int)resp.StatusCode, body, json);
            return new InvalidOperationException($"Error enviando mensaje WhatsApp: {(int)resp.StatusCode} - {body}");
        }

        private bool HabilitarFallbackArgentina()
            => string.Equals(_waba.EnableArgentinaFallback, "true", StringComparison.OrdinalIgnoreCase);

        private static bool EsErrorRecipientNotAllowed(Exception ex)
        {
            var msg = ex.Message ?? string.Empty;
            if (!msg.Contains("131030", StringComparison.Ordinal))
                return false;

            try
            {
                var idx = msg.IndexOf("{\"error\"", StringComparison.Ordinal);
                if (idx < 0) return true;
                var json = msg.Substring(idx);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("error", out var err) &&
                    err.TryGetProperty("code", out var codeEl) &&
                    codeEl.ValueKind == JsonValueKind.Number &&
                    codeEl.GetInt32() == MetaRecipientNotAllowed)
                    return true;
            }
            catch
            {
                return true;
            }

            return false;
        }

        private static string QuitarNueveArgentina(string destinoNormalizado)
        {
            // Formato típico AR en E.164 para móvil: 549 + área + número. Ej: 54911xxxxxxx
            if (string.IsNullOrWhiteSpace(destinoNormalizado)) return string.Empty;
            if (!destinoNormalizado.StartsWith("549", StringComparison.Ordinal)) return destinoNormalizado;
            if (destinoNormalizado.Length < 4) return destinoNormalizado;
            return "54" + destinoNormalizado.Substring(3);
        }

        private static string NormalizarDestino(string destino)
        {
            if (string.IsNullOrWhiteSpace(destino)) return string.Empty;
            return Regex.Replace(destino, @"\D", string.Empty);
        }
    }
}
