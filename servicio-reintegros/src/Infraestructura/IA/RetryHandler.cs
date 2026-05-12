using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ServicioReintegros.AssistCard.Infraestructura.IA
{
    public sealed class RetryHandler : DelegatingHandler
    {
        private const int MaxAttempts = 3;
        private const int BaseDelayMs = 300;
        private const int MaxDelayMs = 5_000;

        private readonly ILogger<RetryHandler> _log;

        public RetryHandler(ILogger<RetryHandler> log)
        {
            _log = log;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            HttpResponseMessage? resp = null;
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    resp = await base.SendAsync(request, ct).ConfigureAwait(false);
                    if (!EsTransitorio(resp.StatusCode) || attempt >= MaxAttempts)
                        return resp;

                    var delay = CalcularDelay(resp, attempt);
                    _log.LogWarning(
                        "HttpRetry. attempt={Attempt} status={Status} delayMs={DelayMs} url={Url}",
                        attempt,
                        (int)resp.StatusCode,
                        delay.TotalMilliseconds,
                        request.RequestUri);

                    resp.Dispose();
                    resp = null;
                    await Task.Delay(delay, ct).ConfigureAwait(false);
                }
                catch (HttpRequestException ex) when (attempt < MaxAttempts)
                {
                    resp?.Dispose();
                    resp = null;
                    var delay = CalcularBackoff(attempt);
                    _log.LogWarning(
                        ex,
                        "HttpRetry network error. attempt={Attempt} delayMs={DelayMs} url={Url}",
                        attempt,
                        delay.TotalMilliseconds,
                        request.RequestUri);
                    await Task.Delay(delay, ct).ConfigureAwait(false);
                }
            }
        }

        private static bool EsTransitorio(HttpStatusCode status)
            => status == HttpStatusCode.TooManyRequests
            || status == HttpStatusCode.RequestTimeout
            || status == HttpStatusCode.BadGateway
            || status == HttpStatusCode.ServiceUnavailable
            || status == HttpStatusCode.GatewayTimeout;

        private static TimeSpan CalcularDelay(HttpResponseMessage resp, int attempt)
        {
            var retryAfter = resp.Headers.RetryAfter;
            if (retryAfter != null)
            {
                if (retryAfter.Delta.HasValue && retryAfter.Delta.Value > TimeSpan.Zero)
                    return Limitar(retryAfter.Delta.Value);
                if (retryAfter.Date.HasValue)
                {
                    var diff = retryAfter.Date.Value - DateTimeOffset.UtcNow;
                    if (diff > TimeSpan.Zero) return Limitar(diff);
                }
            }
            return CalcularBackoff(attempt);
        }

        private static TimeSpan CalcularBackoff(int attempt)
        {
            var baseMs = BaseDelayMs * Math.Pow(2, attempt - 1);
            var jitter = Random.Shared.NextDouble() * 200;
            return Limitar(TimeSpan.FromMilliseconds(baseMs + jitter));
        }

        private static TimeSpan Limitar(TimeSpan delay)
        {
            if (delay.TotalMilliseconds > MaxDelayMs)
                return TimeSpan.FromMilliseconds(MaxDelayMs);
            return delay;
        }
    }
}
