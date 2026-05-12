using System;

namespace ServicioReintegros.AssistCard.Dominio.Entidades
{
    public sealed class CacheReintegros
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Clave { get; set; } = string.Empty;
        public string? ValorJson { get; set; }
        public DateTime? ExpiraEn { get; set; }
    }
}
