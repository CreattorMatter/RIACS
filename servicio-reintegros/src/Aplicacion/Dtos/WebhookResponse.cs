namespace ServicioReintegros.AssistCard.Aplicacion.Dtos
{
    // Respuesta genérica hacia la consola/logs (el envío al usuario lo hace el adaptador de WhatsApp)
    public sealed class WebhookResponse
    {
        public bool Exito { get; set; }
        public string Mensaje { get; set; } = string.Empty;
    }
}
