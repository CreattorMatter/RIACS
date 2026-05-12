namespace ServicioReintegros.AssistCard.Aplicacion.Servicios.StateHandlers
{
    /// <summary>
    /// Resultado producido por un state handler.
    /// </summary>
    public sealed class StateResult
    {
        public string Mensaje { get; set; } = string.Empty;
        public bool Procesado { get; set; } = true;
    }
}
