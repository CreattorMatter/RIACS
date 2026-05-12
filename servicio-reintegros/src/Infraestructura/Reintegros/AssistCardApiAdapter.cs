using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServicioReintegros.AssistCard.Aplicacion.Dtos;
using ServicioReintegros.AssistCard.Aplicacion.Puertos;
using ServicioReintegros.AssistCard.Configuracion;

namespace ServicioReintegros.AssistCard.Infraestructura.Reintegros
{
    public sealed class AssistCardApiAdapter : IProveedorReintegros
    {
        private readonly HttpClient _http;
        private readonly ReintegrosOpciones _cfg;
        private readonly ILogger<AssistCardApiAdapter> _log;

        public AssistCardApiAdapter(HttpClient http, IOptions<ReintegrosOpciones> cfg, ILogger<AssistCardApiAdapter> log)
        {
            _http = http;
            _cfg = cfg.Value;
            _log = log;
        }

        public async Task<IEnumerable<ResumenReintegro>> BuscarPorIdentificadorAsync(ConsultaIdentificador consulta, CancellationToken ct = default)
        {
            var url = "api/chatbot/Agent/Reimbursement"; // según PROMPT
            var body = new
            {
                benefitRequestId = consulta.BenefitRequestId,
                caseId = consulta.CaseId,
                email = consulta.Email,
                countryIsoCode = consulta.CountryIsoCode,
                nationalId = consulta.NationalId,
                voucherCode = consulta.VoucherCode
            };

            var jsonOpts = new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
            var jsonBody = JsonSerializer.Serialize(body, jsonOpts);

            if (_log.IsEnabled(LogLevel.Debug))
                _log.LogDebug("ReintegrosApi.RequestBody json={Json}", jsonBody);

            var traceId = Activity.Current?.TraceId.ToString();
            var sw = Stopwatch.StartNew();

            try
            {
                _log.LogInformation(
                    "ReintegrosApi.RequestStart traceId={TraceId} url={Url} hasBenefitRequestId={HasBenefitRequestId} hasCaseId={HasCaseId} hasEmail={HasEmail} hasVoucherCode={HasVoucherCode} countryIsoCode={CountryIsoCode}",
                    traceId,
                    url,
                    consulta.BenefitRequestId.HasValue,
                    !string.IsNullOrWhiteSpace(consulta.CaseId),
                    !string.IsNullOrWhiteSpace(consulta.Email),
                    !string.IsNullOrWhiteSpace(consulta.VoucherCode),
                    consulta.CountryIsoCode);

                const int maxIntentos = 3;
                HttpRequestException? ultimoErrorTransitorio = null;

                for (var intento = 1; intento <= maxIntentos; intento++)
                {
                    sw = Stopwatch.StartNew();

                    try
                    {
                        using var req = new HttpRequestMessage(HttpMethod.Post, url);
                        req.Headers.Add("X-API-Key", _cfg.ApiKey);
                        req.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                        using var resp = await _http.SendAsync(req, ct);
                        var statusCode = (int)resp.StatusCode;
                        var json = await resp.Content.ReadAsStringAsync(ct);
                        sw.Stop();

                        if (!resp.IsSuccessStatusCode)
                        {
                            var sample = json.Length <= 1500 ? json : json.Substring(0, 1500);
                            _log.LogWarning(
                                "ReintegrosApi.ResponseNonSuccess traceId={TraceId} statusCode={StatusCode} elapsedMs={ElapsedMs} body(sample)={BodySample}",
                                traceId,
                                statusCode,
                                sw.ElapsedMilliseconds,
                                sample);
                            resp.EnsureSuccessStatusCode();
                        }

                        _log.LogInformation(
                            "ReintegrosApi.ResponseOk traceId={TraceId} statusCode={StatusCode} elapsedMs={ElapsedMs} length={Length}",
                            traceId,
                            statusCode,
                            sw.ElapsedMilliseconds,
                            json?.Length ?? 0);

                        using var doc = JsonDocument.Parse(json);

                        try
                        {
                            _log.LogInformation("Reintegros API raw root kind={Kind}", doc.RootElement.ValueKind);
                            if (doc.RootElement.ValueKind == JsonValueKind.Object)
                            {
                                var props = doc.RootElement.EnumerateObject().Select(p => p.Name).ToArray();
                                _log.LogInformation("Reintegros API root props={Props}", string.Join(",", props));
                            }

                            if (_log.IsEnabled(LogLevel.Debug))
                            {
                                var sample = json.Length <= 1500 ? json : json.Substring(0, 1500);
                                _log.LogDebug("Reintegros API raw json(sample)={JsonSample}", sample);
                            }
                        }
                        catch
                        {
                        }

                        var root = doc.RootElement;
                        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var dataEl))
                            root = dataEl;

                        var resultados = new List<ResumenReintegro>();
                        // Mapeo básico (ajustar según respuesta real)
                        if (root.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in root.EnumerateArray())
                            {
                                resultados.Add(ParseResumen(item));
                            }
                        }
                        else if (root.ValueKind == JsonValueKind.Object)
                        {
                            resultados.Add(ParseResumen(root));
                        }

                        return resultados;
                    }
                    catch (HttpRequestException ex) when (ex.StatusCode is null && intento < maxIntentos)
                    {
                        ultimoErrorTransitorio = ex;

                        if (_log.IsEnabled(LogLevel.Warning))
                        {
                            _log.LogWarning(
                                ex,
                                "ReintegrosApi.RequestRetry traceId={TraceId} url={Url} intento={Intento}/{MaxIntentos}",
                                traceId,
                                url,
                                intento,
                                maxIntentos);
                        }

                        var delayMs = intento switch
                        {
                            1 => 250,
                            2 => 750,
                            _ => 1000
                        };
                        await Task.Delay(delayMs, ct);
                    }
                }

