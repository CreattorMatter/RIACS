using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure.Identity;
using Microsoft.Azure.StackExchangeRedis;
using ServicioReintegros.AssistCard.Aplicacion.Puertos;
using StackExchange.Redis;

namespace ServicioReintegros.AssistCard.Infraestructura.Cache
{
    public sealed class RedisAdapter : ICacheAplicacion, IAsyncDisposable
    {
        private readonly ConnectionMultiplexer _redis;
        private readonly IDatabase _db;

        private RedisAdapter(ConnectionMultiplexer redis)
        {
            _redis = redis;
            _db = _redis.GetDatabase();
        }

        public static RedisAdapter Crear(string redisUrl)
        {
            var mux = CrearMultiplexerAsync(redisUrl).GetAwaiter().GetResult();
            return new RedisAdapter(mux);
        }

        private static string AgregarPuertoSiFalta(string raw, string puerto)
        {
            var idx = raw.IndexOf(',');
            var endpoint = idx >= 0 ? raw.Substring(0, idx) : raw;
            if (endpoint.Contains(':'))
                return raw;

            var endpointConPuerto = endpoint + ":" + puerto;
            return idx >= 0 ? endpointConPuerto + raw.Substring(idx) : endpointConPuerto;
        }

        private static string QuitarOpcionesAbortConnect(string raw)
        {
            var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length <= 1)
                return raw;

            var kept = new List<string>(parts.Length) { parts[0] };
            for (var i = 1; i < parts.Length; i++)
            {
                var p = parts[i];
                var eq = p.IndexOf('=');
                var key = (eq >= 0 ? p.Substring(0, eq) : p).Trim();

                if (key.Equals("abortConnectFail", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (key.Equals("abortConnect", StringComparison.OrdinalIgnoreCase))
                    continue;

                kept.Add(p);
            }

            return string.Join(',', kept);
        }

        private static async Task<ConnectionMultiplexer> CrearMultiplexerAsync(string redisUrl)
        {
            var raw = (redisUrl ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(raw))
                throw new ArgumentException("REDIS_URL vacío", nameof(redisUrl));

            // Si trae password => modo clásico access key/ACL.
            // Si NO trae password => asumimos autenticación Microsoft Entra (Managed Identity / Service Principal)
            // usando el paquete Microsoft.Azure.StackExchangeRedis (refresh automático del token).
            var idx = raw.IndexOf(',');
            var endpointPart = idx >= 0 ? raw.Substring(0, idx) : raw;
            var host = endpointPart;
            var hostIdx = endpointPart.IndexOf(':');
            if (hostIdx >= 0)
                host = endpointPart.Substring(0, hostIdx);
            host = host.Trim();

            var esAzureManaged = host.EndsWith(".redis.azure.net", StringComparison.OrdinalIgnoreCase);
            var esAzureCache = host.EndsWith(".redis.cache.windows.net", StringComparison.OrdinalIgnoreCase);

            if (raw.Contains("password=", StringComparison.OrdinalIgnoreCase))
            {
                var puertoDefault = esAzureManaged ? "10000" : (esAzureCache ? "6380" : "6379");
                var normalized = AgregarPuertoSiFalta(raw, puertoDefault);
                normalized = QuitarOpcionesAbortConnect(normalized);

                var options1 = ConfigurationOptions.Parse(normalized);
                if (esAzureManaged || esAzureCache)
                    options1.Ssl = true;
                options1.AbortOnConnectFail = false;

                return await ConnectionMultiplexer.ConnectAsync(options1);
            }

            var endpoint = raw;
            if (!endpoint.Contains(':'))
            {
                var defaultPort = esAzureManaged ? "10000" : (esAzureCache ? "6380" : "6379");
                endpoint = endpoint + ":" + defaultPort;
            }

            var options = ConfigurationOptions.Parse(endpoint);
            options.Ssl = true;
            options.AbortOnConnectFail = false;
            options.Protocol = RedisProtocol.Resp3;

            await options.ConfigureForAzureWithTokenCredentialAsync(new DefaultAzureCredential());
            return await ConnectionMultiplexer.ConnectAsync(options);
        }

        public async Task<string?> ObtenerAsync(string clave, CancellationToken ct = default)
        {
            var val = await _db.StringGetAsync(clave);
            return val.HasValue ? val.ToString() : null;
        }

        public Task GuardarAsync(string clave, string valor, TimeSpan ttl, CancellationToken ct = default)
            => _db.StringSetAsync(clave, valor, ttl);

        public async Task<long> IncrementarAsync(string clave, CancellationToken ct = default)
            => await _db.StringIncrementAsync(clave);

        public async Task<TimeSpan?> TtlAsync(string clave, CancellationToken ct = default)
        {
            var ttl = await _db.KeyTimeToLiveAsync(clave);
            return ttl;
        }

        public async ValueTask DisposeAsync()
        {
            await _redis.CloseAsync();
            _redis.Dispose();
        }
    }
}
