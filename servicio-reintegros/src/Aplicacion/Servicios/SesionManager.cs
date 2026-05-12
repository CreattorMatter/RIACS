using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ServicioReintegros.AssistCard.Aplicacion.Dtos;
using ServicioReintegros.AssistCard.Aplicacion.Puertos;
using ServicioReintegros.AssistCard.Dominio.Entidades;

namespace ServicioReintegros.AssistCard.Aplicacion.Servicios
{
    /// <summary>
    /// Encapsula todas las operaciones de cache para sesión, historial, reintegros y locale.
    /// Extraído del OrquestadorConversacion para mejorar SRP y testabilidad.
    /// Cada operación es best-effort: si la cache falla, loggea y devuelve default.
    /// </summary>
    public sealed class SesionManager
    {
        private static readonly TimeSpan LocaleTtl = TimeSpan.FromDays(7);
        private static readonly TimeSpan HistorialTtl = TimeSpan.FromDays(2);
        private static readonly TimeSpan SessionTtl = TimeSpan.FromDays(2);
        private static readonly TimeSpan ContextoReintegroTtl = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan DedupeTtl = TimeSpan.FromHours(6);
        private const int HistorialMaxItems = 20;

        private readonly ICacheAplicacion _cache;
        private readonly ILogger<SesionManager> _log;

        public SesionManager(ICacheAplicacion cache, ILogger<SesionManager> log)
        {
            _cache = cache;
            _log = log;
        }

        // --- Sesión ---

        public async Task<SesionConversacion?> ObtenerSesionAsync(string telefono, CancellationToken ct)
        {
            try
            {
                var json = await _cache.ObtenerAsync($"sess:{telefono}", ct);
                if (string.IsNullOrWhiteSpace(json))
                    return null;
                return JsonSerializer.Deserialize<SesionConversacion>(json);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "SesionManager.ObtenerSesion fallo telefono={Telefono}", telefono);
                return null;
            }
        }

