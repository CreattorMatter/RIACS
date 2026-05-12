using System;
using System.Text.RegularExpressions;

namespace ServicioReintegros.AssistCard.Aplicacion.Servicios
{
    /// <summary>
    /// Detecta comandos globales e intenciones del usuario.
    /// Extraído de OrquestadorConversacion para reutilización y testeo.
    /// </summary>
    public static class CommandDetector
    {
        // ── Comandos globales ──

        public static bool EsComandoMenu(string cmd)
            => cmd == "hola" || cmd == "hello" || cmd == "hi"
            || cmd == "oi" || cmd == "ola"
            || cmd == "menu" || cmd == "inicio"
            || cmd == "start" || cmd == "begin";

        public static bool EsComandoCancelar(string cmd)
            => cmd == "cancelar" || cmd == "cancel";

        public static bool EsComandoAtras(string cmd)
            => cmd == "atras" || cmd == "back" || cmd == "volver"
            || cmd == "voltar" || cmd == "retornar";

        public static bool EsComandoEstado(string cmd)
            => cmd == "estado" || cmd == "status";

        public static bool EsComandoPagos(string cmd)
            => cmd == "pago" || cmd == "pagos" || cmd == "payment" || cmd == "payments";

        // ── Intenciones de negocio ──

        public static bool EsIntencionConsultarReintegro(string texto)
        {
            var t = TextNormalizer.Normalizar(texto);
            if (t == "1") return true;
            return t.Contains("reintegro") || t.Contains("reembolso")
                || t.Contains("reimbursement") || t.Contains("refund");
        }

        public static bool EsReclamoDemoraPago(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return false;
            var t = TextNormalizer.Normalizar(texto);
            if (string.IsNullOrWhiteSpace(t)) return false;

            var mencionaPago = t.Contains("pagan", StringComparison.OrdinalIgnoreCase)
                || t.Contains("paguen", StringComparison.OrdinalIgnoreCase)
                || t.Contains("pago", StringComparison.OrdinalIgnoreCase)
                || t.Contains("pagamento", StringComparison.OrdinalIgnoreCase)
                || t.Contains("receber", StringComparison.OrdinalIgnoreCase)
                || t.Contains("pagar", StringComparison.OrdinalIgnoreCase)
                || t.Contains("reembolso", StringComparison.OrdinalIgnoreCase)
                || t.Contains("reembolsar", StringComparison.OrdinalIgnoreCase)
                || t.Contains("abonan", StringComparison.OrdinalIgnoreCase)
                || t.Contains("abono", StringComparison.OrdinalIgnoreCase)
                || t.Contains("payment", StringComparison.OrdinalIgnoreCase)
                || t.Contains("refund", StringComparison.OrdinalIgnoreCase)
                || t.Contains("reimburse", StringComparison.OrdinalIgnoreCase)
                || t.Contains("paid", StringComparison.OrdinalIgnoreCase)
                || t.Contains("pay", StringComparison.OrdinalIgnoreCase);

            var mencionaDemora = t.Contains("demora", StringComparison.OrdinalIgnoreCase)
                || t.Contains("demorando", StringComparison.OrdinalIgnoreCase)
                || t.Contains("demoro", StringComparison.OrdinalIgnoreCase)
                || t.Contains("tarda", StringComparison.OrdinalIgnoreCase)
                || t.Contains("retras", StringComparison.OrdinalIgnoreCase)
                || t.Contains("mes", StringComparison.OrdinalIgnoreCase)
                || t.Contains("meses", StringComparison.OrdinalIgnoreCase)
                || t.Contains("dia", StringComparison.OrdinalIgnoreCase)
                || t.Contains("dias", StringComparison.OrdinalIgnoreCase)
                || t.Contains("paso", StringComparison.OrdinalIgnoreCase)
                || t.Contains("pasaron", StringComparison.OrdinalIgnoreCase)
                || t.Contains("passaram", StringComparison.OrdinalIgnoreCase)
                || t.Contains("passou", StringComparison.OrdinalIgnoreCase)
                || t.Contains("semanas", StringComparison.OrdinalIgnoreCase)
                || t.Contains("tempo", StringComparison.OrdinalIgnoreCase)
                || t.Contains("still", StringComparison.OrdinalIgnoreCase)
                || t.Contains("waiting", StringComparison.OrdinalIgnoreCase)
                || t.Contains("weeks", StringComparison.OrdinalIgnoreCase)
                || t.Contains("months", StringComparison.OrdinalIgnoreCase)
                || t.Contains("delay", StringComparison.OrdinalIgnoreCase)
                || t.Contains("late", StringComparison.OrdinalIgnoreCase);

            return mencionaPago && mencionaDemora;
        }

        public static bool EsConsultaPlazosPago(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return false;
            var t = TextNormalizer.Normalizar(texto);
            if (string.IsNullOrWhiteSpace(t)) return false;

            return (t.Contains("cuanto tiempo", StringComparison.OrdinalIgnoreCase)
                    || t.Contains("en cuanto tiempo", StringComparison.OrdinalIgnoreCase)
                    || t.Contains("plazo", StringComparison.OrdinalIgnoreCase)
                    || t.Contains("cuando pagan", StringComparison.OrdinalIgnoreCase)
                    || t.Contains("when do you pay", StringComparison.OrdinalIgnoreCase)
                    || t.Contains("how long", StringComparison.OrdinalIgnoreCase))
                && (t.Contains("pagan", StringComparison.OrdinalIgnoreCase)
                    || t.Contains("pago", StringComparison.OrdinalIgnoreCase)
                    || t.Contains("payment", StringComparison.OrdinalIgnoreCase)
                    || t.Contains("pay", StringComparison.OrdinalIgnoreCase));
        }

