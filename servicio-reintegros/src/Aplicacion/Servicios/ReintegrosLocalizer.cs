using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServicioReintegros.AssistCard.Aplicacion.Dtos;
using ServicioReintegros.AssistCard.Aplicacion.Puertos;
using ServicioReintegros.AssistCard.Configuracion;

namespace ServicioReintegros.AssistCard.Aplicacion.Servicios
{
    public interface IReintegrosLocalizer
    {
        Task LocalizarAsync(ResumenReintegro reintegro, string locale, CancellationToken ct);
    }

    public sealed class ReintegrosLocalizer : IReintegrosLocalizer
    {
        private readonly ReintegrosLocalizationOpciones _cfg;
        private readonly IProveedorTraduccion _trad;
        private readonly ICacheAplicacion _cache;
        private readonly ILogger<ReintegrosLocalizer> _log;

        public ReintegrosLocalizer(
            IOptions<ReintegrosLocalizationOpciones> cfg,
            IProveedorTraduccion trad,
            ICacheAplicacion cache,
            ILogger<ReintegrosLocalizer> log)
        {
            _cfg = cfg.Value;
            _trad = trad;
            _cache = cache;
            _log = log;
        }

        public async Task LocalizarAsync(ResumenReintegro reintegro, string locale, CancellationToken ct)
        {
            if (reintegro == null) return;
            if (string.IsNullOrWhiteSpace(locale)) return;

            var target = locale.StartsWith("pt", StringComparison.OrdinalIgnoreCase)
                ? "pt"
                : locale.StartsWith("en", StringComparison.OrdinalIgnoreCase)
                    ? "en"
                    : "es";

            if (string.IsNullOrWhiteSpace(reintegro.StatusOriginal) && !string.IsNullOrWhiteSpace(reintegro.Status))
                reintegro.StatusOriginal = reintegro.Status;

            if (!string.IsNullOrWhiteSpace(reintegro.StatusOriginal))
                reintegro.Status = await LocalizarCampoAsync(reintegro.StatusOriginal, target, "status", _cfg.Status, ct);

            if (string.IsNullOrWhiteSpace(reintegro.BenefitTypeOriginal) && !string.IsNullOrWhiteSpace(reintegro.BenefitType))
                reintegro.BenefitTypeOriginal = reintegro.BenefitType;

            if (!string.IsNullOrWhiteSpace(reintegro.BenefitTypeOriginal))
                reintegro.BenefitType = await LocalizarCampoAsync(reintegro.BenefitTypeOriginal, target, "benefitType", _cfg.BenefitType, ct);

            if (reintegro.PaymentDetails != null && reintegro.PaymentDetails.Count > 0)
            {
                foreach (var p in reintegro.PaymentDetails)
                {
                    if (p == null) continue;

                    if (!string.IsNullOrWhiteSpace(p.Metodo))
                        p.Metodo = await TraducirConCacheAsync(p.Metodo, target, "paymentMethod", ct);

                    if (!string.IsNullOrWhiteSpace(p.Estado))
                        p.Estado = await TraducirConCacheAsync(p.Estado, target, "paymentStatus", ct);

                    if (!string.IsNullOrWhiteSpace(p.MonedaOriginal))
                        p.MonedaOriginal = await TraducirConCacheAsync(p.MonedaOriginal, target, "currency", ct);

                    if (!string.IsNullOrWhiteSpace(p.Detalle))
                        p.Detalle = await TraducirConCacheAsync(p.Detalle, target, "paymentDetail", ct);
                }
            }

            if (reintegro.PendingDocuments != null && reintegro.PendingDocuments.Count > 0)
            {
                foreach (var d in reintegro.PendingDocuments)
                {
                    if (d == null) continue;

                    if (!string.IsNullOrWhiteSpace(d.DocumentType))
                        d.DocumentType = await TraducirConCacheAsync(d.DocumentType, target, "pendingDocType", ct);

                    if (!string.IsNullOrWhiteSpace(d.Description))
                        d.Description = await TraducirConCacheAsync(d.Description, target, "pendingDocDescription", ct);
                }
            }

            if (reintegro.AwaitResult != null && reintegro.AwaitResult.Count > 0)
            {
                foreach (var it in reintegro.AwaitResult)
                {
                    if (it == null) continue;

                    if (!string.IsNullOrWhiteSpace(it.Concept))
                        it.Concept = await TraducirConCacheAsync(it.Concept, target, "deniedItemConcept", ct);

                    if (it.DenialReason == null || it.DenialReason.Count == 0) continue;

                    for (var i = 0; i < it.DenialReason.Count; i++)
                    {
                        var rr = it.DenialReason[i];
                        if (string.IsNullOrWhiteSpace(rr)) continue;
                        it.DenialReason[i] = await TraducirConCacheAsync(rr, target, "denialReason", ct);
                    }
                }
            }
        }

        private async Task<string> LocalizarCampoAsync(
            string original,
            string target,
            string campo,
            Dictionary<string, Dictionary<string, string>> catalogo,
            CancellationToken ct)
        {
            var key = NormalizarKey(original);

            if (catalogo != null && catalogo.Count > 0)
            {
                if (catalogo.TryGetValue(key, out var perLocale) && perLocale != null && perLocale.TryGetValue(target, out var mapped) && !string.IsNullOrWhiteSpace(mapped))
                {
                    _log.LogInformation("Localizar.{Campo}.MapHit target={Target} original={Original} mapped={Mapped}", campo, target, original, mapped);
                    return mapped;
                }

                _log.LogInformation("Localizar.{Campo}.MapMiss target={Target} original={Original}", campo, target, original);
            }

            return await TraducirConCacheAsync(original, target, campo, ct);
        }

        private async Task<string> TraducirConCacheAsync(string texto, string destino, string campo, CancellationToken ct)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(texto)) return texto;
                if (string.IsNullOrWhiteSpace(destino)) return texto;

                var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(texto)));
                var key = $"tr:{destino}:{campo}:{hash}";
                var cached = await _cache.ObtenerAsync(key, ct);
                if (!string.IsNullOrWhiteSpace(cached))
                {
                    _log.LogInformation("Localizar.Translation.CacheHit campo={Campo} destino={Destino} original={Original}", campo, destino, texto);
                    return cached;
                }

                _log.LogInformation("Localizar.Translation.CacheMiss campo={Campo} destino={Destino} original={Original}", campo, destino, texto);

                var traducido = await _trad.TraducirAsync(texto, destino, ct);
                if (string.IsNullOrWhiteSpace(traducido))
                    return texto;

                _log.LogInformation("Localizar.Translation.Result campo={Campo} destino={Destino} translated={Translated}", campo, destino, traducido);

                await _cache.GuardarAsync(key, traducido, TimeSpan.FromDays(30), ct);
                return traducido;
            }
            catch
            {
                return texto;
            }
        }

        private static string NormalizarKey(string input)
        {
            var s = (input ?? string.Empty).Trim().ToLowerInvariant();
            return s;
        }
    }
}
