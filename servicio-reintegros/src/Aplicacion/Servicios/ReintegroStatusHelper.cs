using System;
using System.Linq;
using ServicioReintegros.AssistCard.Aplicacion.Dtos;

namespace ServicioReintegros.AssistCard.Aplicacion.Servicios
{
    /// <summary>
    /// Helpers para evaluar el estado de un ResumenReintegro.
    /// Extraído de OrquestadorConversacion para reutilización y testeo.
    /// </summary>
    public static class ReintegroStatusHelper
    {
        public static bool EstaEnRevision(ResumenReintegro? r)
        {
            if (r == null) return false;
            var status = (r.StatusOriginal ?? r.Status ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(status)) return false;
            return status.Contains("under review") || status.Contains("received")
                || status.Contains("en revision") || status.Contains("en revisao")
                || status.Contains("recebido");
        }

        public static bool EsEstadoPagoPendiente(ResumenReintegro? r)
        {
            if (r == null) return false;
            var norm = (r.StatusOriginal ?? r.Status ?? string.Empty).Trim().ToLowerInvariant();
            return norm.Contains("pending payment", StringComparison.OrdinalIgnoreCase)
                || norm.Contains("pago pendiente", StringComparison.OrdinalIgnoreCase)
                || norm.Contains("pagamento pendente", StringComparison.OrdinalIgnoreCase)
                || norm.Contains("pagamento em processo", StringComparison.OrdinalIgnoreCase);
        }

        public static bool EsResumenValido(ResumenReintegro? r)
        {
            if (r == null) return false;
            return !string.IsNullOrWhiteSpace(r.BenefitId) || !string.IsNullOrWhiteSpace(r.CaseId);
        }

        public static int? DiasDesdeCreacion(ResumenReintegro? r)
        {
            if (r?.CreateDate == null) return null;
            return (int)(DateTime.UtcNow - r.CreateDate.Value).TotalDays;
        }

        public static bool DentroPrimeros20Dias(ResumenReintegro? r)
        {
            var dias = DiasDesdeCreacion(r);
            return dias.HasValue && dias.Value <= 20;
        }
    }
}
