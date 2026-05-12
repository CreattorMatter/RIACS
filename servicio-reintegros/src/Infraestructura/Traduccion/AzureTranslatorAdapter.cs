using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using ServicioReintegros.AssistCard.Aplicacion.Puertos;

namespace ServicioReintegros.AssistCard.Infraestructura.Traduccion
{
    public sealed class AzureTranslatorAdapter : IProveedorTraduccion
    {
        private readonly HttpClient _http;
        private readonly TranslatorOpciones _cfg;

        public AzureTranslatorAdapter(HttpClient http, IOptions<TranslatorOpciones> cfg)
        {
            _http = http;
            _cfg = cfg.Value;
        }

        public async Task<string> DetectarAsync(string texto, CancellationToken ct = default)
        {
            var reqBody = "[ { \"Text\": " + JsonSerializer.Serialize(texto) + " } ]";
            using var req = new HttpRequestMessage(HttpMethod.Post, "/detect?api-version=3.0");
            req.Content = new StringContent(reqBody, Encoding.UTF8, "application/json");
            req.Headers.Add("Ocp-Apim-Subscription-Key", _cfg.Key);
            if (!string.IsNullOrWhiteSpace(_cfg.Region)) req.Headers.Add("Ocp-Apim-Subscription-Region", _cfg.Region);
            var resp = await _http.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var lang = doc.RootElement[0].GetProperty("language").GetString();
            return string.IsNullOrWhiteSpace(lang) ? "es" : lang!;
        }

        public async Task<string> TraducirAsync(string texto, string destino, CancellationToken ct = default)
        {
            var reqBody = "[ { \"Text\": " + JsonSerializer.Serialize(texto) + " } ]";
            using var req = new HttpRequestMessage(HttpMethod.Post, $"/translate?api-version=3.0&to={destino}");
            req.Content = new StringContent(reqBody, Encoding.UTF8, "application/json");
            req.Headers.Add("Ocp-Apim-Subscription-Key", _cfg.Key);
            if (!string.IsNullOrWhiteSpace(_cfg.Region)) req.Headers.Add("Ocp-Apim-Subscription-Region", _cfg.Region);
            var resp = await _http.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var translated = doc.RootElement[0].GetProperty("translations")[0].GetProperty("text").GetString();
            return translated ?? texto;
        }
    }

    public sealed class TranslatorOpciones
    {
        public string Endpoint { get; set; } = string.Empty; // ej: https://api.cognitive.microsofttranslator.com
        public string Key { get; set; } = string.Empty;
        public string? Region { get; set; }
    }
}
