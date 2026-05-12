using System.Collections.Generic;

namespace ServicioReintegros.AssistCard.Configuracion
{
    public sealed class ReintegrosLocalizationOpciones
    {
        public Dictionary<string, Dictionary<string, string>> Status { get; set; } = new();
        public Dictionary<string, Dictionary<string, string>> BenefitType { get; set; } = new();
    }
}
