using System;
using System.Collections.Generic;

namespace ServicioReintegros.AssistCard.Aplicacion.Dtos
{
    public sealed class ResumenReintegro
    {
        public string? BenefitId { get; set; }
        public string? CaseId { get; set; }
        public DateTime? CreateDate { get; set; }
        public string? BenefitType { get; set; }
        public string? BenefitTypeOriginal { get; set; }
        public string? Status { get; set; }
        public string? StatusOriginal { get; set; }
        public decimal? TotalRequestedUsd { get; set; }
        public decimal? TotalApprovedUsd { get; set; }
        public decimal? TotalDeniedUsd { get; set; }
        public List<DetallePago> PaymentDetails { get; set; } = new();
        public List<ItemDetalleReintegro> AwaitResult { get; set; } = new();
        public List<DocumentoPendiente> PendingDocuments { get; set; } = new();
    }

    public sealed class ItemDetalleReintegro
    {
        public string? Concept { get; set; }
        public string? Currency { get; set; }
        public decimal? Denied { get; set; }
        public decimal? DeniedUsd { get; set; }
        public List<string> DenialReason { get; set; } = new();
    }

    public sealed class DetallePago
    {
        public string? Metodo { get; set; }
        public decimal? MontoUsd { get; set; }
        public DateTime? FechaPago { get; set; }
        public string? Estado { get; set; }
        public string? MonedaOriginal { get; set; }
        public string? Detalle { get; set; }
    }

    public sealed class DocumentoPendiente
    {
        public int? DocumentId { get; set; }
        public string? DocumentType { get; set; }
        public string? Description { get; set; }
    }
}
