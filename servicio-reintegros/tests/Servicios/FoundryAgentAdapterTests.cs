using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ServicioReintegros.AssistCard.Aplicacion.Dtos;
using ServicioReintegros.AssistCard.Configuracion;
using ServicioReintegros.AssistCard.Infraestructura.IA;

namespace ServicioReintegros.Tests.Servicios
{
    public sealed class FoundryAgentAdapterTests
    {
        [Fact]
        public async Task EnviarAsync_UsaRetrieveDirectoCuandoLaKbResponde()
        {
            var handler = new StubHttpMessageHandler((request, _) =>
            {
                if (request.RequestUri!.AbsoluteUri.Contains("/knowledgebases/", StringComparison.OrdinalIgnoreCase))
                {
                    return Json(HttpStatusCode.OK, """
                    {
                      "response": [
                        {
                          "content": [
                            {
                              "text": "Texto oficial KB"
                            }
                          ]
                        }
                      ]
                    }
                    """);
                }

                throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
            });

            var adapter = CrearAdapter(handler);
            var response = await adapter.EnviarAsync(
                "user-1",
                "es",
                "¿Qué requisitos necesito?",
                new IaRequestContext { Intent = "RequisitosYPasosIniciarReintegro", Question = "¿Qué requisitos necesito?" });

            Assert.True(response.IsValid);
            Assert.Equal("Texto oficial KB", response.Message);
            var request = Assert.Single(handler.Requests);
            Assert.True(request.Headers.ContainsKey("api-key"));
            Assert.False(request.Headers.ContainsKey("Authorization"));
        }

        [Fact]
        public async Task EnviarAsync_SearchSinApiKeyUsaBearerToken()
        {
            var handler = new StubHttpMessageHandler((request, _) =>
            {
                if (request.RequestUri!.AbsoluteUri.Contains("/knowledgebases/", StringComparison.OrdinalIgnoreCase))
                {
                    return Json(HttpStatusCode.OK, """
                    {
                      "response": [
                        {
                          "content": [
                            {
                              "text": "Respuesta por RBAC"
                            }
                          ]
                        }
                      ]
                    }
                    """);
                }

                throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
            });

            var adapter = CrearAdapter(
                handler,
                searchApiKey: string.Empty,
                credential: new StaticTokenCredential("search-token"));

            var response = await adapter.EnviarAsync(
                "user-1",
                "es",
                "Â¿QuÃ© requisitos necesito?",
                new IaRequestContext { Intent = "RequisitosYPasosIniciarReintegro", Question = "Â¿QuÃ© requisitos necesito?" });

            Assert.True(response.IsValid);
            Assert.Equal("Respuesta por RBAC", response.Message);
            var request = Assert.Single(handler.Requests);
            Assert.False(request.Headers.ContainsKey("api-key"));
            Assert.Equal("Bearer search-token", request.Headers["Authorization"]);
        }

