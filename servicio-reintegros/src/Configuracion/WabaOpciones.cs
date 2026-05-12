namespace ServicioReintegros.AssistCard.Configuracion
{
    public sealed class WabaOpciones
    {
        public string AccessToken { get; set; } = string.Empty;
        public string AppSecret { get; set; } = string.Empty;
        public string PhoneNumberId { get; set; } = string.Empty;
        public string BusinessAccountId { get; set; } = string.Empty;
        public string ApiBase { get; set; } = "https://graph.facebook.com/v20.0";
        public string VerifyToken { get; set; } = string.Empty;
        public string EnableArgentinaFallback { get; set; } = "false";
    }
}
