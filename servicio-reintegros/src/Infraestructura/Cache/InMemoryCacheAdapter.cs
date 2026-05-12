using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using ServicioReintegros.AssistCard.Aplicacion.Puertos;

namespace ServicioReintegros.AssistCard.Infraestructura.Cache
{
    public sealed class InMemoryCacheAdapter : ICacheAplicacion
    {
        private sealed class CacheItem
        {
            public string Valor { get; set; } = string.Empty;
            public DateTimeOffset ExpiraEn { get; set; }
        }

        private readonly ConcurrentDictionary<string, CacheItem> _items = new();
        private readonly ConcurrentDictionary<string, long> _counters = new();

        public Task<string?> ObtenerAsync(string clave, CancellationToken ct = default)
        {
            if (_items.TryGetValue(clave, out var item))
            {
                if (item.ExpiraEn > DateTimeOffset.UtcNow)
                    return Task.FromResult<string?>(item.Valor);

                _items.TryRemove(clave, out _);
            }

            return Task.FromResult<string?>(null);
        }

        public Task GuardarAsync(string clave, string valor, TimeSpan ttl, CancellationToken ct = default)
        {
            var expira = DateTimeOffset.UtcNow.Add(ttl);
            _items[clave] = new CacheItem { Valor = valor, ExpiraEn = expira };
            return Task.CompletedTask;
        }

        public Task<long> IncrementarAsync(string clave, CancellationToken ct = default)
        {
            var nuevo = _counters.AddOrUpdate(clave, 1L, (_, actual) => actual + 1);
            return Task.FromResult(nuevo);
        }

        public Task<TimeSpan?> TtlAsync(string clave, CancellationToken ct = default)
        {
            if (_items.TryGetValue(clave, out var item))
            {
                var ttl = item.ExpiraEn - DateTimeOffset.UtcNow;
                if (ttl < TimeSpan.Zero) ttl = TimeSpan.Zero;
                return Task.FromResult<TimeSpan?>(ttl);
            }

            return Task.FromResult<TimeSpan?>(null);
        }
    }
}