        [Fact]
        public async Task EnviarAsync_Documental_HaceFallbackAlIndiceCuandoRetrieveFallaPorSemantic()
        {
            var handler = new StubHttpMessageHandler((request, body) =>
            {
                var uri = request.RequestUri!.AbsoluteUri;

                if (uri.Contains("/knowledgebases/", StringComparison.OrdinalIgnoreCase))
                {
                    return Json(HttpStatusCode.BadRequest, """
                    {
                      "error": {
                        "message": "Semantic Search is not enabled for this service. Parameter name: queryType"
                      }
                    }
                    """);
                }

                if (uri.Contains("/indexes/assistcard-reintegros-index/docs/search", StringComparison.OrdinalIgnoreCase))
                {
                    return Json(HttpStatusCode.OK, """
                    {
                      "value": [
                        {
                          "@search.score": 2.4,
                          "content": "Requisitos documentales por tipo de reintegro. Gastos médicos y/o farmacia: Foto del pasaporte, sellos de entrada y salida, datos de cuenta bancaria, comprobantes originales de gastos y notas médicas.",
                          "metadata_storage_name": "Preguntas frecuentes de ACdocx.docx",
                          "metadata_storage_path": "path-1"
                        }
                      ]
                    }
                    """);
                }

                if (uri.Contains("/openai/v1/responses", StringComparison.OrdinalIgnoreCase))
                {
                    return Json(HttpStatusCode.OK, """
                    {
                      "output_text": "{\"message\":\"Necesitás pasaporte, sellos o itinerario, datos bancarios, comprobantes de gastos y documentación médica cuando corresponda.\",\"options\":[],\"continue\":true,\"isValid\":true,\"error\":null,\"uploadContext\":null}"
                    }
                    """);
                }

                throw new InvalidOperationException($"Unexpected request: {uri}");
            });

            var adapter = CrearAdapter(handler);
            var response = await adapter.EnviarAsync(
                "user-1",
                "es",
                "¿Qué requisitos necesito?",
                new IaRequestContext { Intent = "RequisitosYPasosIniciarReintegro", Question = "¿Qué requisitos necesito?" });

            Assert.True(response.IsValid);
            Assert.Equal("Necesitás pasaporte, sellos o itinerario, datos bancarios, comprobantes de gastos y documentación médica cuando corresponda.", response.Message);
            Assert.Equal(3, handler.Requests.Count);
            var searchRequest = Assert.Single(handler.Requests.Where(r => r.Uri.Contains("/indexes/assistcard-reintegros-index/docs/search", StringComparison.OrdinalIgnoreCase)));
            Assert.Contains("requisitos y pasos oficiales para iniciar un reintegro", searchRequest.Body, StringComparison.OrdinalIgnoreCase);
            var responsesRequest = Assert.Single(handler.Requests.Where(r => r.Uri.Contains("/openai/v1/responses", StringComparison.OrdinalIgnoreCase)));
            Assert.DoesNotContain("agent_reference", responsesRequest.Body, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task EnviarAsync_Documental_HaceFallbackAlIndiceCuandoRetrieveNoTraeContenidoRelevante()
        {
            var handler = new StubHttpMessageHandler((request, body) =>
            {
                var uri = request.RequestUri!.AbsoluteUri;

                if (uri.Contains("/knowledgebases/", StringComparison.OrdinalIgnoreCase))
                {
                    return Json(HttpStatusCode.OK, """
                    {
                      "response": [
                        {
                          "content": [
                            {
                              "text": "No se encontro contenido relevante para su consulta."
                            }
                          ]
                        }
                      ]
                    }
                    """);
                }

                if (uri.Contains("/indexes/assistcard-reintegros-index/docs/search", StringComparison.OrdinalIgnoreCase))
                {
                    return Json(HttpStatusCode.OK, """
                    {
                      "value": [
                        {
                          "@search.score": 2.4,
                          "content": "Para iniciar un reintegro se debe ingresar a la app My Assist Card, cargar el gasto y adjuntar la documentacion requerida.",
                          "metadata_storage_name": "faq.docx",
                          "metadata_storage_path": "path-1"
                        }
                      ]
                    }
                    """);
                }

                if (uri.Contains("/openai/v1/responses", StringComparison.OrdinalIgnoreCase))
                {
                    return Json(HttpStatusCode.OK, """
                    {
                      "output_text": "{\"message\":\"Para iniciar un reintegro, ingresá a la app My Assist Card, cargá el gasto y adjuntá la documentación requerida.\",\"options\":[],\"continue\":true,\"isValid\":true,\"error\":null,\"uploadContext\":null}"
                    }
                    """);
                }

                throw new InvalidOperationException($"Unexpected request: {uri}");
            });

            var adapter = CrearAdapter(handler);
            var response = await adapter.EnviarAsync(
                "user-1",
                "es",
                "Que requisitos necesito?",
                new IaRequestContext { Intent = "RequisitosYPasosIniciarReintegro", Question = "Que requisitos necesito?" });

            Assert.True(response.IsValid);
            Assert.Equal("Para iniciar un reintegro, ingresá a la app My Assist Card, cargá el gasto y adjuntá la documentación requerida.", response.Message);
            Assert.Equal(3, handler.Requests.Count);
            Assert.Contains(handler.Requests, r => r.Uri.Contains("/knowledgebases/", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(handler.Requests, r => r.Uri.Contains("/indexes/assistcard-reintegros-index/docs/search", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(handler.Requests, r => r.Uri.Contains("/openai/v1/responses", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task EnviarAsync_Documental_FallbackAlIndiceSinApiKeyUsaBearerToken()
        {
            var handler = new StubHttpMessageHandler((request, body) =>
            {
                var uri = request.RequestUri!.AbsoluteUri;

                if (uri.Contains("/knowledgebases/", StringComparison.OrdinalIgnoreCase))
                {
                    return Json(HttpStatusCode.BadRequest, """
                    {
                      "error": {
                        "message": "Semantic Search is not enabled for this service. Parameter name: queryType"
                      }
                    }
                    """);
                }

                if (uri.Contains("/indexes/assistcard-reintegros-index/docs/search", StringComparison.OrdinalIgnoreCase))
                {
                    return Json(HttpStatusCode.OK, """
                    {
                      "value": [
                        {
                          "@search.score": 2.4,
                          "content": "Texto de grounding de Azure AI Search.",
                          "metadata_storage_name": "faq.docx",
                          "metadata_storage_path": "path-1"
                        }
                      ]
                    }
                    """);
                }

                if (uri.Contains("/openai/v1/responses", StringComparison.OrdinalIgnoreCase))
                {
                    return Json(HttpStatusCode.OK, """
                    {
                      "output_text": "{\"message\":\"Respuesta redactada con grounding.\",\"options\":[],\"continue\":true,\"isValid\":true,\"error\":null,\"uploadContext\":null}"
                    }
                    """);
                }

                throw new InvalidOperationException($"Unexpected request: {uri}");
            });

            var adapter = CrearAdapter(
                handler,
                searchApiKey: string.Empty,
                credential: new StaticTokenCredential("search-token"));

            var response = await adapter.EnviarAsync(
                "user-1",
                "es",
                "Â¿QuÃ© requisitos necesito?",
                new IaRequestContext { Intent = "RequisitosYPasosIniciarReintegro", Question = "Â¿QuÃ© requisitos necesito?" });

            Assert.True(response.IsValid);
            Assert.Equal("Respuesta redactada con grounding.", response.Message);

            var searchRequests = handler.Requests
                .Where(r => r.Uri.Contains("search.example", StringComparison.OrdinalIgnoreCase))
                .ToList();

            Assert.Equal(2, searchRequests.Count);
            Assert.All(searchRequests, r =>
            {
                Assert.False(r.Headers.ContainsKey("api-key"));
                Assert.Equal("Bearer search-token", r.Headers["Authorization"]);
            });

            var responsesRequest = Assert.Single(handler.Requests.Where(r => r.Uri.Contains("/openai/v1/responses", StringComparison.OrdinalIgnoreCase)));
            Assert.DoesNotContain("agent_reference", responsesRequest.Body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("tool_choice", responsesRequest.Body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("mcp_approval_response", responsesRequest.Body, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task EnviarAsync_NoDocumental_UsaResponsesSinAgentReference()
        {
            var handler = new StubHttpMessageHandler((request, body) =>
            {
                var uri = request.RequestUri!.AbsoluteUri;
                if (uri.Contains("/openai/v1/responses", StringComparison.OrdinalIgnoreCase))
                {
                    return Json(HttpStatusCode.OK, """
                    {
                      "output_text": "{\"message\":\"Contame un poco más del problema para ayudarte mejor.\",\"options\":[],\"continue\":true,\"isValid\":true,\"error\":null,\"uploadContext\":null}"
                    }
                    """);
                }

                throw new InvalidOperationException($"Unexpected request: {uri}");
            });

            var adapter = CrearAdapter(handler);
            var response = await adapter.EnviarAsync(
                "user-1",
                "es",
                "Tengo un problema",
                new IaRequestContext { Intent = "AnalizarProblemaGeneral", Question = "Tengo un problema" });

            Assert.True(response.IsValid);
            Assert.Equal("Contame un poco más del problema para ayudarte mejor.", response.Message);
            var request = Assert.Single(handler.Requests);
            Assert.Contains("\"model\":\"gpt-4o\"", request.Body, StringComparison.Ordinal);
            Assert.DoesNotContain("agent_reference", request.Body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("NUNCA", request.Body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("NEVER", request.Body, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ExplicarEstadoAsync_PendingPaymentFallbackDescribePago()
        {
            var handler = new StubHttpMessageHandler((request, _) =>
                Json(HttpStatusCode.TooManyRequests, """
                {
                  "error": {
                    "message": "Too Many Requests"
                  }
                }
                """));

            var adapter = CrearAdapter(handler);
            var response = await adapter.ExplicarEstadoAsync(
                "Pending Payment",
                "Health care expenses",
                "es");

            Assert.Contains("proceso de pago", response, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("documentaci", response, StringComparison.OrdinalIgnoreCase);
        }

        private static FoundryAgentAdapter CrearAdapter(
            HttpMessageHandler handler,
            string searchApiKey = "search-key",
            TokenCredential? credential = null)
        {
            return new FoundryAgentAdapter(
                new HttpClient(handler),
                Options.Create(new FoundryOpciones
                {
                    Endpoint = "https://foundry.example/api/projects/demo",
                    ApiKey = "foundry-key",
                    ModelDeployment = "gpt-4o"
                }),
                Options.Create(new SearchOpciones
                {
                    Endpoint = "https://search.example",
                    ApiKey = searchApiKey,
                    KnowledgeBaseName = "kb-demo",
                    IndexName = "assistcard-reintegros-index"
                }),
                NullLogger<FoundryAgentAdapter>.Instance,
                credential ?? new StaticTokenCredential("default-token"));
        }

        private static HttpResponseMessage Json(HttpStatusCode statusCode, string json)
            => new(statusCode)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

        private sealed class StubHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, string, HttpResponseMessage> _responder;

            public StubHttpMessageHandler(Func<HttpRequestMessage, string, HttpResponseMessage> responder)
            {
                _responder = responder;
            }

            public List<RecordedRequest> Requests { get; } = new();

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var body = request.Content == null
                    ? string.Empty
                    : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                Requests.Add(new RecordedRequest(
                    request.Method.Method,
                    request.RequestUri?.AbsoluteUri ?? string.Empty,
                    body,
                    request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value))));

                return _responder(request, body);
            }
        }

        private sealed record RecordedRequest(
            string Method,
            string Uri,
            string Body,
            Dictionary<string, string> Headers);

        private sealed class StaticTokenCredential : TokenCredential
        {
            private readonly string _token;

            public StaticTokenCredential(string token)
            {
                _token = token;
            }

            public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
                => new(_token, DateTimeOffset.UtcNow.AddHours(1));

            public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
                => new(GetToken(requestContext, cancellationToken));
        }
    }
}
