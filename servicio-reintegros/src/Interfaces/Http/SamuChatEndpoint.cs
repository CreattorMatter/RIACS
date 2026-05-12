using FastEndpoints;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using ServicioReintegros.AssistCard.Aplicacion.Dtos;
using ServicioReintegros.AssistCard.Aplicacion.Servicios;

namespace ServicioReintegros.AssistCard.Interfaces.Http
{
    // Endpoint opcional estilo Samu: recibe modelName + message y devuelve outputMessage + continue
    public sealed class SamuChatEndpoint : Endpoint<SamuChatRequest, SamuChatResponse>
    {
        private readonly OrquestadorConversacion _orquestador;

        public SamuChatEndpoint(OrquestadorConversacion orquestador)
        {
            _orquestador = orquestador;
        }

        public override void Configure()
        {
            Post("/api/chatbot/Agent/Reintegros");
            AllowAnonymous();
        }

        public override async Task HandleAsync(SamuChatRequest req, CancellationToken ct)
        {
            var phone = HttpContext.Request.Headers["x-whatsapp-phone-number"].ToString();
            var convId = HttpContext.Request.Headers["x-whatsapp-conversation-id"].ToString();
            var lang = HttpContext.Request.Headers["x-whatsapp-language"].ToString();

            var locale = NormalizarLocale(lang);

            var sessionKey = ConstruirSessionKey(convId, phone);

            string textoAEnviar;

            if (req.Archivos != null && req.Archivos.Count > 0)
            {
                var archivos = req.Archivos
                    .Select(a => (a.Nombre, a.ContentType, a.ContenidoBase64));
                textoAEnviar = await _orquestador.ProcesarUploadBase64Async(sessionKey, archivos, locale, ct);
            }
            else
            {
                var userMsg = (req.Message ?? string.Empty).Trim();
                textoAEnviar = await _orquestador.ProcesarTurnoApiAsync(sessionKey, userMsg, locale, ct);
            }

            var cont = !EsMensajeCierre(textoAEnviar);

            var resp = new SamuChatResponse
            {
                Data = new SamuChatData
                {
                    OutputMessage = textoAEnviar,
                    Continue = cont
                },
                IsValid = true,
                Error = null
            };

            await SendAsync(resp, cancellation: ct);
        }

        private static string ConstruirSessionKey(string convId, string phone)
        {
            var c = (convId ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(c))
                return "samu:" + c;

            var p = (phone ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(p))
                return "samu:phone:" + p;

            return "samu:anon:" + Guid.NewGuid().ToString("N");
        }

        private static string NormalizarLocale(string? lang)
        {
            var raw = (lang ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(raw))
                return "es";

            var token = raw.Split(new[] { '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
            var head = (token.Length > 0 ? token[0] : raw).Trim().ToLowerInvariant();

            if (head == "br") return "pt";
            if (head.StartsWith("pt")) return "pt";
            if (head.StartsWith("en")) return "en";
            if (head.StartsWith("es")) return "es";
            return "es";
        }

        private static bool EsMensajeCierre(string texto)
        {
            var t = (texto ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(t))
                return false;

            return t.Contains("La conversación ha finalizado", StringComparison.OrdinalIgnoreCase)
                || t.Contains("The conversation has ended", StringComparison.OrdinalIgnoreCase)
                || t.Contains("A conversa foi encerrada", StringComparison.OrdinalIgnoreCase);
        }
    }
}
