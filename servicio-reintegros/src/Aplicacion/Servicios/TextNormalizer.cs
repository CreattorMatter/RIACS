using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ServicioReintegros.AssistCard.Aplicacion.Servicios
{
    /// <summary>
    /// Normaliza texto: quita acentos, colapsa espacios, lowercase.
    /// Extraído de OrquestadorConversacion para reutilización y testeo.
    /// </summary>
    public static class TextNormalizer
    {
        public static string Normalizar(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return string.Empty;

            var formD = texto.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(formD.Length);
            foreach (var ch in formD)
            {
                var uc = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (uc != UnicodeCategory.NonSpacingMark)
                    sb.Append(ch);
            }

            var sinAcentos = sb.ToString().Normalize(NormalizationForm.FormC);
            sinAcentos = Regex.Replace(sinAcentos, @"\s+", " ");
            return sinAcentos.Trim();
        }

        public static bool EsTextoAmbiguoParaDeteccion(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return true;
            var t = texto.Trim();
            if (t.All(char.IsDigit)) return true;
            if (t.Length <= 3 && t.All(c => !char.IsLetter(c))) return true;
            return false;
        }

        public static string MapearIdioma(string locale)
        {
            if (string.IsNullOrWhiteSpace(locale)) return "es-ES";
            var lower = locale.ToLowerInvariant();
            if (lower.StartsWith("en")) return "en-US";
            if (lower.StartsWith("pt")) return "pt-BR";
            return "es-ES";
        }

        public static string DerivarConversationIdDesdeIdentificador(string? telefono)
        {
            var t = (telefono ?? string.Empty).Trim();
            if (t.StartsWith("samu:", System.StringComparison.OrdinalIgnoreCase))
            {
                var rest = t.Substring("samu:".Length);
                if (rest.StartsWith("phone:", System.StringComparison.OrdinalIgnoreCase))
                    return string.Empty;
                if (rest.StartsWith("anon:", System.StringComparison.OrdinalIgnoreCase))
                    return string.Empty;
                return rest;
            }
            return string.Empty;
        }
    }
}
