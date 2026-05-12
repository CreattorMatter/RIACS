using System.Collections.Generic;

namespace ServicioReintegros.AssistCard.Aplicacion.Dtos
{
    public sealed class SamuChatRequest
    {
        public string? ModelName { get; set; }
        public string? Message { get; set; }
        public List<SamuArchivoAdjunto>? Archivos { get; set; }
    }

    public sealed class SamuArchivoAdjunto
    {
        public string Nombre { get; set; } = "documento";
        public string ContentType { get; set; } = "application/octet-stream";
        public string ContenidoBase64 { get; set; } = string.Empty;
    }

    public sealed class SamuChatResponse
    {
        public SamuChatData Data { get; set; } = new();
        public bool IsValid { get; set; } = true;
        public string? Error { get; set; }
    }

    public sealed class SamuChatData
    {
        public string OutputMessage { get; set; } = string.Empty;
        public bool Continue { get; set; } = true;
    }
}
