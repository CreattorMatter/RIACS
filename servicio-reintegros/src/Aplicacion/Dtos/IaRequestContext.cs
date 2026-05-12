using System.Collections.Generic;
using System.Net;
using ServicioReintegros.AssistCard.Dominio.Entidades;

namespace ServicioReintegros.AssistCard.Aplicacion.Dtos
{
    public sealed class IaRequestContext
    {
        public string? Intent { get; init; }
        public string? Question { get; init; }
        public string? Locale { get; init; }
        public string? Nombre { get; init; }
        public string? Email { get; init; }
        public string? Estado { get; init; }
        public string? Canal { get; init; }
        public string? Telefono { get; init; }
        public string? Status { get; init; }
        public string? BenefitType { get; init; }
        public string? Problem { get; init; }
        public int? DiasDesdeCarga { get; init; }
        public bool? DentroPrimeros20Dias { get; init; }
        public List<string>? LastOptions { get; init; }
        public List<HistorialItem>? Historial { get; init; }
        public ResumenReintegro? Reintegro { get; init; }
        public object? PaymentDetails { get; init; }
        public object? AdditionalContext { get; init; }
    }

    internal sealed class KnowledgeRetrieveResult
    {
        public bool Success { get; init; }
        public bool PartialContent { get; init; }
        public string? Message { get; init; }
        public string? Error { get; init; }
        public string? RawBody { get; init; }
        public HttpStatusCode? StatusCode { get; init; }
    }

    internal sealed class FoundryResponseResult
    {
        public bool Success { get; init; }
        public string? Message { get; init; }
        public string? RawBody { get; init; }
        public string? Error { get; init; }
        public HttpStatusCode? StatusCode { get; init; }
    }

    internal sealed class SearchDocumentHit
    {
        public string? Title { get; init; }
        public string? Path { get; init; }
        public string? Content { get; init; }
        public double? Score { get; init; }
    }
}
