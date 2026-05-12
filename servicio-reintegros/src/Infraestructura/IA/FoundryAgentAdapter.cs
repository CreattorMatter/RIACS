using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServicioReintegros.AssistCard.Aplicacion.Dtos;
using ServicioReintegros.AssistCard.Aplicacion.Puertos;
using ServicioReintegros.AssistCard.Aplicacion.Servicios;
using ServicioReintegros.AssistCard.Configuracion;

namespace ServicioReintegros.AssistCard.Infraestructura.IA
{
    public sealed class FoundryAgentAdapter : IProveedorIA
    {
        private const string SearchKnowledgeApiVersion = "2025-11-01-preview";
        private const string SearchDocsApiVersion = "2025-09-01";
        private const string SearchTokenScope = "https://search.azure.com/.default";
        private const string FoundryTokenScope = "https://ai.azure.com/.default";
        private const int MaxGroundingDocuments = 3;
        private const int MaxCharsPerGroundingDocument = 1800;
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = false
        };

        private static readonly HashSet<string> DocumentalIntents = new(StringComparer.OrdinalIgnoreCase)
        {
            "RequisitosYPasosIniciarReintegro",
            "ProblemasApp",
            "ProblemasAppMyAC",
            "ReclamoDemoraPago",
            "PlazosPagoPendiente",
            "MonedaPagoPendiente",
            "TipoCambioPagoPendiente",
            "ConsultaFAQ",
            "Faq",
            "ExplicarEstado",
            "ExplicarProximosPasos"
        };

        private readonly HttpClient _http;
        private readonly FoundryOpciones _cfg;
        private readonly SearchOpciones _searchCfg;
        private readonly ILogger<FoundryAgentAdapter> _log;
        private readonly TokenCredential _credential;
        private readonly ICacheAplicacion? _cache;
        private readonly ConcurrentDictionary<string, Lazy<Task<AgentResponse>>> _enFlight = new();
        private int _loggedModelFallback;
        private string? _resolvedIndexName;

        [ActivatorUtilitiesConstructor]
        public FoundryAgentAdapter(
            HttpClient http,
            IOptions<FoundryOpciones> cfg,
            IOptions<SearchOpciones> searchCfg,
            ILogger<FoundryAgentAdapter> log,
            ICacheAplicacion? cache = null)
            : this(http, cfg, searchCfg, log, new DefaultAzureCredential(), cache)
        {
        }

        public FoundryAgentAdapter(
            HttpClient http,
            IOptions<FoundryOpciones> cfg,
            IOptions<SearchOpciones> searchCfg,
            ILogger<FoundryAgentAdapter> log,
            TokenCredential credential,
            ICacheAplicacion? cache = null)
        {
            _http = http;
            _cfg = cfg.Value;
            _searchCfg = searchCfg.Value;
            _log = log;
            _credential = credential ?? throw new ArgumentNullException(nameof(credential));
            _cache = cache;
        }

        public async Task<AgentResponse> EnviarAsync(string userId, string locale, string textoEntrada, IaRequestContext? contexto = null, CancellationToken ct = default)
        {
            var localeNorm = string.IsNullOrWhiteSpace(locale) ? "es" : locale;
            var cacheKey = ConstruirClaveCache(textoEntrada, contexto, localeNorm);

            var hit = await ObtenerDeCacheAsync(cacheKey, ct).ConfigureAwait(false);
            if (hit != null)
            {
                if (_log.IsEnabled(LogLevel.Information))
                    _log.LogInformation("IaCacheHit. intent={Intent} locale={Locale} key={Key}", contexto?.Intent ?? "<empty>", localeNorm, cacheKey);
                return hit;
            }

            var lazy = _enFlight.GetOrAdd(cacheKey, k => new Lazy<Task<AgentResponse>>(
                () => EjecutarConLimpiezaAsync(k, () => EnviarRealAsync(textoEntrada, contexto, localeNorm, k, ct))));

            try
            {
                return await lazy.Value.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error invocando runtime de IA. intent={Intent} locale={Locale}", contexto?.Intent ?? "<empty>", localeNorm);
                return new AgentResponse
                {
                    IsValid = false,
                    Error = ex.Message,
                    Message = MensajeFallback(localeNorm)
                };
            }
        }

        private async Task<AgentResponse> EjecutarConLimpiezaAsync(string cacheKey, Func<Task<AgentResponse>> ejecutor)
        {
            try
            {
                return await ejecutor().ConfigureAwait(false);
            }
            finally
            {
                _enFlight.TryRemove(cacheKey, out _);
            }
        }

        private async Task<AgentResponse> EnviarRealAsync(string textoEntrada, IaRequestContext? contexto, string locale, string cacheKey, CancellationToken ct)
        {
            AgentResponse generado;
            if (DebeUsarKnowledgeBase(contexto))
                generado = await EjecutarRagConIndiceAsync(textoEntrada, contexto, locale, ct).ConfigureAwait(false);
            else
                generado = await EjecutarFoundryDirectoAsync(textoEntrada, contexto, locale, ct).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(generado.Message))
            {
                _log.LogWarning(
                    "IaRespuestaVacia. intent={Intent} locale={Locale} error={Error} -> fallback localizado",
                    contexto?.Intent ?? "<empty>",
                    locale,
                    generado.Error ?? "<empty>");
                generado.Message = MensajeFallback(locale);
                return generado;
            }

