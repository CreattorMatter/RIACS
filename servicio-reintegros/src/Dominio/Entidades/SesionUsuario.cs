using System;

namespace ServicioReintegros.AssistCard.Dominio.Entidades
{
    public sealed class SesionUsuario
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Telefono { get; set; } = string.Empty;
        public string Idioma { get; set; } = "es";
        public string? Estado { get; set; }
        public string? UltimaIntencion { get; set; }
        public string? ContextoJson { get; set; }
        public DateTime ActualizadoEn { get; set; } = DateTime.UtcNow;
    }
}
