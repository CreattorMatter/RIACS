using System;
using System.Text.RegularExpressions;

namespace ServicioReintegros.AssistCard.Aplicacion.Servicios
{
    public enum GuardrailPolicy
    {
        Default,
        NoAceleracion,
        ProblemasApp
    }

    public sealed record GuardrailResult(string Mensaje, bool UsedFallback, string? Reason);

    /// <summary>
    /// Servicio centralizado de guardrails para sanitizar respuestas de IA.
    /// Reemplaza frases genéricas de "contacta al soporte" por el email correcto,
    /// y corrige emails cruzados entre locales.
    /// Extraído de FoundryAgentAdapter y OrquestadorConversacion para reutilización.
    /// </summary>
    public static class GuardrailService
    {
        private static readonly string[] PatronesSoporte = new[]
        {
            @"contacta(?:r)?(?:\s+a)?\s+(?:nuestro\s+)?soporte",
            @"contact(?:a|ar|e)?\s+(?:al|a|o)\s+soporte",
            @"(?:puedes|puede|podés)\s+contactar\s+(?:a\s+)?(?:nuestro\s+)?soporte",
            @"contact\s+(?:our\s+)?(?:customer\s+)?support(?:\s+team)?",
            @"(?:you\s+can\s+)?(?:reach\s+out\s+to|contact)\s+(?:our\s+)?support",
            @"(?:entre\s+em\s+)?contato?\s+(?:com\s+)?(?:o\s+)?(?:nosso\s+)?suporte",
            @"contacte?\s+(?:o\s+)?suporte"
        };

        private static readonly string[] FrasesProhibidas = new[]
        {
            "soporte tecnico",
            "technical support",
            "suporte tecnico",
            "recibiremos",
            "receberemos",
            "we will receive",
            "por este medio",
            "por este canal"
        };

        /// <summary>
        /// Sanitiza una respuesta de IA reemplazando frases genéricas de soporte por el email correcto.
        /// También corrige emails cruzados entre locales (reembolso@ en es/en, reintegros@ en pt).
        /// </summary>
        public static string SanitizarReferenciasSoporte(string mensaje, string? locale)
        {
            if (string.IsNullOrWhiteSpace(mensaje)) return mensaje;

            var email = LocaleHelper.EmailSoporte(locale);

            var result = mensaje;
            foreach (var patron in PatronesSoporte)
            {
                result = Regex.Replace(result, patron, email, RegexOptions.IgnoreCase);
            }

            // Corregir emails cruzados entre locales
            if (LocaleHelper.ResolveBase(locale) != "pt")
            {
                result = Regex.Replace(result, @"reembolso@assistcard\.com", "reintegros@assistcard.com", RegexOptions.IgnoreCase);
            }
            else
            {
                result = Regex.Replace(result, @"reintegros@assistcard\.com", "reembolso@assistcard.com", RegexOptions.IgnoreCase);
            }

            return result;
        }

        /// <summary>
        /// Verifica si un texto contiene alguna frase prohibida por las reglas de negocio.
        /// </summary>
        public static bool ContieneFraseProhibida(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return false;
            var norm = texto.ToLowerInvariant();
            foreach (var frase in FrasesProhibidas)
            {
                if (norm.Contains(frase, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Asegura que el email de soporte esté presente en la respuesta.
        /// Si no lo está, lo agrega al final con un mensaje adecuado al idioma.
        /// </summary>
        public static string AsegurarEmailEnRespuesta(string mensaje, string? locale)
        {
            if (string.IsNullOrWhiteSpace(mensaje)) return mensaje;

            var email = LocaleHelper.EmailSoporte(locale);
            if (mensaje.Contains(email, System.StringComparison.OrdinalIgnoreCase))
                return mensaje;

            return mensaje + LocaleHelper.Loc(locale,
                $" Si necesitás más ayuda, escribinos a {email}.",
                $" If you need further assistance, please contact us at {email}.",
                $" Se precisar de mais ajuda, entre em contato pelo email {email}.");
        }

        /// <summary>
        /// Pipeline completo de guardrails post-IA.
        /// 1) Sanitiza referencias a soporte. 2) Detecta frases prohibidas.
        /// 3) Detecta promesas de aceleración (si policy lo requiere).
        /// 4) Asegura email presente.
        /// Si algún guardrail falla, reemplaza por fallbackMessage.
        /// </summary>
        public static GuardrailResult AplicarGuardrails(
            string mensajeIa,
            string locale,
            string? fallbackMessage,
            GuardrailPolicy policy = GuardrailPolicy.Default)
        {
            if (string.IsNullOrWhiteSpace(mensajeIa))
                return new GuardrailResult(fallbackMessage ?? BotMessages.RespuestaIaVacia(locale), true, "empty_ia_response");

            var resultado = SanitizarReferenciasSoporte(mensajeIa, locale);

            if (ContieneFraseProhibida(resultado))
            {
                if (!string.IsNullOrWhiteSpace(fallbackMessage))
                    return new GuardrailResult(fallbackMessage, true, "frase_prohibida");
                resultado = SanitizarFrasesProhibidas(resultado);
            }

            if (policy == GuardrailPolicy.NoAceleracion && ContienePromesaAceleracion(resultado))
            {
                if (!string.IsNullOrWhiteSpace(fallbackMessage))
                    return new GuardrailResult(fallbackMessage, true, "promesa_aceleracion");
            }

            resultado = AsegurarEmailEnRespuesta(resultado, locale);

            return new GuardrailResult(resultado, false, null);
        }

        /// <summary>
        /// Detecta si el texto contiene promesas de acelerar el proceso de pago/reintegro.
        /// </summary>
        public static bool ContienePromesaAceleracion(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return false;
            var norm = texto.ToLowerInvariant();
            return norm.Contains("aceler", StringComparison.OrdinalIgnoreCase)
                || norm.Contains("acceler", StringComparison.OrdinalIgnoreCase)
                || norm.Contains("apress", StringComparison.OrdinalIgnoreCase);
        }

        private static string SanitizarFrasesProhibidas(string texto)
        {
            var result = texto;
            foreach (var frase in FrasesProhibidas)
            {
                result = Regex.Replace(result, Regex.Escape(frase), string.Empty, RegexOptions.IgnoreCase);
            }
            return Regex.Replace(result, @"\s{2,}", " ").Trim();
        }
    }
}