        public async Task GuardarSesionAsync(string telefono, SesionConversacion sesion, CancellationToken ct)
        {
            try
            {
                var json = JsonSerializer.Serialize(sesion);
                await _cache.GuardarAsync($"sess:{telefono}", json, SessionTtl, ct);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "SesionManager.GuardarSesion fallo telefono={Telefono}", telefono);
            }
        }

        public async Task ResetSesionAsync(string telefono, CancellationToken ct)
        {
            try
            {
                var jsonSesion = JsonSerializer.Serialize(new SesionConversacion());
                await _cache.GuardarAsync($"sess:{telefono}", jsonSesion, SessionTtl, ct);
                await _cache.GuardarAsync($"reintegro:actual:{telefono}", string.Empty, TimeSpan.FromSeconds(1), ct);
                await _cache.GuardarAsync($"reintegros:lista:{telefono}", string.Empty, TimeSpan.FromSeconds(1), ct);
                await _cache.GuardarAsync($"uploadctx:{telefono}", string.Empty, TimeSpan.FromSeconds(1), ct);
                await _cache.GuardarAsync($"hist:{telefono}", string.Empty, TimeSpan.FromSeconds(1), ct);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "SesionManager.ResetSesion fallo telefono={Telefono}", telefono);
            }
        }

        // --- Historial ---

        public async Task<List<HistorialItem>> ObtenerHistorialAsync(string telefono, CancellationToken ct)
        {
            try
            {
                var json = await _cache.ObtenerAsync($"hist:{telefono}", ct);
                if (string.IsNullOrWhiteSpace(json))
                    return new List<HistorialItem>();

                var list = JsonSerializer.Deserialize<List<HistorialItem>>(json);
                return list ?? new List<HistorialItem>();
            }
            catch (Exception ex)
            {
                _log.LogDebug(ex, "SesionManager.ObtenerHistorial fallo telefono={Telefono}", telefono);
                return new List<HistorialItem>();
            }
        }

        public async Task AgregarHistorialAsync(string telefono, string rol, string texto, CancellationToken ct)
        {
            var historial = await ObtenerHistorialAsync(telefono, ct);
            historial.Add(new HistorialItem
            {
                Rol = rol,
                Texto = texto,
                Ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });

            if (historial.Count > HistorialMaxItems)
                historial = historial.Skip(Math.Max(0, historial.Count - HistorialMaxItems)).ToList();

            var json = JsonSerializer.Serialize(historial);
            await _cache.GuardarAsync($"hist:{telefono}", json, HistorialTtl, ct);
        }

        public async Task AgregarHistorialSiEsPosibleAsync(string telefono, string textoUsuario, string textoRespuesta, CancellationToken ct)
        {
            try
            {
                await AgregarHistorialAsync(telefono, "user", textoUsuario, ct);
                await AgregarHistorialAsync(telefono, "assistant", textoRespuesta, ct);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "SesionManager.AgregarHistorialSiEsPosible fallo telefono={Telefono}", telefono);
            }
        }

        // --- Reintegro actual ---

        public async Task<ResumenReintegro?> ObtenerReintegroActualAsync(string telefono, CancellationToken ct)
        {
            try
            {
                var json = await _cache.ObtenerAsync($"reintegro:actual:{telefono}", ct);
                if (string.IsNullOrWhiteSpace(json))
                    return null;

                var reintegro = JsonSerializer.Deserialize<ResumenReintegro>(json);

                // Refrescar TTL del reintegro para evitar expiración durante navegación (Bug #4)
                if (reintegro != null)
                {
                    try
                    {
                        await _cache.GuardarAsync($"reintegro:actual:{telefono}", json, ContextoReintegroTtl, ct);
                    }
                    catch (Exception ex)
                    {
                        _log.LogDebug(ex, "SesionManager.RefreshTTL reintegro fallo telefono={Telefono}", telefono);
                    }
                }

                return reintegro;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "SesionManager.ObtenerReintegroActual fallo telefono={Telefono}", telefono);
                return null;
            }
        }

        public async Task GuardarReintegroActualAsync(string telefono, ResumenReintegro resumen, CancellationToken ct)
        {
            try
            {
                var json = JsonSerializer.Serialize(resumen);
                await _cache.GuardarAsync($"reintegro:actual:{telefono}", json, ContextoReintegroTtl, ct);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "SesionManager.GuardarReintegroActual fallo telefono={Telefono}", telefono);
            }
        }

        // --- Lista de reintegros ---

        public async Task<List<ResumenReintegro>> ObtenerListaReintegrosAsync(string telefono, CancellationToken ct)
        {
            try
            {
                var json = await _cache.ObtenerAsync($"reintegros:lista:{telefono}", ct);
                if (string.IsNullOrWhiteSpace(json))
                    return new List<ResumenReintegro>();
                var list = JsonSerializer.Deserialize<List<ResumenReintegro>>(json);
                return list ?? new List<ResumenReintegro>();
            }
            catch (Exception ex)
            {
                _log.LogDebug(ex, "SesionManager.ObtenerListaReintegros fallo telefono={Telefono}", telefono);
                return new List<ResumenReintegro>();
            }
        }

        public async Task GuardarListaReintegrosAsync(string telefono, List<ResumenReintegro> lista, CancellationToken ct)
        {
            try
            {
                var json = JsonSerializer.Serialize(lista);
                await _cache.GuardarAsync($"reintegros:lista:{telefono}", json, ContextoReintegroTtl, ct);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "SesionManager.GuardarListaReintegros fallo telefono={Telefono}", telefono);
            }
        }

        // --- Locale ---

        public async Task<string?> ObtenerLocaleAsync(string telefono, CancellationToken ct)
        {
            try
            {
                return await _cache.ObtenerAsync($"locale:{telefono}", ct);
            }
            catch (Exception ex)
            {
                _log.LogDebug(ex, "SesionManager.ObtenerLocale fallo telefono={Telefono}", telefono);
                return null;
            }
        }

        public async Task GuardarLocaleAsync(string telefono, string locale, CancellationToken ct)
        {
            try
            {
                await _cache.GuardarAsync($"locale:{telefono}", locale, LocaleTtl, ct);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "SesionManager.GuardarLocale fallo telefono={Telefono}", telefono);
            }
        }

        // --- Dedup mensajes ---

        public async Task<bool> EsMensajeDuplicadoAsync(string msgId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(msgId)) return false;
            try
            {
                var key = $"wa:msg:{msgId}";
                var existing = await _cache.ObtenerAsync(key, ct);
                if (!string.IsNullOrWhiteSpace(existing))
                    return true;
                await _cache.GuardarAsync(key, "1", DedupeTtl, ct);
                return false;
            }
            catch (Exception ex)
            {
                _log.LogDebug(ex, "SesionManager.EsMensajeDuplicado fallo msgId={MsgId}", msgId);
                return false;
            }
        }
    }
}