                throw new HttpRequestException(
                    "No se pudo completar la solicitud al API de reintegros luego de reintentos.",
                    ultimoErrorTransitorio);
            }
            catch (Exception ex)
            {
                sw.Stop();
                _log.LogError(
                    ex,
                    "ReintegrosApi.RequestError traceId={TraceId} url={Url} elapsedMs={ElapsedMs}",
                    traceId,
                    url,
                    sw.ElapsedMilliseconds);
                throw;
            }
        }

        public async Task AgregarDocumentosAsync(int benefitRequestId, IEnumerable<(int DocumentId, string Nombre, string ContentType, Stream Contenido)> archivos, CancellationToken ct = default)
        {
            if (archivos is null)
                return;

            // Construir lista de archivos con contenido en Base64 según especificación
            var filesPayload = new List<object>();
            foreach (var (documentId, nombre, contentType, contenido) in archivos)
            {
                if (contenido == null)
                    continue;

                using var ms = new MemoryStream();
                await contenido.CopyToAsync(ms, ct);
                var base64 = Convert.ToBase64String(ms.ToArray());

                filesPayload.Add(new
                {
                    documentId,
                    name = nombre,
                    contentBase64 = base64
                });
            }

            if (filesPayload.Count == 0)
                return;

            var body = new
            {
                benefitRequestId,
                files = filesPayload
            };

            // Endpoint según documentación técnica de carga de documentación pendiente
            var url = "api/chatbot/Agent/Reimbursement/upload-requested-files";

            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Add("X-API-Key", _cfg.ApiKey);
            req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

            var resp = await _http.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();
        }

        public Task ActualizarDatosBancariosAsync(int benefitRequestId, object datosBancarios, CancellationToken ct = default)
            => Task.CompletedTask; // pendiente de especificación de API

        private static string? LeerComoString(JsonElement el)
        {
            try
            {
                return el.ValueKind switch
                {
                    JsonValueKind.String => el.GetString(),
                    JsonValueKind.Number => el.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    _ => null
                };
            }
            catch
            {
                return null;
            }
        }

        private ResumenReintegro ParseResumen(JsonElement el)
        {
            try
            {
                _log.LogInformation("ParseResumen kind={Kind}", el.ValueKind);
                if (el.ValueKind == JsonValueKind.Object)
                {
                    var props = el.EnumerateObject().Select(p => p.Name).ToArray();
                    _log.LogInformation("ParseResumen props={Props}", string.Join(",", props));
                }

                if (_log.IsEnabled(LogLevel.Debug))
                {
                    var raw = el.GetRawText();
                    var sample = raw.Length <= 1200 ? raw : raw.Substring(0, 1200);
                    _log.LogDebug("ParseResumen raw(sample)={JsonSample}", sample);
                }
            }
            catch
            {
            }

            var resumen = new ResumenReintegro
            {
                BenefitId = el.TryGetProperty("benefitId", out var v1) ? LeerComoString(v1) : null,
                CaseId = el.TryGetProperty("caseId", out var v2) ? LeerComoString(v2) : null,
                BenefitType = el.TryGetProperty("benefitType", out var v3) ? LeerComoString(v3) : null,
                BenefitTypeOriginal = el.TryGetProperty("benefitType", out var v3o) ? LeerComoString(v3o) : null,
                Status = el.TryGetProperty("status", out var v4) ? LeerComoString(v4) : null,
                StatusOriginal = el.TryGetProperty("status", out var v4o) ? LeerComoString(v4o) : null,
                TotalRequestedUsd = el.TryGetProperty("totalRequestedUsd", out var v5) && v5.TryGetDecimal(out var d1) ? d1 : null,
                TotalApprovedUsd = el.TryGetProperty("totalApprovedUsd", out var v6) && v6.TryGetDecimal(out var d2) ? d2 : null,
                TotalDeniedUsd = el.TryGetProperty("totalDeniedUsd", out var v7) && v7.TryGetDecimal(out var d3) ? d3 : null,
            };

            if (el.TryGetProperty("createDate", out var cd))
            {
                if (cd.ValueKind == JsonValueKind.String && cd.TryGetDateTime(out var dt))
                    resumen.CreateDate = dt;
            }

            try
            {
                _log.LogInformation(
                    "ParseResumen mapped benefitId={BenefitId} caseId={CaseId} status={Status}",
                    resumen.BenefitId,
                    resumen.CaseId,
                    resumen.Status);
            }
            catch
            {
            }

            if (el.TryGetProperty("paymentDetails", out var pds) && pds.ValueKind == JsonValueKind.Array)
            {
                foreach (var pd in pds.EnumerateArray())
                {
                    var pago = new DetallePago
                    {
                        Metodo = pd.TryGetProperty("method", out var m) ? LeerComoString(m) : null,
                        Estado = pd.TryGetProperty("status", out var s) ? LeerComoString(s) : null,
                        MonedaOriginal = pd.TryGetProperty("currency", out var c) ? LeerComoString(c) : null,
                        MontoUsd = pd.TryGetProperty("amountUsd", out var a) && a.TryGetDecimal(out var ad) ? ad : null,
                        Detalle = pd.TryGetProperty("paymentDetail", out var det) ? LeerComoString(det) : null,
                    };

                    if (pd.TryGetProperty("paymentDate", out var dtPay) && dtPay.ValueKind == JsonValueKind.String && dtPay.TryGetDateTime(out var fPay))
                        pago.FechaPago = fPay;
                    else if (pd.TryGetProperty("date", out var dt) && dt.ValueKind == JsonValueKind.String && dt.TryGetDateTime(out var f))
                        pago.FechaPago = f;

                    resumen.PaymentDetails.Add(new DetallePago
                    {
                        Metodo = pago.Metodo,
                        Estado = pago.Estado,
                        MonedaOriginal = pago.MonedaOriginal,
                        MontoUsd = pago.MontoUsd,
                        FechaPago = pago.FechaPago,
                        Detalle = pago.Detalle,
                    });
                }
            }

            if (el.TryGetProperty("awaitResult", out var awaitRes) && awaitRes.ValueKind == JsonValueKind.Array)
            {
                ParseItemsDenegados(awaitRes, resumen);
            }
            else if (el.TryGetProperty("auditResult", out var auditRes) && auditRes.ValueKind == JsonValueKind.Array)
            {
                ParseItemsDenegados(auditRes, resumen);
            }

            if (el.TryGetProperty("pendingDocuments", out var pdocs) && pdocs.ValueKind == JsonValueKind.Array)
            {
                foreach (var d in pdocs.EnumerateArray())
                {
                    int? docId = null;
                    if (d.TryGetProperty("documentId", out var did))
                    {
                        var sId = LeerComoString(did);
                        if (!string.IsNullOrWhiteSpace(sId) && int.TryParse(sId, out var parsed))
                            docId = parsed;
                    }

                    resumen.PendingDocuments.Add(new DocumentoPendiente
                    {
                        DocumentId = docId,
                        DocumentType = d.TryGetProperty("documentType", out var dt) ? LeerComoString(dt) : null,
                        Description = d.TryGetProperty("description", out var desc) ? LeerComoString(desc) : null,
                    });
                }
            }
            return resumen;
        }

        private static void ParseItemsDenegados(JsonElement itemsArray, ResumenReintegro resumen)
        {
            foreach (var it in itemsArray.EnumerateArray())
            {
                if (it.ValueKind != JsonValueKind.Object) continue;

                var item = new ItemDetalleReintegro
                {
                    Concept = it.TryGetProperty("concept", out var c) ? LeerComoString(c) : null,
                    Currency = it.TryGetProperty("currency", out var cur) ? LeerComoString(cur) : null,
                    Denied = it.TryGetProperty("denied", out var den) && den.TryGetDecimal(out var denDec) ? denDec : null,
                    DeniedUsd = it.TryGetProperty("deniedUsd", out var denUsd) && denUsd.TryGetDecimal(out var denUsdDec) ? denUsdDec : null,
                };

                if (it.TryGetProperty("denialReason", out var dr))
                {
                    if (dr.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var rr in dr.EnumerateArray())
                        {
                            var s = LeerComoString(rr);
                            if (!string.IsNullOrWhiteSpace(s))
                                item.DenialReason.Add(s);
                        }
                    }
                    else if (dr.ValueKind == JsonValueKind.String)
                    {
                        var s = LeerComoString(dr);
                        if (!string.IsNullOrWhiteSpace(s))
                            item.DenialReason.Add(s);
                    }
                }

                resumen.AwaitResult.Add(item);
            }
        }
    }
}
