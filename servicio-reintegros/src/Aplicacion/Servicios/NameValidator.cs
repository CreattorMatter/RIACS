using System;
using System.Collections.Generic;
using System.Linq;

namespace ServicioReintegros.AssistCard.Aplicacion.Servicios
{
    /// <summary>
    /// Valida si un texto parece ser un nombre de persona.
    /// Extraído de OrquestadorConversacion para reutilización y testeo.
    /// </summary>
    public static class NameValidator
    {
        private static readonly string[] Keywords = new[]
        {
            "reintegro", "reembolso", "reimbursement", "refund",
            "necesito", "quiero", "consulta", "consultar", "informacion", "info",
            "ayuda", "help", "need", "want",
            "estado", "status", "pago", "pagos", "payment", "payments",
            "documento", "documentos", "upload", "cargar",
            "denegado", "denegacion", "denied"
        };

        private static readonly HashSet<string> Stopwords = new(StringComparer.OrdinalIgnoreCase)
        {
            "hola", "hello", "hi", "ola", "oi", "buenas",
            "ok", "okay", "si", "sí", "no",
            "menu", "menú", "back", "atras", "atrás"
        };

        public static bool EsNombreProbable(string texto)
        {
            var raw = (texto ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(raw)) return false;
            if (raw.Length > 70) return false;
            if (raw.Any(char.IsDigit)) return false;
            if (raw.Contains('@') || raw.Contains('?') || raw.Contains('!')) return false;

            var norm = TextNormalizer.Normalizar(raw);
            if (string.IsNullOrWhiteSpace(norm)) return false;

            if (Keywords.Any(k => norm.Contains(k, StringComparison.OrdinalIgnoreCase)))
                return false;

            var parts = norm.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length < 1 || parts.Length > 5) return false;

            if (parts.Any(p => Stopwords.Contains(p)))
                return false;

            if (parts.Length == 1 && parts[0].Length < 3) return false;
            if (parts.Any(p => p.Length < 2 || p.Length > 25)) return false;
            if (parts.Any(p => p.Any(ch => !char.IsLetter(ch) && ch != '-' && ch != '\''))) return false;

            return true;
        }
    }
}
