using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ServicioReintegros.AssistCard.Aplicacion.Dtos
{
    // Modelo mínimo del Webhook de WhatsApp para extraer mensajes entrantes
    public sealed class WebhookRequest
    {
        public string? Object { get; set; }
        public List<WebhookEntry>? Entry { get; set; }
    }

    public sealed class WebhookEntry
    {
        public List<WebhookChange>? Changes { get; set; }
    }

    public sealed class WebhookChange
    {
        public WebhookValue? Value { get; set; }
        public string? Field { get; set; }
    }

    public sealed class WebhookValue
    {
        public List<WebhookMessage>? Messages { get; set; }
        public List<WebhookContact>? Contacts { get; set; }
    }

    public sealed class WebhookContact
    {
        [JsonPropertyName("wa_id")]
        public string? WaId { get; set; }
    }

    public sealed class WebhookMessage
    {
        public string? From { get; set; }
        public string? Id { get; set; }
        public string? Type { get; set; }
        public WebhookText? Text { get; set; }
        public WebhookImage? Image { get; set; }
        public WebhookDocument? Document { get; set; }
    }

    public sealed class WebhookText
    {
        public string? Body { get; set; }
    }

    public sealed class WebhookImage
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("mime_type")]
        public string? MimeType { get; set; }

        [JsonPropertyName("caption")]
        public string? Caption { get; set; }
    }

    public sealed class WebhookDocument
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("mime_type")]
        public string? MimeType { get; set; }

        [JsonPropertyName("filename")]
        public string? FileName { get; set; }

        [JsonPropertyName("caption")]
        public string? Caption { get; set; }
    }
}
