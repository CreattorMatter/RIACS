using System.Linq;
using System.Text.RegularExpressions;
using ServicioReintegros.AssistCard.Aplicacion.Dtos;

namespace ServicioReintegros.AssistCard.Aplicacion.Servicios
{
    /// <summary>
    /// Parsea texto de entrada del usuario e intenta construir un ConsultaIdentificador.
    /// Extraído del OrquestadorConversacion para ser testeable y reutilizable.
    /// </summary>
    public static class IdentificadorParser
    {
        public static bool TryParse(string texto, out ConsultaIdentificador? consulta)
        {
            consulta = null;
            var t = (texto ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(t))
                return false;

            // BenefitRequestId: numérico (5 a 8 dígitos)
            if (t.All(char.IsDigit))
            {
                if (t.Length >= 5 && t.Length <= 8 && int.TryParse(t, out var brId))
                {
                    consulta = new ConsultaIdentificador { BenefitRequestId = brId };
                    return true;
                }

                return false;
            }

            // Email
            if (Regex.IsMatch(t, @"^[^\s@]+@[^\s@]+\.[^\s@]+$", RegexOptions.IgnoreCase))
            {
                consulta = new ConsultaIdentificador { Email = t };
                return true;
            }

            // CaseId: 7 alfanum (sanitizado)
            var alfanum = new string(t.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
            if (alfanum.Length == 7 && Regex.IsMatch(alfanum, @"^[A-Z0-9]{7}$", RegexOptions.IgnoreCase))
            {
                consulta = new ConsultaIdentificador { CaseId = alfanum };
                return true;
            }

            // Voucher: ISO2 + codigo (permitimos separadores, sin límite máximo — la API decide)
            var voucher = Regex.Replace(t, @"[^A-Za-z0-9]", string.Empty).ToUpperInvariant();
            if (voucher.Length >= 5
                && voucher.Any(char.IsDigit)
                && Regex.IsMatch(voucher, @"^[A-Z]{2}[A-Z0-9]{3,}$"))
            {
                var countryIso = voucher.Substring(0, 2);
                var voucherCode = voucher.Substring(2);
                consulta = new ConsultaIdentificador { CountryIsoCode = countryIso, VoucherCode = voucherCode };
                return true;
            }

            return false;
        }
    }
}