            generado.Message = SanitizarReferenciasSoporte(generado.Message, locale);
            await GuardarEnCacheAsync(cacheKey, generado, ObtenerTtlCache(contexto), ct).ConfigureAwait(false);
            return generado;
        }

        private async Task<AgentResponse> EjecutarRagConIndiceAsync(string prompt, IaRequestContext? contexto, string locale, CancellationToken ct)
        {
            var searchResult = await BuscarEnIndiceAsync(prompt, contexto, ct).ConfigureAwait(false);
            string promptFinal;
            if (searchResult.Success && !string.IsNullOrWhiteSpace(searchResult.Message))
            {
                promptFinal = ConstruirPromptGroundedDesdeIndice(prompt, contexto, locale, searchResult.Message!);
            }
            else
            {
                promptFinal = ConstruirPromptSinGrounding(prompt, contexto, locale);
                if (_log.IsEnabled(LogLevel.Information))
                {
                    _log.LogInformation(
                        "RagIndexSinHits. intent={Intent} locale={Locale} indexError={IndexError} -> prompt sin grounding",
                        contexto?.Intent ?? "<empty>",
                        locale,
                        searchResult.Error ?? "<empty>");
                }
            }

            var synthesis = await ConsultarFoundryResponsesAsync(promptFinal, contexto, locale, ct).ConfigureAwait(false);
            if (!synthesis.Success)
            {
                return new AgentResponse
                {
                    IsValid = false,
                    Error = synthesis.Error ?? "FoundryResponsesFailed"
                };
            }

            var parsed = new AgentResponse();
            var body = ExtraerTextoDesdeJson(synthesis.RawBody) ?? synthesis.RawBody ?? string.Empty;
            ParsearAgentResponse(parsed, body, locale);
            return parsed;
        }

        private async Task<AgentResponse> EjecutarFoundryDirectoAsync(string textoEntrada, IaRequestContext? contexto, string locale, CancellationToken ct)
        {
            var responseResult = await ConsultarFoundryResponsesAsync(textoEntrada, contexto, locale, ct).ConfigureAwait(false);
            var result = new AgentResponse();
            if (!responseResult.Success)
            {
                result.IsValid = false;
                result.Error = responseResult.Error ?? "FoundryResponsesFailed";
                return result;
            }

            var body = ExtraerTextoDesdeJson(responseResult.RawBody) ?? responseResult.RawBody ?? string.Empty;
            ParsearAgentResponse(result, body, locale);
            return result;
        }

        public async Task<string> ExplicarEstadoAsync(string status, string benefitType, string locale, CancellationToken ct = default)
        {
            var prompt = locale.StartsWith("en", StringComparison.OrdinalIgnoreCase)
                ? $"The reimbursement status is: {status}. Benefit type: {benefitType}. What does it mean and what should the customer do next? Return only a JSON object with a 'message' property."
                : locale.StartsWith("pt", StringComparison.OrdinalIgnoreCase)
                    ? $"O status do reembolso é: {status}. Tipo de benefício: {benefitType}. O que significa e o que o cliente deve fazer a seguir? Retorne apenas um objeto JSON com uma propriedade 'message'."
                    : $"El estado del reintegro presentado se encuentra en: {status}. Tipo de beneficio: {benefitType}. ¿Qué significa y cómo debería seguir el cliente? Devolvé solo un objeto JSON con una propiedad 'message'.";

            var ctx = new IaRequestContext
            {
                Intent = "ExplicarEstado",
                Status = status,
                BenefitType = benefitType,
                Locale = locale
            };

            var resp = await EnviarAsync("system", locale, prompt, ctx, ct);
            if (!string.IsNullOrWhiteSpace(resp.Message))
                return resp.Message;

            if (_log.IsEnabled(LogLevel.Information))
                _log.LogInformation("ExplicarEstado fallback. status={Status} benefitType={BenefitType} locale={Locale} iaError={Error}", status, benefitType, locale, resp.Error ?? "<empty>");

            return FallbackExplicarEstado(status, benefitType, locale);
        }

        public async Task<string> ExplicarProximosPasosAsync(string status, string locale, CancellationToken ct = default)
        {
            var prompt = locale.StartsWith("en", StringComparison.OrdinalIgnoreCase)
                ? $"Based on this reimbursement status: {status}, what are the next steps for the customer? Keep it short and actionable. Return only a JSON object with a 'message' property."
                : locale.StartsWith("pt", StringComparison.OrdinalIgnoreCase)
                    ? $"Com base neste status do reembolso: {status}, quais são os próximos passos para o cliente? Seja curto e objetivo. Retorne apenas um objeto JSON com uma propriedade 'message'."
                    : $"Según el estado del reintegro: {status}, ¿cuáles son los próximos pasos para el cliente? Sé breve y accionable. Devolvé solo un objeto JSON con una propiedad 'message'.";

            var ctx = new IaRequestContext
            {
                Intent = "ExplicarProximosPasos",
                Status = status,
                Locale = locale
            };

            var resp = await EnviarAsync("system", locale, prompt, ctx, ct);
            if (!string.IsNullOrWhiteSpace(resp.Message))
                return resp.Message;

            if (_log.IsEnabled(LogLevel.Information))
                _log.LogInformation("ExplicarProximosPasos fallback. status={Status} locale={Locale} iaError={Error}", status, locale, resp.Error ?? "<empty>");

            return FallbackProximosPasos(status, locale);
        }

        public async Task<string> ExplicarPagoAsync(object detalles, string locale, CancellationToken ct = default)
        {
            var detallesJson = JsonSerializer.Serialize(detalles, JsonOptions);
            var prompt = locale.StartsWith("en", StringComparison.OrdinalIgnoreCase)
                ? $"My reimbursement has the following payment details: {detallesJson}. Explain it simply and mention relevant dates/currency. Return only a JSON object with a 'message' property."
                : locale.StartsWith("pt", StringComparison.OrdinalIgnoreCase)
                    ? $"Meu reembolso tem os seguintes detalhes de pagamento: {detallesJson}. Explique de forma simples, incluindo datas e moeda quando disponíveis. Retorne apenas um objeto JSON com uma propriedade 'message'."
                    : $"Mi reintegro tiene el siguiente detalle de pago: {detallesJson}. Explicalo de forma simple, incluyendo fechas y moneda si aparece. Devolvé solo un objeto JSON con una propiedad 'message'.";

            var ctx = new IaRequestContext
            {
                Intent = "ExplicarPago",
                PaymentDetails = detalles,
                Locale = locale
            };

            var resp = await EnviarAsync("system", locale, prompt, ctx, ct);
            return string.IsNullOrWhiteSpace(resp.Message) ? MensajeFallback(locale) : resp.Message;
        }

        public async Task<string> ResponderFaqAsync(string pregunta, string locale, CancellationToken ct = default)
        {
            var prompt = locale.StartsWith("en", StringComparison.OrdinalIgnoreCase)
                ? $"Answer the user's question using the configured knowledge base. If there is an official answer in the knowledge base, reproduce it faithfully. Return the response as a JSON object with a 'message' property. Question: {pregunta}."
                : locale.StartsWith("pt", StringComparison.OrdinalIgnoreCase)
                    ? $"Responda à pergunta do usuário usando a base de conhecimento configurada. Se existir uma resposta oficial na base de conhecimento, reproduza-a fielmente. Retorne a resposta como um objeto JSON com uma propriedade 'message'. Pergunta: {pregunta}."
                    : $"Respondé usando la base de conocimiento configurada. Si existe una respuesta oficial en la base, reproducila fielmente. Devolvé la respuesta como un objeto JSON con una propiedad 'message'. Pregunta: {pregunta}.";

            var ctx = new IaRequestContext
            {
                Intent = "ConsultaFAQ",
                Question = pregunta,
                Locale = locale
            };

            var resp = await EnviarAsync("system", locale, prompt, ctx, ct);
            return string.IsNullOrWhiteSpace(resp.Message) ? MensajeFallback(locale) : resp.Message;
        }

        private bool DebeUsarKnowledgeBase(IaRequestContext? contexto)
            => contexto != null
                && !string.IsNullOrWhiteSpace(contexto.Intent)
                && DocumentalIntents.Contains(contexto.Intent);

        private static string NormalizarParaComparacion(string text)
        {
            var normalized = text.Trim().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(normalized.Length);
            foreach (var ch in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                    sb.Append(char.ToLowerInvariant(ch));
            }

            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        private async Task<KnowledgeRetrieveResult> BuscarEnIndiceAsync(string prompt, IaRequestContext? contexto, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(_searchCfg.Endpoint))
            {
                _log.LogWarning(
                    "SearchIndexError. Missing SEARCH_* configuration. endpointConfigured={EndpointConfigured} SearchAuthMode={SearchAuthMode}",
                    !string.IsNullOrWhiteSpace(_searchCfg.Endpoint),
                    ObtenerSearchAuthMode());
                return new KnowledgeRetrieveResult { Error = "SearchIndexNotConfigured" };
            }

            var indexName = await ResolverSearchIndexNameAsync(ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(indexName))
                return new KnowledgeRetrieveResult { Error = "SearchIndexNameNotResolved" };

            var query = ConstruirPreguntaRetrieve(prompt, contexto);
            var payload = new
            {
                search = query,
                top = MaxGroundingDocuments,
                queryType = "simple",
                searchMode = "any",
                searchFields = "content",
                select = "content,metadata_storage_name,metadata_storage_path"
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, ConstruirSearchDocsUri(indexName))
            {
                Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json")
            };

            _log.LogInformation(
                "SearchIndexStart. endpoint={Endpoint} indexName={IndexName} intent={Intent} SearchAuthMode={SearchAuthMode}",
                _searchCfg.Endpoint,
                indexName,
                contexto?.Intent ?? "<empty>",
                ObtenerSearchAuthMode());

            if (!await IntentarAplicarAutenticacionSearchAsync(request, "SearchIndex", ct).ConfigureAwait(false))
                return new KnowledgeRetrieveResult { Error = "SearchAuthFailed" };

            using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
            var rawBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var statusCode = response.StatusCode;
            var hits = ExtraerHitsDeBusqueda(rawBody);

            if (statusCode == HttpStatusCode.PartialContent)
            {
                var groundedContent = ConstruirGroundingDesdeHits(hits);
                _log.LogWarning(
                    "SearchIndexPartial. endpoint={Endpoint} indexName={IndexName} hitCount={HitCount} bodySample={BodySample}",
                    _searchCfg.Endpoint,
                    indexName,
                    hits.Count,
                    Truncar(rawBody));
                return new KnowledgeRetrieveResult
                {
                    Success = !string.IsNullOrWhiteSpace(groundedContent),
                    PartialContent = true,
                    Message = groundedContent,
                    Error = string.IsNullOrWhiteSpace(groundedContent) ? "SearchIndexPartialEmpty" : null,
                    RawBody = rawBody,
                    StatusCode = statusCode
                };
            }

            if (!response.IsSuccessStatusCode)
            {
                var err = ExtraerMensajeError(rawBody) ?? $"SearchIndexHttp{(int)statusCode}";
                _log.LogWarning(
                    "SearchIndexError. statusCode={StatusCode} endpoint={Endpoint} indexName={IndexName} error={Error} bodySample={BodySample}",
                    (int)statusCode,
                    _searchCfg.Endpoint,
                    indexName,
                    err,
                    Truncar(rawBody));
                return new KnowledgeRetrieveResult { Error = err, RawBody = rawBody, StatusCode = statusCode };
            }

            var content = ConstruirGroundingDesdeHits(hits);
            var docNames = string.Join(" | ", hits
                .Take(MaxGroundingDocuments)
                .Select(h => string.IsNullOrWhiteSpace(h.Title) ? "<sin-nombre>" : h.Title!));
            _log.LogInformation(
                "SearchIndexSuccess. statusCode={StatusCode} endpoint={Endpoint} indexName={IndexName} intent={Intent} query={Query} hitCount={HitCount} docs={Docs} groundedLength={GroundedLength}",
                (int)statusCode,
                _searchCfg.Endpoint,
                indexName,
                contexto?.Intent ?? "<empty>",
                Truncar(query, 180),
                hits.Count,
                docNames,
                content?.Length ?? 0);

            return new KnowledgeRetrieveResult
            {
                Success = !string.IsNullOrWhiteSpace(content),
                Message = content,
                Error = string.IsNullOrWhiteSpace(content) ? "SearchIndexEmpty" : null,
                RawBody = rawBody,
                StatusCode = statusCode
            };
        }

        private async Task<FoundryResponseResult> ConsultarFoundryResponsesAsync(string prompt, IaRequestContext? contexto, string locale, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(_cfg.Endpoint))
            {
                _log.LogWarning("FoundryResponsesError. FOUNDRY_ENDPOINT no configurado.");
                return new FoundryResponseResult { Error = "FoundryEndpointNotConfigured" };
            }

            var modelDeployment = ObtenerFoundryModelDeployment();
            if (string.IsNullOrWhiteSpace(modelDeployment))
            {
                _log.LogWarning("FoundryResponsesError. FOUNDRY_MODEL_DEPLOYMENT/FOUNDRY_DEPLOYMENT no configurados.");
                return new FoundryResponseResult { Error = "FoundryModelNotConfigured" };
            }

            var inputText = ConstruirInputFoundry(prompt, contexto, locale);
            var payload = new
            {
                model = modelDeployment,
                input = inputText
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, ConstruirFoundryResponsesUri())
            {
                Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json")
            };

            await AplicarAutenticacionFoundryAsync(request, ct).ConfigureAwait(false);

            if (_log.IsEnabled(LogLevel.Information))
            {
                _log.LogInformation(
                    "FoundryResponsesStart. endpoint={Endpoint} model={Model} intent={Intent}",
                    _cfg.Endpoint,
                    modelDeployment,
                    contexto?.Intent ?? "<empty>");
            }

            using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
            var rawBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var err = ExtraerMensajeError(rawBody) ?? $"FoundryResponsesHttp{(int)response.StatusCode}";
                _log.LogWarning(
                    "FoundryResponsesError. statusCode={StatusCode} endpoint={Endpoint} model={Model} error={Error} bodySample={BodySample}",
                    (int)response.StatusCode,
                    _cfg.Endpoint,
                    modelDeployment,
                    err,
                    Truncar(rawBody));
                return new FoundryResponseResult { Error = err, RawBody = rawBody, StatusCode = response.StatusCode };
            }

            _log.LogInformation(
                "FoundryResponsesSuccess. statusCode={StatusCode} endpoint={Endpoint} model={Model} textLength={TextLength}",
                (int)response.StatusCode,
                _cfg.Endpoint,
                modelDeployment,
                ExtraerTextoDesdeJson(rawBody)?.Length ?? 0);

            return new FoundryResponseResult
            {
                Success = true,
                RawBody = rawBody,
                Message = ExtraerTextoDesdeJson(rawBody),
                StatusCode = response.StatusCode
            };
        }

        private Uri ConstruirSearchDocsUri(string indexName)
        {
            var endpoint = (_searchCfg.Endpoint ?? string.Empty).Trim().TrimEnd('/');
            var safeIndexName = Uri.EscapeDataString(indexName.Trim());
            return new Uri($"{endpoint}/indexes/{safeIndexName}/docs/search?api-version={SearchDocsApiVersion}");
        }

        private Uri ConstruirKnowledgeSourcesUri()
        {
            var endpoint = (_searchCfg.Endpoint ?? string.Empty).Trim().TrimEnd('/');
            return new Uri($"{endpoint}/knowledgesources?api-version={SearchKnowledgeApiVersion}");
        }

        private Uri ConstruirFoundryResponsesUri()
        {
            var endpoint = (_cfg.Endpoint ?? string.Empty).Trim().TrimEnd('/');
            return new Uri($"{endpoint}/openai/v1/responses");
        }

        private async Task AplicarAutenticacionFoundryAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (!string.IsNullOrWhiteSpace(_cfg.ApiKey))
            {
                request.Headers.TryAddWithoutValidation("api-key", _cfg.ApiKey);
                return;
            }

            var token = await _credential.GetTokenAsync(
                new TokenRequestContext(new[] { FoundryTokenScope }),
                ct).ConfigureAwait(false);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);
        }

        private async Task<bool> IntentarAplicarAutenticacionSearchAsync(HttpRequestMessage request, string operation, CancellationToken ct)
        {
            try
            {
                await AplicarAutenticacionSearchAsync(request, ct).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex)
            {
                _log.LogWarning(
                    ex,
                    "{Operation}Error. Search authentication failed. endpoint={Endpoint} SearchAuthMode={SearchAuthMode}",
                    operation,
                    _searchCfg.Endpoint,
                    ObtenerSearchAuthMode());
                return false;
            }
        }

        private async Task AplicarAutenticacionSearchAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (!string.IsNullOrWhiteSpace(_searchCfg.ApiKey))
            {
                request.Headers.TryAddWithoutValidation("api-key", _searchCfg.ApiKey.Trim());
                return;
            }

            var token = await _credential.GetTokenAsync(
                new TokenRequestContext(new[] { SearchTokenScope }),
                ct).ConfigureAwait(false);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);
        }

        private string ObtenerSearchAuthMode()
            => string.IsNullOrWhiteSpace(_searchCfg.ApiKey) ? "Entra" : "ApiKey";

        private async Task<string?> ResolverSearchIndexNameAsync(CancellationToken ct)
        {
            if (!string.IsNullOrWhiteSpace(_resolvedIndexName))
                return _resolvedIndexName;

            if (!string.IsNullOrWhiteSpace(_searchCfg.IndexName))
            {
                _resolvedIndexName = _searchCfg.IndexName.Trim();
                return _resolvedIndexName;
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, ConstruirKnowledgeSourcesUri());
            if (!await IntentarAplicarAutenticacionSearchAsync(request, "SearchIndexResolve", ct).ConfigureAwait(false))
                return null;

            using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
            var rawBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _log.LogWarning(
                    "SearchIndexResolveError. statusCode={StatusCode} endpoint={Endpoint} error={Error} bodySample={BodySample}",
                    (int)response.StatusCode,
                    _searchCfg.Endpoint,
                    ExtraerMensajeError(rawBody) ?? $"SearchIndexResolveHttp{(int)response.StatusCode}",
                    Truncar(rawBody));
                return null;
            }

            var indexNames = ExtraerIndexNamesDesdeKnowledgeSources(rawBody);
            if (indexNames.Count == 1)
            {
                _resolvedIndexName = indexNames[0];
                return _resolvedIndexName;
            }

            _log.LogWarning(
                "SearchIndexResolveAmbiguous. endpoint={Endpoint} knowledgeBase={KnowledgeBase} indexCount={IndexCount} indexes={Indexes}",
                _searchCfg.Endpoint,
                _searchCfg.KnowledgeBaseName,
                indexNames.Count,
                string.Join(",", indexNames));
            return null;
        }

        private string ObtenerFoundryModelDeployment()
        {
            if (!string.IsNullOrWhiteSpace(_cfg.ModelDeployment))
                return _cfg.ModelDeployment.Trim();

            if (Interlocked.Exchange(ref _loggedModelFallback, 1) == 0)
            {
                _log.LogWarning(
                    "FOUNDRY_MODEL_DEPLOYMENT no configurado. Se usa FOUNDRY_DEPLOYMENT como fallback temporal. deployment={Deployment}",
                    _cfg.Deployment);
            }

            return (_cfg.Deployment ?? string.Empty).Trim();
        }

        private static string ConstruirInputFoundry(string prompt, IaRequestContext? contexto, string locale)
        {
            var sb = new StringBuilder();
            var langInstruction = ObtenerInstruccionIdioma(locale);
            if (!string.IsNullOrWhiteSpace(langInstruction))
                sb.Append(langInstruction).Append("\n\n");

            sb.Append(prompt ?? string.Empty);

            if (contexto != null)
                sb.Append("\n\nContext: ").Append(JsonSerializer.Serialize(contexto, JsonOptions));

            return sb.ToString();
        }

        private static string ConstruirPromptGroundedDesdeIndice(string prompt, IaRequestContext? contexto, string locale, string groundedContent)
        {
            var question = ConstruirPreguntaRetrieve(prompt, contexto);
            var sb = new StringBuilder();

            if ((locale ?? string.Empty).StartsWith("en", StringComparison.OrdinalIgnoreCase))
            {
                sb.AppendLine("Use ONLY the documentary excerpts below to answer the user.");
                sb.AppendLine("If the excerpts do not contain enough information, say so clearly.");
                sb.AppendLine("Return ONLY a JSON object with this exact shape:");
                sb.AppendLine("{\"message\":\"text\",\"options\":[],\"continue\":true,\"isValid\":true,\"error\":null,\"uploadContext\":null}");
                sb.AppendLine();
                sb.AppendLine($"User question: {question}");
                sb.AppendLine();
                sb.AppendLine("Documentary excerpts:");
            }
            else if ((locale ?? string.Empty).StartsWith("pt", StringComparison.OrdinalIgnoreCase))
            {
                sb.AppendLine("Use APENAS os trechos documentais abaixo para responder ao usuário.");
                sb.AppendLine("Se os trechos não contiverem informação suficiente, diga isso claramente.");
                sb.AppendLine("Retorne APENAS um objeto JSON com esta forma exata:");
                sb.AppendLine("{\"message\":\"texto\",\"options\":[],\"continue\":true,\"isValid\":true,\"error\":null,\"uploadContext\":null}");
                sb.AppendLine();
                sb.AppendLine($"Pergunta do usuário: {question}");
                sb.AppendLine();
                sb.AppendLine("Trechos documentais:");
            }
            else
            {
                sb.AppendLine("Usá ÚNICAMENTE los extractos documentales de abajo para responder al usuario.");
                sb.AppendLine("Si los extractos no contienen información suficiente, indicá eso claramente.");
                sb.AppendLine("Devolvé ÚNICAMENTE un objeto JSON con esta forma exacta:");
                sb.AppendLine("{\"message\":\"texto\",\"options\":[],\"continue\":true,\"isValid\":true,\"error\":null,\"uploadContext\":null}");
                sb.AppendLine();
                sb.AppendLine($"Pregunta del usuario: {question}");
                sb.AppendLine();
                sb.AppendLine("Extractos documentales:");
            }

            sb.AppendLine(groundedContent);
            return sb.ToString();
        }

        private static string ConstruirPreguntaRetrieve(string prompt, IaRequestContext? contexto)
        {
            if (contexto != null)
            {
                var status = contexto.Status ?? string.Empty;
                var benefitType = contexto.BenefitType ?? string.Empty;
                return (contexto.Intent ?? string.Empty) switch
                {
                    "RequisitosYPasosIniciarReintegro" => "¿Cuáles son los requisitos y pasos oficiales para iniciar un reintegro desde la app My Assist Card?",
                    "ProblemasApp" or "ProblemasAppMyAC" => "¿Cuáles son los pasos oficiales para resolver problemas técnicos en la app My Assist Card?",
                    "ReclamoDemoraPago" => "¿Cuál es la respuesta oficial para un reclamo por demora en el pago del reintegro?",
                    "PlazosPagoPendiente" => "¿Cuáles son los plazos oficiales de pago de un reintegro?",
                    "MonedaPagoPendiente" => "¿En qué moneda se paga un reintegro?",
                    "TipoCambioPagoPendiente" => "¿Cómo se aplica el tipo de cambio en el pago del reintegro?",
                    "ConsultaFAQ" or "Faq" => contexto.Question ?? prompt,
                    "ExplicarEstado" => $"¿Qué significa el estado '{status}' para un reintegro de tipo '{benefitType}' y qué debe hacer el cliente?",
                    "ExplicarProximosPasos" => $"¿Cuáles son los próximos pasos oficiales para un reintegro con estado '{status}'?",
                    _ => contexto.Question ?? prompt
                };
            }

            return prompt;
        }

        private static void ParsearAgentResponse(AgentResponse result, string body, string locale)
        {
            var candidateJson = ExtraerJsonLimpio(body);

            try
            {
                var jsonOpts = new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip
                };

                using var doc = JsonDocument.Parse(candidateJson, jsonOpts);
                var root = doc.RootElement;

                if (root.ValueKind == JsonValueKind.Object)
                {
                    if (root.TryGetProperty("message", out var msgProp) && msgProp.ValueKind == JsonValueKind.String)
                        result.Message = msgProp.GetString() ?? string.Empty;

                    if (root.TryGetProperty("options", out var optsProp) && optsProp.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var opt in optsProp.EnumerateArray())
                        {
                            if (opt.ValueKind == JsonValueKind.String)
                                result.Options.Add(opt.GetString()!);
                        }
                    }

                    if (root.TryGetProperty("continue", out var contProp) && (contProp.ValueKind == JsonValueKind.True || contProp.ValueKind == JsonValueKind.False))
                        result.Continue = contProp.GetBoolean();

                    if (root.TryGetProperty("isValid", out var validProp) && (validProp.ValueKind == JsonValueKind.True || validProp.ValueKind == JsonValueKind.False))
                        result.IsValid = validProp.GetBoolean();

                    if (root.TryGetProperty("error", out var errProp) && errProp.ValueKind == JsonValueKind.String)
                        result.Error = errProp.GetString();

                    if (root.TryGetProperty("uploadContext", out var upProp) && upProp.ValueKind == JsonValueKind.Object)
                    {
                        var ctx = new UploadContext();

                        if (upProp.TryGetProperty("benefitRequestId", out var brProp) && brProp.TryGetInt32(out var brId))
                            ctx.BenefitRequestId = brId;

                        if (upProp.TryGetProperty("documentId", out var docIdProp) && docIdProp.TryGetInt32(out var docId))
                            ctx.DocumentId = docId;

                        if (upProp.TryGetProperty("fileName", out var fnProp) && fnProp.ValueKind == JsonValueKind.String)
                            ctx.FileName = fnProp.GetString();

                        if (upProp.TryGetProperty("contentType", out var ctProp) && ctProp.ValueKind == JsonValueKind.String)
                            ctx.ContentType = ctProp.GetString();

                        result.UploadContext = ctx;
                    }
                }
                else if (root.ValueKind == JsonValueKind.String)
                {
                    result.Message = root.GetString() ?? string.Empty;
                }
            }
            catch
            {
                if (string.IsNullOrWhiteSpace(result.Message))
                {
                    if (EsTextoBasura(body))
                        result.Message = MensajeFallback(locale);
                    else
                        result.Message = PareceJson(body) ? MensajeFallback(locale) : body;
                }
            }

            if (string.IsNullOrWhiteSpace(result.Message))
                result.Message = EsTextoBasura(body) ? MensajeFallback(locale) : (PareceJson(body) ? MensajeFallback(locale) : body);

            if (!string.IsNullOrWhiteSpace(result.Message))
                result.Message = SanitizarReferenciasSoporte(result.Message, locale);
        }

        private static string? ExtraerTextoDesdeJson(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("output_text", out var outputText) && outputText.ValueKind == JsonValueKind.String)
                    return outputText.GetString();

                if (root.TryGetProperty("output", out var output) && output.ValueKind == JsonValueKind.Array)
                {
                    var outputValue = ExtraerTextoDesdeArrayDeContenido(output);
                    if (!string.IsNullOrWhiteSpace(outputValue))
                        return outputValue;
                }

                if (root.TryGetProperty("response", out var response) && response.ValueKind == JsonValueKind.Array)
                {
                    var responseValue = ExtraerTextoDesdeArrayDeContenido(response);
                    if (!string.IsNullOrWhiteSpace(responseValue))
                        return responseValue;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private static string? ExtraerTextoDesdeArrayDeContenido(JsonElement array)
        {
            var sb = new StringBuilder();
            foreach (var item in array.EnumerateArray())
            {
                if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var c in content.EnumerateArray())
                {
                    if (c.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String)
                    {
                        if (sb.Length > 0)
                            sb.Append('\n');
                        sb.Append(t.GetString());
                    }
                }
            }

            var text = sb.ToString();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }

        private static string? ExtraerMensajeError(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("error", out var error))
                {
                    if (error.ValueKind == JsonValueKind.Object)
                    {
                        if (error.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.String)
                            return msg.GetString();
                    }
                    else if (error.ValueKind == JsonValueKind.String)
                    {
                        return error.GetString();
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private static List<string> ExtraerIndexNamesDesdeKnowledgeSources(string? json)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(json))
                return result;

            try
            {
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.Array)
                    return result;

                foreach (var item in value.EnumerateArray())
                {
                    if (!item.TryGetProperty("searchIndexParameters", out var searchIndexParams)
                        || searchIndexParams.ValueKind != JsonValueKind.Object)
                        continue;

                    if (!searchIndexParams.TryGetProperty("searchIndexName", out var nameProp)
                        || nameProp.ValueKind != JsonValueKind.String)
                        continue;

                    var name = nameProp.GetString();
                    if (!string.IsNullOrWhiteSpace(name) && !result.Contains(name, StringComparer.OrdinalIgnoreCase))
                        result.Add(name);
                }
            }
            catch
            {
            }

            return result;
        }

        private static List<SearchDocumentHit> ExtraerHitsDeBusqueda(string? json)
        {
            var hits = new List<SearchDocumentHit>();
            if (string.IsNullOrWhiteSpace(json))
                return hits;

            try
            {
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.Array)
                    return hits;

                foreach (var item in value.EnumerateArray())
                {
                    string? content = null;
                    string? title = null;
                    string? path = null;
                    double? score = null;

                    if (item.TryGetProperty("content", out var contentProp) && contentProp.ValueKind == JsonValueKind.String)
                        content = contentProp.GetString();
                    if (item.TryGetProperty("metadata_storage_name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
                        title = nameProp.GetString();
                    if (item.TryGetProperty("metadata_storage_path", out var pathProp) && pathProp.ValueKind == JsonValueKind.String)
                        path = pathProp.GetString();
                    if (item.TryGetProperty("@search.score", out var scoreProp) && scoreProp.TryGetDouble(out var rawScore))
                        score = rawScore;

                    if (string.IsNullOrWhiteSpace(content))
                        continue;

                    hits.Add(new SearchDocumentHit
                    {
                        Content = content,
                        Title = title,
                        Path = path,
                        Score = score
                    });
                }
            }
            catch
            {
            }

            return hits;
        }

        private static string? ConstruirGroundingDesdeHits(List<SearchDocumentHit> hits)
        {
            if (hits == null || hits.Count == 0)
                return null;

            var sb = new StringBuilder();
            var count = 0;
            foreach (var hit in hits)
            {
                if (count >= MaxGroundingDocuments)
                    break;

                var excerpt = hit.Content ?? string.Empty;
                excerpt = excerpt.Replace("\r", " ").Replace("\n", " ").Trim();
                excerpt = Regex.Replace(excerpt, "\\s+", " ");
                if (excerpt.Length > MaxCharsPerGroundingDocument)
                    excerpt = excerpt[..MaxCharsPerGroundingDocument] + "...";

                if (string.IsNullOrWhiteSpace(excerpt))
                    continue;

                if (sb.Length > 0)
                    sb.AppendLine();

                count++;
                sb.Append("[Doc ").Append(count).Append("]");
                if (!string.IsNullOrWhiteSpace(hit.Title))
                    sb.Append(" ").Append(hit.Title);
                if (!string.IsNullOrWhiteSpace(hit.Path))
                    sb.Append(" (").Append(hit.Path).Append(")");
                sb.AppendLine();
                sb.AppendLine(excerpt);
            }

            return sb.Length == 0 ? null : sb.ToString().Trim();
        }

        private static string MensajeFallback(string locale)
        {
            if (string.IsNullOrWhiteSpace(locale))
                locale = "es";

            var l = locale.ToLowerInvariant();
            if (l.StartsWith("en"))
                return "I could not generate an answer at this moment. Please try again later.";
            if (l.StartsWith("pt"))
                return "Não consegui gerar uma resposta no momento. Tente novamente mais tarde.";
            return "No pude generar una respuesta en este momento. Intentá más tarde.";
        }

        private async Task<AgentResponse?> ObtenerDeCacheAsync(string clave, CancellationToken ct)
        {
            if (_cache == null || string.IsNullOrEmpty(clave))
                return null;

            try
            {
                var json = await _cache.ObtenerAsync(clave, ct).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(json))
                    return null;
                return JsonSerializer.Deserialize<AgentResponse>(json, JsonOptions);
            }
            catch (Exception ex)
            {
                _log.LogDebug(ex, "IaCacheRead fallo. clave={Clave}", clave);
                return null;
            }
        }

        private async Task GuardarEnCacheAsync(string clave, AgentResponse respuesta, TimeSpan ttl, CancellationToken ct)
        {
            if (_cache == null || string.IsNullOrEmpty(clave) || ttl <= TimeSpan.Zero)
                return;
            if (respuesta == null || string.IsNullOrWhiteSpace(respuesta.Message))
                return;
            if (!string.IsNullOrEmpty(respuesta.Error))
                return;

            try
            {
                var json = JsonSerializer.Serialize(respuesta, JsonOptions);
                await _cache.GuardarAsync(clave, json, ttl, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log.LogDebug(ex, "IaCacheWrite fallo. clave={Clave}", clave);
            }
        }

        private static TimeSpan ObtenerTtlCache(IaRequestContext? contexto)
        {
            var intent = contexto?.Intent ?? string.Empty;
            return intent switch
            {
                "ExplicarEstado" => TimeSpan.FromMinutes(15),
                "ExplicarProximosPasos" => TimeSpan.FromMinutes(15),
                "ExplicarPago" => TimeSpan.FromMinutes(15),
                "RequisitosYPasosIniciarReintegro" => TimeSpan.FromHours(6),
                "ProblemasApp" or "ProblemasAppMyAC" => TimeSpan.FromHours(6),
                "ReclamoDemoraPago" => TimeSpan.FromHours(2),
                "PlazosPagoPendiente" => TimeSpan.FromHours(6),
                "MonedaPagoPendiente" => TimeSpan.FromHours(6),
                "TipoCambioPagoPendiente" => TimeSpan.FromHours(6),
                "ConsultaFAQ" or "Faq" => TimeSpan.FromHours(2),
                _ => TimeSpan.FromMinutes(30)
            };
        }

        private static string ConstruirClaveCache(string textoEntrada, IaRequestContext? contexto, string locale)
        {
            var sb = new StringBuilder();
            sb.Append("ia:v2:");
            sb.Append(locale.ToLowerInvariant()).Append(':');
            sb.Append(contexto?.Intent ?? "free").Append(':');
            sb.Append(NormalizarParaComparacion(contexto?.Status ?? string.Empty)).Append(':');
            sb.Append(NormalizarParaComparacion(contexto?.BenefitType ?? string.Empty)).Append(':');
            var pregunta = !string.IsNullOrWhiteSpace(contexto?.Question)
                ? contexto!.Question!
                : textoEntrada ?? string.Empty;
            sb.Append(HashSha256(NormalizarParaComparacion(pregunta)));
            return sb.ToString();
        }

        private static string HashSha256(string entrada)
        {
            var bytes = Encoding.UTF8.GetBytes(entrada ?? string.Empty);
            var hash = SHA256.HashData(bytes);
            var hex = new StringBuilder(hash.Length * 2);
            foreach (var b in hash)
                hex.Append(b.ToString("x2", CultureInfo.InvariantCulture));
            return hex.ToString(0, 24);
        }

        private static string ConstruirPromptSinGrounding(string prompt, IaRequestContext? contexto, string locale)
        {
            var sb = new StringBuilder();
            var l = (locale ?? string.Empty).ToLowerInvariant();
            if (l.StartsWith("en"))
            {
                sb.AppendLine("Answer the user's question using your general knowledge about reimbursement processes.");
                sb.AppendLine("If you don't have enough information, say so clearly and suggest contacting reintegros@assistcard.com.");
                sb.AppendLine("Return ONLY a JSON object with this exact shape: {\"message\":\"text\",\"options\":[],\"continue\":true,\"isValid\":true,\"error\":null,\"uploadContext\":null}");
            }
            else if (l.StartsWith("pt"))
            {
                sb.AppendLine("Responda à pergunta do usuário com seu conhecimento geral sobre processos de reembolso.");
                sb.AppendLine("Se não tiver informação suficiente, diga claramente e sugira contatar reembolso@assistcard.com.");
                sb.AppendLine("Retorne APENAS um objeto JSON com esta forma exata: {\"message\":\"texto\",\"options\":[],\"continue\":true,\"isValid\":true,\"error\":null,\"uploadContext\":null}");
            }
            else
            {
                sb.AppendLine("Respondé a la pregunta del usuario con tu conocimiento general sobre procesos de reintegro.");
                sb.AppendLine("Si no tenés información suficiente, indicá eso claramente y sugerí escribir a reintegros@assistcard.com.");
                sb.AppendLine("Devolvé ÚNICAMENTE un objeto JSON con esta forma exacta: {\"message\":\"texto\",\"options\":[],\"continue\":true,\"isValid\":true,\"error\":null,\"uploadContext\":null}");
            }
            sb.AppendLine();
            sb.AppendLine($"Pregunta: {prompt}");
            if (contexto != null)
            {
                sb.AppendLine();
                sb.Append("Contexto: ").Append(JsonSerializer.Serialize(contexto, JsonOptions));
            }
            return sb.ToString();
        }

        private static bool PareceJson(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
                return false;

            var t = texto.Trim();
            if (t.StartsWith("{"))
                return true;
            if (t.StartsWith("["))
                return true;
            if (t.StartsWith("json", StringComparison.OrdinalIgnoreCase) && t.Contains("{") && t.Contains("}"))
                return true;
            if (t.StartsWith("```", StringComparison.Ordinal) && t.Contains("{") && t.Contains("}"))
                return true;
            return false;
        }

        private static string ExtraerJsonLimpio(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return string.Empty;

            var t = body.Trim();
            if (t.StartsWith("json", StringComparison.OrdinalIgnoreCase))
            {
                var idxNl = t.IndexOf('\n');
                if (idxNl > 0)
                    t = t[(idxNl + 1)..].Trim();
            }

            if (t.StartsWith("```", StringComparison.Ordinal))
            {
                t = Regex.Replace(t, @"^```\w*\s*", string.Empty, RegexOptions.Multiline);
                t = Regex.Replace(t, @"\s*```$", string.Empty, RegexOptions.Multiline);
                t = t.Trim();
            }

            if (!t.StartsWith("{") && t.Contains("{") && t.Contains("}"))
            {
                var start = t.IndexOf('{');
                var end = t.LastIndexOf('}');
                if (start >= 0 && end > start)
                    t = t.Substring(start, end - start + 1).Trim();
            }

            return t;
        }

        private static bool EsTextoBasura(string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
                return true;

            var t = texto.Trim();
            if (t.Equals("OpenAI.Responses.OpenAIResponse", StringComparison.Ordinal))
                return true;

            if (t.StartsWith("OpenAI.", StringComparison.Ordinal) && t.Contains("Response", StringComparison.Ordinal))
                return true;

            return false;
        }

        private static string Truncar(string? texto, int max = 900)
        {
            if (string.IsNullOrWhiteSpace(texto))
                return "<empty>";

            return texto.Length <= max ? texto : texto[..max];
        }

        private static string FallbackExplicarEstado(string status, string benefitType, string locale)
        {
            var st = (status ?? string.Empty).Trim();
            var bt = (benefitType ?? string.Empty).Trim();
            var l = (locale ?? "es").ToLowerInvariant();

            if (l.StartsWith("en"))
                return $"The current reimbursement status is '{st}' (benefit type: '{bt}'). Please check if there are pending documents or payment details, and contact support if you need additional clarification.";

            if (l.StartsWith("pt"))
                return $"O status atual do reembolso é '{st}' (tipo de benefício: '{bt}'). Verifique se há documentos pendentes ou detalhes de pagamento e, se precisar, contate o suporte para mais informações.";

            var stLower = st.ToLowerInvariant();
            var displayStatus = st;
            if (string.Equals(st, "received", StringComparison.OrdinalIgnoreCase))
                displayStatus = "Recibido";
            if (stLower.Contains("received") || stLower.Contains("recib"))
                return $"El estado '{displayStatus}' significa que el reintegro fue recibido y está en proceso de revisión. Todavía no fue evaluado/aprobado. Si te solicitan documentación adicional, te va a aparecer como pendiente.";
            if (stLower.Contains("under review") || stLower.Contains("revision") || stLower.Contains("revisión"))
                return $"El estado '{st}' indica que el reintegro está siendo revisado. En general, solo resta esperar la validación; si te piden documentación, te aparecerá como pendiente.";
            if (stLower.Contains("pending payment") || stLower.Contains("pendiente de pago") || stLower.Contains("pago pendiente"))
                return $"El estado '{st}' significa que el reintegro fue aprobado y está en proceso de pago. Revisá el correo asociado y los datos de pago informados; si no ves novedades dentro del plazo indicado, contactá a soporte.";
            if (stLower.Contains("pending doc") || stLower.Contains("documentacion pendiente") || stLower.Contains("documentación pendiente"))
                return $"El estado '{st}' indica que hay documentación pendiente o una validación asociada al reintegro. Revisá si tenés documentos pendientes para adjuntar.";
            if (stLower.Contains("pending") || stLower.Contains("pend"))
                return $"El estado '{st}' suele indicar que hay una acción pendiente (por ejemplo, documentación solicitada o validación). Revisá si tenés documentos pendientes para adjuntar.";
            if (stLower.Contains("approved") || stLower.Contains("aprob"))
                return $"El estado '{st}' indica que el reintegro fue aprobado. El próximo paso habitual es el proceso de pago (si aplica).";
            if (stLower.Contains("closed") || stLower.Contains("cerr"))
                return $"El estado '{st}' significa que el reintegro fue cerrado/finalizado. Si el caso tiene importes aprobados, revisá el detalle financiero o de pago; si necesitás más información, podés contactar a soporte.";
            if (stLower.Contains("finished") || stLower.Contains("finaliz"))
                return $"El estado '{st}' significa que el reintegro fue finalizado. Revisá el detalle financiero o de pago para confirmar el resultado del trámite.";
            if (stLower.Contains("denied") || stLower.Contains("reject") || stLower.Contains("deneg"))
                return $"El estado '{st}' indica que el reintegro fue rechazado/denegado. Puede deberse a documentación incompleta o criterios de cobertura. Si querés, podés detallar el problema para revisar próximos pasos.";

            return $"El estado actual del reintegro es '{st}' (tipo de beneficio: '{bt}'). Si necesitás más detalle, indicame qué parte querés entender (documentación, pagos o próximos pasos).";
        }

        private static string FallbackProximosPasos(string status, string locale)
        {
            var st = (status ?? string.Empty).Trim();
            var l = (locale ?? "es").ToLowerInvariant();

            if (l.StartsWith("en"))
                return $"Next steps depend on the status '{st}'. Check if there are pending documents, and if the status is approved, review payment details.";

            if (l.StartsWith("pt"))
                return $"Os próximos passos dependem do status '{st}'. Verifique se há documentos pendentes e, se estiver aprovado, revise os detalhes de pagamento.";

            return $"Según el estado '{st}', los próximos pasos suelen ser: revisar si hay documentación pendiente; si está aprobado, revisar el detalle de pago; si está en revisión, aguardar actualización.";
        }

        private static string ObtenerInstruccionIdioma(string locale)
        {
            if (string.IsNullOrWhiteSpace(locale))
                return string.Empty;

            var l = locale.Trim().ToLowerInvariant();
            if (l.StartsWith("en"))
            {
                return "Respond exclusively in English.\n"
                     + "If the issue requires escalation, tell the user to write to reintegros@assistcard.com for further assistance.";
            }

            if (l.StartsWith("pt"))
            {
                return "Responda exclusivamente em Português.\n"
                     + "Se o caso precisar de encaminhamento, indique ao usuário que escreva para reembolso@assistcard.com para mais assistência.";
            }

            return "Respondé exclusivamente en español.\n"
                 + "Si el caso requiere derivación, indicá al usuario que escriba a reintegros@assistcard.com para más asistencia.";
        }

        private static string SanitizarReferenciasSoporte(string mensaje, string locale)
            => GuardrailService.SanitizarReferenciasSoporte(mensaje, locale);
    }
}
