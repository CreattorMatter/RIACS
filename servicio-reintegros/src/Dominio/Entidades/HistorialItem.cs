namespace ServicioReintegros.AssistCard.Dominio.Entidades
{
    public sealed class HistorialItem
    {
        public string Rol { get; set; } = string.Empty;
        public string Texto { get; set; } = string.Empty;
        public long Ts { get; set; }
    }
}
