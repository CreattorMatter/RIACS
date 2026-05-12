namespace ServicioReintegros.AssistCard.Aplicacion.Dtos
{
    public sealed class ConsultaIdentificador
    {
        public int? BenefitRequestId { get; set; }
        public string? CaseId { get; set; }
        public string? Email { get; set; }
        public string? CountryIsoCode { get; set; }
        public string? NationalId { get; set; }
        public string? VoucherCode { get; set; }
    }
}
