using System.Collections.Generic;

namespace ServicioReintegros.AssistCard.Dominio.Entidades
{
    public sealed class SesionConversacion
    {
        public EstadoConversacion Estado { get; set; } = EstadoConversacion.AwaitingName;
        public string? Nombre { get; set; }
        public string? CurrentBenefitId { get; set; }
        public string? CurrentCaseId { get; set; }
        public int? PendingDocIdSeleccionado { get; set; }
        public EstadoConversacion? EstadoVolver { get; set; }
        public List<string> LastOptions { get; set; } = new();
    }
}
