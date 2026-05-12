using System.Collections.Generic;

namespace ServicioReintegros.AssistCard.Aplicacion.Dtos
{
    public sealed class AgentResponse
    {
        public string? Message { get; set; }
        public List<string> Options { get; set; } = new();
        public bool Continue { get; set; } = true;
        public bool IsValid { get; set; } = true;
        public string? Error { get; set; }

        public UploadContext? UploadContext { get; set; }
    }

    public sealed class UploadContext
    {
        public int? BenefitRequestId { get; set; }
        public int? DocumentId { get; set; }
        public string? FileName { get; set; }
        public string? ContentType { get; set; }
    }
}
