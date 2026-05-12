using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using ServicioReintegros.AssistCard.Configuracion;

namespace ServicioReintegros.AssistCard.Infraestructura.Seguridad.Firmas
{
    // Verifica la firma HMAC SHA-256 de Meta (X-Hub-Signature-256)
    public sealed class WhatsAppMetaSignatureVerifier
    {
        private readonly WabaOpciones _waba;
        public WhatsAppMetaSignatureVerifier(IOptions<WabaOpciones> waba) => _waba = waba.Value;

        public void Verificar(IHeaderDictionary headers, string rawBody)
        {
            if (string.IsNullOrWhiteSpace(_waba.AppSecret))
                throw new InvalidOperationException("AppSecret de WABA no configurado");

            if (!headers.TryGetValue("X-Hub-Signature-256", out var firmHeader))
                throw new UnauthorizedAccessException("Firma ausente");

            var headerVal = firmHeader.ToString();
            if (!headerVal.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("Formato de firma inválido");

            var firmaHex = headerVal.Substring("sha256=".Length);

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_waba.AppSecret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody ?? string.Empty));
            var hashHex = Convert.ToHexString(hash).ToLowerInvariant();

            if (!TimeConstantEquals(hashHex, firmaHex.ToLowerInvariant()))
                throw new UnauthorizedAccessException("Firma inválida");
        }

        private static bool TimeConstantEquals(string a, string b)
        {
            if (a.Length != b.Length) return false;
            var result = 0;
            for (int i = 0; i < a.Length; i++)
                result |= a[i] ^ b[i];
            return result == 0;
        }
    }
}
