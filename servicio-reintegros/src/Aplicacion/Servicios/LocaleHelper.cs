using System;

namespace ServicioReintegros.AssistCard.Aplicacion.Servicios
{
    /// <summary>
    /// Utilidades centralizadas para resolver idioma y datos dependientes de locale.
    /// Elimina la duplicación de ternarios locale.StartsWith("en")/("pt") por todo el código.
    /// </summary>
    public static class LocaleHelper
    {
        /// <summary>
        /// Normaliza el locale a su código base: "en", "pt" o "es".
        /// </summary>
        public static string ResolveBase(string? locale)
        {
            if (string.IsNullOrWhiteSpace(locale)) return "es";
            var l = locale.Trim().ToLowerInvariant();
            if (l.StartsWith("en")) return "en";
            if (l.StartsWith("pt")) return "pt";
            return "es";
        }

        /// <summary>
        /// Devuelve el email de soporte correcto según el idioma.
        /// PT → reembolso@assistcard.com, resto → reintegros@assistcard.com
        /// </summary>
        public static string EmailSoporte(string? locale)
            => ResolveBase(locale) == "pt" ? "reembolso@assistcard.com" : "reintegros@assistcard.com";

        /// <summary>
        /// Selecciona un valor según el locale (en/pt/es).
        /// Reemplaza el patrón: locale.StartsWith("en") ? x : locale.StartsWith("pt") ? y : z
        /// </summary>
        public static string Loc(string? locale, string es, string en, string pt)
        {
            var b = ResolveBase(locale);
            return b switch
            {
                "en" => en,
                "pt" => pt,
                _ => es
            };
        }

        /// <summary>
        /// Versión con placeholder {0} que se reemplaza con un valor dinámico (ej: nombre, email).
        /// </summary>
        public static string Loc(string? locale, string es, string en, string pt, params object[] args)
        {
            var template = Loc(locale, es, en, pt);
            return args.Length > 0 ? string.Format(template, args) : template;
        }
    }
}