        public static bool EsConsultaMonedaPago(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return false;
            var t = TextNormalizer.Normalizar(texto);
            if (string.IsNullOrWhiteSpace(t)) return false;

            var mencionaMoneda = t.Contains("moneda", StringComparison.OrdinalIgnoreCase)
                || t.Contains("currency", StringComparison.OrdinalIgnoreCase)
                || t.Contains("moeda", StringComparison.OrdinalIgnoreCase)
                || t.Contains("usd", StringComparison.OrdinalIgnoreCase)
                || t.Contains("dolar", StringComparison.OrdinalIgnoreCase)
                || t.Contains("dollar", StringComparison.OrdinalIgnoreCase);

            var mencionaPago = t.Contains("pagan", StringComparison.OrdinalIgnoreCase)
                || t.Contains("pago", StringComparison.OrdinalIgnoreCase)
                || t.Contains("abonan", StringComparison.OrdinalIgnoreCase)
                || t.Contains("abono", StringComparison.OrdinalIgnoreCase)
                || t.Contains("payment", StringComparison.OrdinalIgnoreCase)
                || t.Contains("pay", StringComparison.OrdinalIgnoreCase)
                || t.Contains("pagam", StringComparison.OrdinalIgnoreCase);

            var mencionaGasto = t.Contains("gaste", StringComparison.OrdinalIgnoreCase)
                || t.Contains("gasto", StringComparison.OrdinalIgnoreCase)
                || t.Contains("spent", StringComparison.OrdinalIgnoreCase)
                || t.Contains("gastei", StringComparison.OrdinalIgnoreCase);

            return mencionaMoneda && (mencionaPago || mencionaGasto);
        }

        public static bool EsConsultaTipoCambio(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return false;
            var t = TextNormalizer.Normalizar(texto);
            if (string.IsNullOrWhiteSpace(t)) return false;

            return t.Contains("tipo de cambio", StringComparison.OrdinalIgnoreCase)
                || t.Contains("tipo cambio", StringComparison.OrdinalIgnoreCase)
                || t.Contains("exchange rate", StringComparison.OrdinalIgnoreCase)
                || t.Contains("cambio oficial", StringComparison.OrdinalIgnoreCase)
                || t.Contains("taxa de cambio", StringComparison.OrdinalIgnoreCase)
                || t.Contains("taxa cambial", StringComparison.OrdinalIgnoreCase)
                || (t.Contains("que cambio", StringComparison.OrdinalIgnoreCase) && t.Contains("usan", StringComparison.OrdinalIgnoreCase))
                || (t.Contains("cual cambio", StringComparison.OrdinalIgnoreCase) && t.Contains("usan", StringComparison.OrdinalIgnoreCase));
        }

        public static bool EsConsultaProblemasApp(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return false;
            var t = TextNormalizer.Normalizar(texto);
            if (string.IsNullOrWhiteSpace(t)) return false;

            var mencionaApp = t.Contains("app", StringComparison.OrdinalIgnoreCase)
                || t.Contains("aplicacion", StringComparison.OrdinalIgnoreCase)
                || t.Contains("aplicacao", StringComparison.OrdinalIgnoreCase)
                || t.Contains("application", StringComparison.OrdinalIgnoreCase)
                || t.Contains("myac", StringComparison.OrdinalIgnoreCase)
                || t.Contains("my assist card", StringComparison.OrdinalIgnoreCase);

            var mencionaProblema = t.Contains("no funciona", StringComparison.OrdinalIgnoreCase)
                || t.Contains("no me funciona", StringComparison.OrdinalIgnoreCase)
                || t.Contains("not working", StringComparison.OrdinalIgnoreCase)
                || t.Contains("doesn't work", StringComparison.OrdinalIgnoreCase)
                || t.Contains("nao funciona", StringComparison.OrdinalIgnoreCase)
                || t.Contains("erro", StringComparison.OrdinalIgnoreCase)
                || t.Contains("error", StringComparison.OrdinalIgnoreCase)
                || t.Contains("cierra", StringComparison.OrdinalIgnoreCase)
                || t.Contains("se cierra", StringComparison.OrdinalIgnoreCase)
                || t.Contains("crash", StringComparison.OrdinalIgnoreCase)
                || t.Contains("problema", StringComparison.OrdinalIgnoreCase)
                || t.Contains("problem", StringComparison.OrdinalIgnoreCase)
                || t.Contains("issue", StringComparison.OrdinalIgnoreCase);

            return mencionaApp && mencionaProblema;
        }

        // ── Locale forzado por texto ──

        public static string? ObtenerLocaleForzado(string texto)
        {
            var t = (texto ?? string.Empty).Trim().ToLowerInvariant();
            if (Regex.IsMatch(t, @"\b(hablo\s+en\s+espa[nñ]ol|espa[nñ]ol|castellano|spanish)\b", RegexOptions.IgnoreCase))
                return "es";
            if (Regex.IsMatch(t, @"\b(hablo\s+en\s+ingles|ingl[eé]s|english)\b", RegexOptions.IgnoreCase))
                return "en";
            if (Regex.IsMatch(t, @"\b(hablo\s+en\s+portug[uú]es|portug[uú]es|portugu[eê]s|portuguese)\b", RegexOptions.IgnoreCase))
                return "pt";
            return null;
        }
    }
}
