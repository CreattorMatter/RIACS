using System;
using System.Collections.Generic;

namespace ServicioReintegros.AssistCard.Aplicacion.Servicios
{
    /// <summary>
    /// Selecciona una opción de una lista dada el texto del usuario.
    /// Soporta selección por número (1, 2, 3...) o por texto normalizado.
    /// Extraído de OrquestadorConversacion para reutilización y testeo.
    /// </summary>
    public static class OptionSelector
    {
        public static string? ElegirOpcion(string textoUsuario, List<string> opciones)
        {
            if (opciones == null || opciones.Count == 0) return null;
            var raw = (textoUsuario ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(raw)) return null;

            if (int.TryParse(raw, out var n))
            {
                if (n >= 1 && n <= opciones.Count)
                    return opciones[n - 1];
            }

            var norm = TextNormalizer.Normalizar(raw);
            foreach (var opt in opciones)
            {
                if (string.Equals(TextNormalizer.Normalizar(opt), norm, StringComparison.OrdinalIgnoreCase))
                    return opt;
            }

            return null;
        }
    }
}
