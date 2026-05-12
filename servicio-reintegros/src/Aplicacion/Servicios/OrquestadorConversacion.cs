using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using ServicioReintegros.AssistCard.Aplicacion.Dtos;
using ServicioReintegros.AssistCard.Aplicacion.Puertos;
using ServicioReintegros.AssistCard.Dominio.Entidades;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServicioReintegros.AssistCard.Configuracion;
using ServicioReintegros.AssistCard.Aplicacion.Servicios.StateHandlers;

namespace ServicioReintegros.AssistCard.Aplicacion.Servicios
{
    // Orquesta el flujo de mensajes entrantes y delega a casos de uso
    public sealed class OrquestadorConversacion
    {
        private static readonly ConcurrentDictionary<string, (SemaphoreSlim Sem, long LastUsedTicks)> SemaforosPorTelefono = new();
        private static readonly AsyncLocal<CapturaSalida?> CapturaSalidaActual = new();
        private static long _ultimaLimpiezaSemaforos = Environment.TickCount64;
        private const long IntervaloLimpiezaSemaforosMs = 5 * 60 * 1000; // 5 min
        private const long TtlSemaforosMs = 30 * 60 * 1000; // 30 min

        private readonly IProveedorWhatsApp _wa;
        private readonly IProveedorReintegros _rein;
        private readonly IProveedorTraduccion _trad;
        private readonly IProveedorIA _ia;
        private readonly IRegistroConversaciones _logger;
        private readonly ILogger<OrquestadorConversacion> _log;
        private readonly WabaOpciones _waba;
        private readonly ICacheAplicacion _cache;
        private readonly IReintegrosLocalizer _reintegrosLocalizer;
        private readonly SesionManager _sesionMgr;
        private readonly StateDispatcher _stateDispatcher = new();

        public OrquestadorConversacion(IProveedorWhatsApp wa, IProveedorReintegros rein, IProveedorTraduccion trad, IProveedorIA ia, IRegistroConversaciones logger, ILogger<OrquestadorConversacion> log, IOptions<WabaOpciones> waba, ICacheAplicacion cache, IReintegrosLocalizer reintegrosLocalizer, SesionManager sesionMgr)
        {
            _wa = wa;
            _rein = rein;
            _trad = trad;
            _ia = ia;
            _logger = logger;
            _log = log;
            _waba = waba.Value;
            _cache = cache;
            _reintegrosLocalizer = reintegrosLocalizer;
            _sesionMgr = sesionMgr;
        }

        public async Task<string> ProcesarTurnoApiAsync(string sessionKey, string texto, string locale, CancellationToken ct)
        {
            var key = (sessionKey ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(key))
                key = Guid.NewGuid().ToString("N");

            var textoIn = (texto ?? string.Empty).Trim();
            var loc = (locale ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(loc))
                loc = "es";

            var captura = new CapturaSalida();
            CapturaSalidaActual.Value = captura;

            try
            {
                await GuardarLocaleAsync(key, loc, ct);
                var req = ConstruirWebhookSintetico(key, textoIn);
                await ProcesarEventoAsync(req, ct);
                return captura.TextoRespuesta ?? string.Empty;
            }
            finally
            {
                CapturaSalidaActual.Value = null;
            }
        }

        public async Task<string> ProcesarUploadBase64Async(
            string sessionKey,
            IEnumerable<(string Nombre, string ContentType, string ContenidoBase64)> archivosBase64,
            string locale,
            CancellationToken ct)
        {
            var key = (sessionKey ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(key))
                return BotMessages.ArchivoSinContexto(locale);

            var loc = (locale ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(loc))
                loc = "es";

            UploadContext? uploadCtx = null;
            try
            {
                var cacheKey = $"uploadctx:{key}";
                var json = await _cache.ObtenerAsync(cacheKey, ct);
                if (!string.IsNullOrWhiteSpace(json))
                    uploadCtx = JsonSerializer.Deserialize<UploadContext>(json);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Cache.ObtenerUploadCtx fallo sessionKey={SessionKey}", key);
            }

            if (uploadCtx == null || !uploadCtx.BenefitRequestId.HasValue || !uploadCtx.DocumentId.HasValue)
                return BotMessages.ArchivoSinContexto(loc);

            var listaStreams = new List<System.IO.MemoryStream>();
            try
            {
                var archivos = new List<(int DocumentId, string Nombre, string ContentType, System.IO.Stream Contenido)>();
                foreach (var a in archivosBase64)
                {
                    byte[] bytes;
                    try
                    {
                        bytes = Convert.FromBase64String(a.ContenidoBase64);
                    }
                    catch (FormatException ex)
                    {
                        _log.LogWarning(ex, "Base64 invalido archivo={Nombre} sessionKey={SessionKey}", a.Nombre, key);
                        return BotMessages.ErrorLeerArchivo(loc);
                    }

                    var ms = new System.IO.MemoryStream(bytes);
                    listaStreams.Add(ms);
                    archivos.Add((
                        uploadCtx.DocumentId.Value,
                        string.IsNullOrWhiteSpace(a.Nombre) ? "documento" : a.Nombre,
                        string.IsNullOrWhiteSpace(a.ContentType) ? "application/octet-stream" : a.ContentType,
                        ms));
                }

                try
                {
                    await _rein.AgregarDocumentosAsync(uploadCtx.BenefitRequestId.Value, archivos, ct);
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "AgregarDocumentos fallo BenefitRequestId={BrId} sessionKey={SessionKey}", uploadCtx.BenefitRequestId, key);
                    return BotMessages.ErrorAdjuntarArchivo(loc);
                }
            }
            finally
            {
                foreach (var ms in listaStreams)
                    ms.Dispose();
            }

            // Post-upload: limpiar contexto y transicionar sesión
            try
            {
                var sesion = await ObtenerSesionAsync(key, ct) ?? new SesionConversacion();
                sesion.Estado = EstadoConversacion.ReintegroPendingDocsAskContinue;
                await GuardarSesionAsync(key, sesion, ct);

                try
                {
                    await _cache.GuardarAsync($"uploadctx:{key}", string.Empty, TimeSpan.FromSeconds(1), ct);
                }
                catch (Exception exCache)
                {
                    _log.LogDebug(exCache, "Cache.LimpiarUploadCtx fallo sessionKey={SessionKey}", key);
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "ProcesarUploadBase64 post-upload fallo sessionKey={SessionKey}", key);
            }

            return BotMessages.DocumentoRecibidoOk(loc) + "\n\n" + BotMessages.PreguntaContinuar(loc);
        }

        private static WebhookRequest ConstruirWebhookSintetico(string from, string texto)
        {
            return new WebhookRequest
            {
                Object = "whatsapp_business_account",
                Entry = new List<WebhookEntry>
                {
                    new WebhookEntry
                    {
                        Changes = new List<WebhookChange>
                        {
                            new WebhookChange
                            {
                                Field = "messages",
                                Value = new WebhookValue
                                {
                                    Contacts = new List<WebhookContact>
                                    {
                                        new WebhookContact { WaId = from }
                                    },
                                    Messages = new List<WebhookMessage>
                                    {
                                        new WebhookMessage
                                        {
                                            From = from,
                                            Type = "text",
                                            Text = new WebhookText { Body = texto }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            };
        }

        public async Task<WebhookResponse> ProcesarEventoAsync(WebhookRequest request, CancellationToken ct)
        {
            var msg = request?.Entry?.FirstOrDefault()?.Changes?.FirstOrDefault()?.Value?.Messages?.FirstOrDefault();
            var from = msg?.From;
            var waId = request?.Entry?.FirstOrDefault()?.Changes?.FirstOrDefault()?.Value?.Contacts?.FirstOrDefault()?.WaId;
            var destinoRespuesta = string.IsNullOrWhiteSpace(waId) ? from : waId;
            var tipo = msg?.Type;

            _log.LogInformation("WA inbound. from={From} wa_id={WaId} destinoRespuesta={Destino} tipo={Tipo}", from, waId, destinoRespuesta, tipo);

            if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(tipo))
                return new WebhookResponse { Exito = true, Mensaje = "Sin contenido procesable" };

            if (msg == null)
                return new WebhookResponse { Exito = true, Mensaje = "Sin contenido procesable" };

            WebhookMessage msgNoNull = msg;

            var now = Environment.TickCount64;
            var entry = SemaforosPorTelefono.AddOrUpdate(
                from,
                _ => (new SemaphoreSlim(1, 1), now),
                (_, old) => (old.Sem, now));
            var sem = entry.Sem;

            // Evicción periódica de semáforos inactivos (cada 5 min, TTL 30 min)
            if (now - Interlocked.Read(ref _ultimaLimpiezaSemaforos) > IntervaloLimpiezaSemaforosMs)
            {
                Interlocked.Exchange(ref _ultimaLimpiezaSemaforos, now);
                foreach (var kv in SemaforosPorTelefono)
                {
                    if (now - kv.Value.LastUsedTicks > TtlSemaforosMs && kv.Value.Sem.CurrentCount > 0)
                        SemaforosPorTelefono.TryRemove(kv.Key, out _);
                }
            }

            await sem.WaitAsync(ct);
            try
            {
                var msgId = msg?.Id;
                if (!string.IsNullOrWhiteSpace(msgId))
                {
                    if (await _sesionMgr.EsMensajeDuplicadoAsync(msgId, ct))
                        return new WebhookResponse { Exito = true, Mensaje = "Duplicado" };
                }

                if (string.Equals(tipo, "image", StringComparison.OrdinalIgnoreCase) || string.Equals(tipo, "document", StringComparison.OrdinalIgnoreCase))
                {
                    await ProcesarMediaAsync(msgNoNull, from, destinoRespuesta!, tipo, ct);
                    return new WebhookResponse { Exito = true, Mensaje = "Media procesada" };
                }

                var texto = msg?.Text?.Body?.Trim();

                if (string.IsNullOrWhiteSpace(texto))
                    return new WebhookResponse { Exito = true, Mensaje = "Sin contenido procesable" };

                var textoSample = texto.Length <= 350 ? texto : texto.Substring(0, 350);
                _log.LogInformation("WA inbound text. from={From} msgId={MsgId} textSample={TextSample}", from, msg?.Id, textoSample);

                var inicio = DateTime.UtcNow;

                string locale;
                var forcedLocale = CommandDetector.ObtenerLocaleForzado(texto);
                if (!string.IsNullOrWhiteSpace(forcedLocale))
                {
                    locale = forcedLocale;
                    await GuardarLocaleAsync(from!, locale, ct);
                }
                else
                {
                    var cachedLocale = await ObtenerLocaleAsync(from!, ct);
                    if (!string.IsNullOrWhiteSpace(cachedLocale))
                    {
                        locale = cachedLocale;
                    }
                    else
                    {
                        if (EsTextoAmbiguoParaDeteccion(texto))
                        {
                            locale = "es";
                        }
                        else
                        {
                            try { locale = await _trad.DetectarAsync(texto, ct); } catch { locale = "es"; }
                        }

                        await GuardarLocaleAsync(from!, locale, ct);
                    }
                }

                var textoNormalizado = texto.Trim();
                var sesion = await ObtenerSesionAsync(from!, ct) ?? new SesionConversacion();

                if (sesion.Estado == EstadoConversacion.Menu)
                    sesion.Estado = string.IsNullOrWhiteSpace(sesion.Nombre) ? EstadoConversacion.AwaitingName : EstadoConversacion.MenuPrincipal;
                if (sesion.Estado == EstadoConversacion.ShowingReintegro)
                    sesion.Estado = EstadoConversacion.ReintegroMenu;

                var cmd = TextNormalizer.Normalizar(textoNormalizado);
                if (CommandDetector.EsComandoMenu(cmd) || CommandDetector.EsComandoCancelar(cmd))
                {
                    var nombreAnterior = sesion.Nombre;
                    await ResetSesionAsync(from!, ct);

                    var sesionNueva = new SesionConversacion();
                    if (!string.IsNullOrWhiteSpace(nombreAnterior))
                    {
                        sesionNueva.Nombre = nombreAnterior;
                        sesionNueva.Estado = EstadoConversacion.MenuPrincipal;
                    }

                    await GuardarSesionAsync(from!, sesionNueva, ct);

                    var menuMsg = sesionNueva.Estado == EstadoConversacion.MenuPrincipal
                        ? ConstruirMenuPrincipal(locale, sesionNueva.Nombre)
                        : ConstruirMensajePedirNombre(locale);
                    await ResponderYRegistrarAsync(from!, destinoRespuesta!, msg?.Id, locale, textoNormalizado, menuMsg, inicio, ct);
                    return new WebhookResponse { Exito = true, Mensaje = "Procesado" };
                }

                if (CommandDetector.EsComandoAtras(cmd))
                {
                    var msgAtras = ProcesarAtras(sesion, locale);
                    await GuardarSesionAsync(from!, sesion, ct);
                    await ResponderYRegistrarAsync(from!, destinoRespuesta!, msg?.Id, locale, textoNormalizado, msgAtras, inicio, ct);
                    return new WebhookResponse { Exito = true, Mensaje = "Procesado" };
                }

                if (sesion.Estado == EstadoConversacion.Ended)
                {
                    var msgEnded = LocaleHelper.Loc(locale,
                        "La conversación ha finalizado. Escribí 'menú' para empezar de nuevo.",
                        "The conversation has ended. Write 'menu' to start again.",
                        "A conversa foi encerrada. Escreva 'menu' para começar de novo.");
                    await ResponderYRegistrarAsync(from!, destinoRespuesta!, msg?.Id, locale, textoNormalizado, msgEnded, inicio, ct);
                    return new WebhookResponse { Exito = true, Mensaje = "Procesado" };
                }

                // ── ARQUITECTURA DE DISPATCH ──
                // Grupo 1: Estados donde el input se interpreta SOLO segun el estado
                //   (nombre, seleccion de menu, ingreso de identificador)
                //
                // Entre grupos: Comandos globales (estado/pagos) y deteccion de
                //   identificadores cross-state.
                //
                // Grupo 2: Estados donde el handler se ejecuta SOLO si ningun
                //   comando global coincidio.
                //
                // Para agregar un nuevo estado, registrar el handler en StateDispatcher
                // y agregar el estado al grupo correspondiente.

                // ── State Handlers: Group 1 (pre-crosscutting) ──
                if (sesion.Estado is EstadoConversacion.AwaitingName
                    or EstadoConversacion.MenuPrincipal
                    or EstadoConversacion.OtrasConsultasMenu
                    or EstadoConversacion.OtrasConsultasProblem
                    or EstadoConversacion.AwaitingIdentifier)
                {
                    try
                    {
                        var handlerCtx = BuildHandlerContext(from!, destinoRespuesta!, textoNormalizado, sesion, msg?.Id, locale, inicio);
                        var stateResult = await _stateDispatcher.DispatchAsync(handlerCtx, ct);
                        if (stateResult != null)
                        {
                            await ResponderYRegistrarAsync(from!, destinoRespuesta!, msg?.Id, locale, textoNormalizado, stateResult.Mensaje, inicio, ct);
                            return new WebhookResponse { Exito = true, Mensaje = "Procesado" };
                        }
                    }
                    catch (Exception ex)
                    {
                        _log.LogError(ex, "StateHandler(G1) fallo estado={Estado} telefono={Telefono}", sesion.Estado, from);
                    }
                }

                if (CommandDetector.EsComandoEstado(cmd) || CommandDetector.EsComandoPagos(cmd))
                {
                    var reintegroActual = await ObtenerReintegroActualAsync(from!, ct);

                    _log.LogInformation(
                        "CmdReintegro. cmd={Cmd} sesionEstado={SesionEstado} hasReintegro={HasReintegro} benefitId={BenefitId} caseId={CaseId} locale={Locale}",
                        cmd,
                        sesion.Estado,
                        reintegroActual != null,
                        reintegroActual?.BenefitId,
                        reintegroActual?.CaseId,
                        locale);

                    if (reintegroActual == null)
                    {
                        sesion.Estado = EstadoConversacion.AwaitingIdentifier;
                        sesion.CurrentBenefitId = null;
                        sesion.CurrentCaseId = null;
                        await GuardarSesionAsync(from!, sesion, ct);

                        var msgSinCtx = BotMessages.MensajeSinReintegro(locale);
                        await ResponderYRegistrarAsync(from!, destinoRespuesta!, msg?.Id, locale, textoNormalizado, msgSinCtx, inicio, ct);
                        return new WebhookResponse { Exito = true, Mensaje = "Procesado" };
                    }

                    _log.LogInformation(
                        "CmdReintegro.BeforeLocalize. cmd={Cmd} status={Status} benefitType={BenefitType}",
                        cmd,
                        reintegroActual.Status,
                        reintegroActual.BenefitType);

                    await _reintegrosLocalizer.LocalizarAsync(reintegroActual, locale, ct);
                    await GuardarReintegroActualAsync(from!, reintegroActual, ct);

                    _log.LogInformation(
                        "CmdReintegro.AfterLocalize. cmd={Cmd} status={Status} benefitType={BenefitType}",
                        cmd,
                        reintegroActual.Status,
                        reintegroActual.BenefitType);

                    var respuestaCmd = CommandDetector.EsComandoEstado(cmd)
                        ? BotMessages.RespuestaEstado(reintegroActual, locale)
                        : BotMessages.RespuestaPagos(reintegroActual, locale);

                    await ResponderYRegistrarAsync(from!, destinoRespuesta!, msg?.Id, locale, textoNormalizado, respuestaCmd, inicio, ct);
                    return new WebhookResponse { Exito = true, Mensaje = "Procesado" };
                }

                if (sesion.Estado != EstadoConversacion.ReintegroProblem
                    && sesion.Estado != EstadoConversacion.OtrasConsultasProblem
                    && TryConstruirConsultaIdentificador(textoNormalizado, out var consulta))
                {
                    if (sesion.Estado == EstadoConversacion.MenuPrincipal)
                        sesion.Estado = EstadoConversacion.AwaitingIdentifier;

                    var respuestaReintegros = await ProcesarConsultaReintegroAsync(from!, destinoRespuesta!, textoNormalizado, consulta!, sesion, msg?.Id, locale, inicio, ct);
                    return respuestaReintegros;
                }

                // ── State Handlers: Group 2 (post-crosscutting) ──
                try
                {
                    var handlerCtx2 = BuildHandlerContext(from!, destinoRespuesta!, textoNormalizado, sesion, msg?.Id, locale, inicio);
                    var stateResult2 = await _stateDispatcher.DispatchAsync(handlerCtx2, ct);
                    if (stateResult2 != null)
                    {
                        await ResponderYRegistrarAsync(from!, destinoRespuesta!, msg?.Id, locale, textoNormalizado, stateResult2.Mensaje, inicio, ct);
                        return new WebhookResponse { Exito = true, Mensaje = "Procesado" };
                    }
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "StateHandler(G2) fallo estado={Estado} telefono={Telefono}", sesion.Estado, from);
                }

                var fallbackDeterministico = await ConstruirFallbackDeterministicoAsync(from!, sesion, locale, ct);
                if (!string.IsNullOrWhiteSpace(fallbackDeterministico))
                {
                    await GuardarSesionAsync(from!, sesion, ct);
                    await ResponderYRegistrarAsync(from!, destinoRespuesta!, msg?.Id, locale, textoNormalizado, fallbackDeterministico, inicio, ct);
                    return new WebhookResponse { Exito = true, Mensaje = "Procesado" };
                }

                var historial = await ObtenerHistorialAsync(from!, ct);

                AgentResponse respuesta;

                try
                {
                    ResumenReintegro? reintegroActual = null;
                    if (sesion.Estado == EstadoConversacion.ReintegroMenu
                        || sesion.Estado == EstadoConversacion.ReintegroFinancialMenu
                        || sesion.Estado == EstadoConversacion.ReintegroPaymentsMenu
                        || sesion.Estado == EstadoConversacion.ReintegroPendingDocsSelect
                        || sesion.Estado == EstadoConversacion.ReintegroPendingDocsAction
                        || sesion.Estado == EstadoConversacion.ReintegroPendingDocsAskContinue
                        || sesion.Estado == EstadoConversacion.ReintegroProblem
                        || sesion.Estado == EstadoConversacion.ReintegroExit)
                        reintegroActual = await ObtenerReintegroActualAsync(from!, ct);

                    var contexto = new IaRequestContext
                    {
                        Canal = "whatsapp",
                        Telefono = from,
                        Locale = locale,
                        Estado = sesion.Estado.ToString(),
                        LastOptions = sesion.LastOptions,
                        Reintegro = reintegroActual,
                        Historial = historial,
                        Question = texto
                    };

                    respuesta = await _ia.EnviarAsync(from, locale, texto, contexto, ct);
                }
                catch
                {
                    await IntentarEnviarAsync(destinoRespuesta!, BotMessages.ServicioNoDisponible(locale), ct);
                    return new WebhookResponse { Exito = true, Mensaje = "Procesado con error proveedor IA" };
                }

                if (respuesta.UploadContext != null && respuesta.UploadContext.BenefitRequestId.HasValue && respuesta.UploadContext.DocumentId.HasValue)
                {
                    try
                    {
                        var key = $"uploadctx:{from}";
                        var jsonCtx = JsonSerializer.Serialize(respuesta.UploadContext);
                        await _cache.GuardarAsync(key, jsonCtx, TimeSpan.FromMinutes(30), ct);
                    }
                    catch (Exception ex)
                    {
                        _log.LogWarning(ex, "Cache.GuardarUploadCtx(IA) fallo telefono={Telefono}", from);
                    }
                }

                var textoAEnviar = BotMessages.ConstruirMensajeWhatsApp(respuesta, locale);

                // Safety net: si la IA respondió en un idioma distinto al del usuario, traducir
                textoAEnviar = await AsegurarIdiomaRespuestaAsync(textoAEnviar, locale, ct);

                sesion.LastOptions = respuesta.Options ?? new List<string>();
                await GuardarSesionAsync(from!, sesion, ct);

                await IntentarEnviarAsync(destinoRespuesta!, textoAEnviar, ct);

                try
                {
                    await AgregarHistorialAsync(from!, "user", texto, ct);
                    await AgregarHistorialAsync(from!, "assistant", textoAEnviar, ct);
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "AgregarHistorial fallo telefono={Telefono}", from);
                }

                await RegistrarConversacionSiEsPosibleAsync(from, msg?.Id, locale, texto, textoAEnviar, inicio, ct);
                return new WebhookResponse { Exito = true, Mensaje = "Procesado" };
            }
            finally
            {
                sem.Release();
            }
        }

        // ── Build handler context para StateDispatcher ──
        private StateHandlerContext BuildHandlerContext(
            string from, string destinoRespuesta, string textoUsuario,
            SesionConversacion sesion, string? msgId, string locale, DateTime inicio)
        {
            return new StateHandlerContext
            {
                Telefono = from,
                DestinoRespuesta = destinoRespuesta,
                TextoUsuario = textoUsuario,
                Locale = locale,
                MsgId = msgId,
                Inicio = inicio,
                Sesion = sesion,
                Ia = _ia,
                Reintegros = _rein,
                Localizer = _reintegrosLocalizer,
                Log = _log,
                ObtenerSesion = ObtenerSesionAsync,
                GuardarSesion = GuardarSesionAsync,
                ObtenerReintegroActual = ObtenerReintegroActualAsync,
                GuardarReintegroActual = GuardarReintegroActualAsync,
                ObtenerListaReintegros = ObtenerListaReintegrosAsync,
                GuardarListaReintegros = GuardarListaReintegrosAsync,
                IntentarEnviar = IntentarEnviarAsync,
                ResponderYRegistrar = (tel, dest, mid, loc, txtIn, txtOut, ini, ct2)
                    => ResponderYRegistrarAsync(tel, dest, mid, loc, txtIn, txtOut, ini, ct2),
                GuardarUploadContext = async (tel, up, ct2) =>
                {
                    var key = $"uploadctx:{tel}";
                    var json = JsonSerializer.Serialize(up);
                    await _cache.GuardarAsync(key, json, TimeSpan.FromMinutes(30), ct2);
                }
            };
        }

        // Delegados a SesionManager
        private Task<List<HistorialItem>> ObtenerHistorialAsync(string telefono, CancellationToken ct)
            => _sesionMgr.ObtenerHistorialAsync(telefono, ct);

        private Task AgregarHistorialAsync(string telefono, string rol, string texto, CancellationToken ct)
            => _sesionMgr.AgregarHistorialAsync(telefono, rol, texto, ct);

        private async Task<WebhookResponse> ProcesarConsultaReintegroAsync(
            string telefono,
            string destinoRespuesta,
            string textoUsuario,
            ConsultaIdentificador consulta,
            SesionConversacion sesion,
            string? msgId,
            string locale,
            DateTime inicio,
            CancellationToken ct)
        {
            List<ResumenReintegro> resultados;
            var swProv = Stopwatch.StartNew();
            try
            {
                var items = await _rein.BuscarPorIdentificadorAsync(consulta, ct);
                swProv.Stop();
                resultados = items?.ToList() ?? new List<ResumenReintegro>();
            }
            catch (Exception ex)
            {
                swProv.Stop();
                _log.LogError(
                    ex,
                    "ConsultaReintegro.Error traceId={TraceId} telefono={Telefono} msgId={MsgId} elapsedMs={ElapsedMs} hasBenefitRequestId={HasBenefitRequestId} hasCaseId={HasCaseId} hasEmail={HasEmail} hasVoucherCode={HasVoucherCode} countryIsoCode={CountryIsoCode}",
                    Activity.Current?.TraceId.ToString(),
                    telefono,
                    msgId,
                    swProv.ElapsedMilliseconds,
                    consulta.BenefitRequestId.HasValue,
                    !string.IsNullOrWhiteSpace(consulta.CaseId),
                    !string.IsNullOrWhiteSpace(consulta.Email),
                    !string.IsNullOrWhiteSpace(consulta.VoucherCode),
                    consulta.CountryIsoCode);
                await ResponderYRegistrarAsync(telefono, destinoRespuesta, msgId, locale, textoUsuario, BotMessages.ErrorConsultarReintegro(locale), inicio, ct);
                return new WebhookResponse { Exito = true, Mensaje = "Procesado con error Reintegros" };
            }

            string textoAEnviar;

            if (resultados.Count == 0)
            {
                sesion.Estado = EstadoConversacion.AwaitingIdentifier;
                sesion.CurrentBenefitId = null;
                sesion.CurrentCaseId = null;
                await GuardarSesionAsync(telefono, sesion, ct);

                textoAEnviar = BotMessages.NoEncontreReintegro(locale);

                await ResponderYRegistrarAsync(telefono, destinoRespuesta, msgId, locale, textoUsuario, textoAEnviar, inicio, ct);
                return new WebhookResponse { Exito = true, Mensaje = "Procesado" };
            }

            if (resultados.Count > 1)
            {
                await GuardarListaReintegrosAsync(telefono, resultados, ct);
                sesion.Estado = EstadoConversacion.SelectingReintegro;
                sesion.CurrentBenefitId = null;
                sesion.CurrentCaseId = null;
                await GuardarSesionAsync(telefono, sesion, ct);

                textoAEnviar = ConstruirMensajeSeleccionReintegro(resultados, locale);
                await ResponderYRegistrarAsync(telefono, destinoRespuesta, msgId, locale, textoUsuario, textoAEnviar, inicio, ct);
                return new WebhookResponse { Exito = true, Mensaje = "Procesado" };
            }

            var elegido = resultados
                .OrderByDescending(r => r.CreateDate ?? DateTime.MinValue)
                .FirstOrDefault();

            if (elegido == null || !EsResumenValido(elegido))
            {
                sesion.Estado = EstadoConversacion.AwaitingIdentifier;
                sesion.CurrentBenefitId = null;
                sesion.CurrentCaseId = null;
                await GuardarSesionAsync(telefono, sesion, ct);

                textoAEnviar = BotMessages.NoEncontreRegistroValido(locale);

                await ResponderYRegistrarAsync(telefono, destinoRespuesta, msgId, locale, textoUsuario, textoAEnviar, inicio, ct);
                return new WebhookResponse { Exito = true, Mensaje = "Procesado" };
            }

            await _reintegrosLocalizer.LocalizarAsync(elegido, locale, ct);

            await GuardarReintegroActualAsync(telefono, elegido, ct);
            sesion.Estado = EstadoConversacion.ReintegroMenu;
            sesion.CurrentBenefitId = elegido.BenefitId;
            sesion.CurrentCaseId = elegido.CaseId;
            await GuardarSesionAsync(telefono, sesion, ct);

            textoAEnviar = ConstruirDetalleYMenuReintegro(elegido, locale, sesion.Nombre);
            await ResponderYRegistrarAsync(telefono, destinoRespuesta, msgId, locale, textoUsuario, textoAEnviar, inicio, ct);
            return new WebhookResponse { Exito = true, Mensaje = "Procesado" };
        }



        // Delegado a IdentificadorParser (clase pública y testeable)
        private static bool TryConstruirConsultaIdentificador(string texto, out ConsultaIdentificador? consulta)
            => IdentificadorParser.TryParse(texto, out consulta);

        private static bool EsResumenValido(ResumenReintegro r)
        {
            if (r == null) return false;
            if (!string.IsNullOrWhiteSpace(r.Status)) return true;
            if (!string.IsNullOrWhiteSpace(r.BenefitId)) return true;
            if (!string.IsNullOrWhiteSpace(r.CaseId)) return true;
            return false;
        }

        private Task<ResumenReintegro?> ObtenerReintegroActualAsync(string telefono, CancellationToken ct)
            => _sesionMgr.ObtenerReintegroActualAsync(telefono, ct);

        private async Task ResponderYRegistrarAsync(
            string telefono,
            string destinoRespuesta,
            string? msgId,
            string locale,
            string textoUsuario,
            string textoRespuesta,
            DateTime inicio,
            CancellationToken ct)
        {
            await IntentarEnviarAsync(destinoRespuesta, textoRespuesta, ct);
            await AgregarHistorialSiEsPosibleAsync(telefono, textoUsuario, textoRespuesta, ct);
            await RegistrarConversacionSiEsPosibleAsync(telefono, msgId, locale, textoUsuario, textoRespuesta, inicio, ct);
        }

        private Task<SesionConversacion?> ObtenerSesionAsync(string telefono, CancellationToken ct)
            => _sesionMgr.ObtenerSesionAsync(telefono, ct);

        private Task GuardarSesionAsync(string telefono, SesionConversacion sesion, CancellationToken ct)
            => _sesionMgr.GuardarSesionAsync(telefono, sesion, ct);

        private Task ResetSesionAsync(string telefono, CancellationToken ct)
            => _sesionMgr.ResetSesionAsync(telefono, ct);


        private static bool EstaEnRevision(ResumenReintegro r)
        {
            if (r == null) return false;
            var status = (r.StatusOriginal ?? r.Status ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(status)) return false;

            var norm = TextNormalizer.Normalizar(status);
            return norm.Contains("under review", StringComparison.OrdinalIgnoreCase)
                   || norm.Contains("received", StringComparison.OrdinalIgnoreCase)
                   || norm.Contains("en revision", StringComparison.OrdinalIgnoreCase)
                   || norm.Contains("en revisao", StringComparison.OrdinalIgnoreCase)
                   || norm.Contains("recebido", StringComparison.OrdinalIgnoreCase);
        }


        private static bool EsEstadoPagoPendiente(ResumenReintegro? r)
        {
            if (r == null) return false;
            var status = (r.StatusOriginal ?? r.Status ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(status)) return false;
            var norm = TextNormalizer.Normalizar(status);

            return norm.Contains("pending payment", StringComparison.OrdinalIgnoreCase)
                   || norm.Contains("pago pendiente", StringComparison.OrdinalIgnoreCase)
                   || norm.Contains("pago en proceso", StringComparison.OrdinalIgnoreCase)
                   || norm.Contains("pagamento pendente", StringComparison.OrdinalIgnoreCase)
                   || norm.Contains("pagamento em processo", StringComparison.OrdinalIgnoreCase);
        }





        private static string ProcesarAtras(SesionConversacion sesion, string locale)
        {
            if (sesion.Estado == EstadoConversacion.OtrasConsultasProblem)
            {
                sesion.Estado = EstadoConversacion.OtrasConsultasMenu;
                return ConstruirMenuOtrasConsultas(locale, sesion.Nombre);
            }

            if (sesion.Estado == EstadoConversacion.OtrasConsultasMenu)
            {
                sesion.Estado = EstadoConversacion.MenuPrincipal;
                return ConstruirMenuPrincipal(locale, sesion.Nombre);
            }

            if (sesion.Estado == EstadoConversacion.SelectingReintegro)
            {
                sesion.Estado = EstadoConversacion.AwaitingIdentifier;
                return ConstruirPedidoIdentificador(locale, sesion.Nombre);
            }

            if (sesion.Estado == EstadoConversacion.ReintegroFinancialMenu
                || sesion.Estado == EstadoConversacion.ReintegroPaymentsMenu
                || sesion.Estado == EstadoConversacion.ReintegroPendingDocsSelect
                || sesion.Estado == EstadoConversacion.ReintegroPendingDocsAction
                || sesion.Estado == EstadoConversacion.ReintegroPendingDocsAskContinue
                || sesion.Estado == EstadoConversacion.ReintegroProblem
                || sesion.Estado == EstadoConversacion.ReintegroExit)
            {
                if (sesion.Estado == EstadoConversacion.ReintegroExit && sesion.EstadoVolver.HasValue)
                {
                    var volver = sesion.EstadoVolver.Value;
                    sesion.EstadoVolver = null;
                    sesion.Estado = volver;
                    if (volver == EstadoConversacion.ReintegroFinancialMenu)
                        return BotMessages.MenuFinanciero(locale);
                }

                sesion.Estado = EstadoConversacion.ReintegroMenu;
                return locale.StartsWith("en")
                    ? "Back. Please choose an option from your reimbursement menu."
                    : locale.StartsWith("pt")
                        ? "Voltar. Escolha uma opção do menu do seu reembolso."
                        : "Volver. Elegí una opción del menú de tu reintegro.";
            }

            if (sesion.Estado == EstadoConversacion.ReintegroMenu)
            {
                sesion.Estado = EstadoConversacion.MenuPrincipal;
                sesion.CurrentBenefitId = null;
                sesion.CurrentCaseId = null;
                sesion.PendingDocIdSeleccionado = null;
                return ConstruirMenuPrincipal(locale, sesion.Nombre);
            }

            if (sesion.Estado == EstadoConversacion.AwaitingIdentifier)
            {
                sesion.Estado = EstadoConversacion.MenuPrincipal;
                sesion.CurrentBenefitId = null;
                sesion.CurrentCaseId = null;
                sesion.PendingDocIdSeleccionado = null;
                return ConstruirMenuPrincipal(locale, sesion.Nombre);
            }

            if (sesion.Estado == EstadoConversacion.MenuPrincipal)
                return ConstruirMenuPrincipal(locale, sesion.Nombre);

            if (sesion.Estado == EstadoConversacion.AwaitingName)
                return ConstruirMensajePedirNombre(locale);

            sesion.Estado = EstadoConversacion.MenuPrincipal;
            return ConstruirMenuPrincipal(locale, sesion.Nombre);
        }



        private static string ConstruirPedidoIdentificador(string locale)
            => ConstruirPedidoIdentificador(locale, null);

        private static string ConstruirPedidoIdentificador(string locale, string? nombre)
        {
            var pref = string.IsNullOrWhiteSpace(nombre) ? string.Empty : $"Excelente {nombre}! ";
            return pref + LocaleHelper.Loc(locale,
                "Por favor enviame uno de estos datos:\n- Nro Beneficio\n- Case ID (7 alfanumérico)\n- Email\n- Voucher (ej: AR12345)",
                "Please send one of these identifiers:\n- Benefit ID\n- Case ID (7 alphanumeric)\n- Email\n- Voucher (e.g. AR12345)",
                "Por favor envie um destes identificadores:\n- Benefício\n- Case ID (7 alfanuméricos)\n- Email\n- Voucher (ex: AR12345)");
        }

        private Task<List<ResumenReintegro>> ObtenerListaReintegrosAsync(string telefono, CancellationToken ct)
            => _sesionMgr.ObtenerListaReintegrosAsync(telefono, ct);

        private static string ConstruirMensajePedirNombre(string locale)
            => LocaleHelper.Loc(locale,
                "¡Hola! Soy Sam, tu asistente virtual de AssistCard. Para consultar sobre tu solicitud de reintegro, ¿me podrías indicar tu nombre completo?",
                "Hi! I'm Sam, your AssistCard virtual assistant. To help you with your reimbursement, please tell me your full name.",
                "Olá! Sou Sam, seu assistente virtual da AssistCard. Para consultar seu reembolso, você pode me informar seu nome completo?");

        private static List<string> OpcionesMenuPrincipal(string locale)
        {
            var b = LocaleHelper.ResolveBase(locale);
            return b switch
            {
                "en" => new List<string> { "Check reimbursement", "Other questions" },
                "pt" => new List<string> { "Consultar reembolso", "Outras consultas" },
                _ => new List<string> { "Consultar Reintegro", "Otras consultas" }
            };
        }

        private static string ConstruirMenuPrincipal(string locale, string? nombre)
        {
            var opciones = OpcionesMenuPrincipal(locale);
            var b = LocaleHelper.ResolveBase(locale);
            var nom = b switch
            {
                "en" => string.IsNullOrWhiteSpace(nombre) ? "Hi! " : $"Hi, {nombre}! ",
                "pt" => string.IsNullOrWhiteSpace(nombre) ? "Olá! " : $"Olá, {nombre}! ",
                _ => string.IsNullOrWhiteSpace(nombre) ? "" : $"¡Hola, {nombre}! "
            };
            var header = LocaleHelper.Loc(locale, "Selecciona una opcion:\n", "Select an option:\n", "Selecione uma opção:\n");
            return nom + header + BotMessages.FormatearOpcionesNumeradas(opciones);
        }

        private static List<string> OpcionesOtrasConsultas(string locale)
            => locale.StartsWith("en")
                ? new List<string>
                {
                    "Requirements and steps to start a reimbursement",
                    "Problems with the App",
                    "Describe an issue",
                    "Back to previous menu"
                }
                : locale.StartsWith("pt")
                    ? new List<string>
                    {
                        "Requisitos e passos para iniciar um reembolso",
                        "Problemas com o App",
                        "Descrever um problema",
                        "Voltar ao menu anterior"
                    }
                    : new List<string>
                    {
                        "Requisitos y pasos para iniciar un reintegro",
                        "Problemas con la App",
                        "Detallar un problema",
                        "Volver al menu anterior"
                    };

        private static string ConstruirMenuOtrasConsultas(string locale, string? nombre)
        {
            var opciones = OpcionesOtrasConsultas(locale);
            if (locale.StartsWith("en"))
            {
                var nom = string.IsNullOrWhiteSpace(nombre) ? string.Empty : $"Ok {nombre}! ";
                return nom + "Select one of the following options:\n" + BotMessages.FormatearOpcionesNumeradas(opciones);
            }
            if (locale.StartsWith("pt"))
            {
                var nom = string.IsNullOrWhiteSpace(nombre) ? string.Empty : $"Ok {nombre}! ";
                return nom + "Selecione uma das seguintes opções:\n" + BotMessages.FormatearOpcionesNumeradas(opciones);
            }

            var nomEs = string.IsNullOrWhiteSpace(nombre) ? "" : $"Bien {nombre}! ";
            return nomEs + "Selecciona una de las siguientes opciones:\n" + BotMessages.FormatearOpcionesNumeradas(opciones);
        }

        private static string ConstruirMensajeSeleccionReintegro(List<ResumenReintegro> lista, string locale)
        {
            var opciones = ConstruirOpcionesSeleccionReintegro(lista, locale);
            var header = locale.StartsWith("en")
                ? "Select one of the benefits:\n"
                : locale.StartsWith("pt")
                    ? "Selecione um dos benefícios:\n"
                    : "Seleccione uno de los beneficios:\n";
            return header + BotMessages.FormatearOpcionesNumeradas(opciones);
        }

        private static List<string> ConstruirOpcionesSeleccionReintegro(List<ResumenReintegro> lista, string locale)
        {
            var opciones = new List<string>();
            foreach (var r in lista)
            {
                var benefit = r.BenefitId ?? "-";
                var st = r.StatusOriginal ?? r.Status ?? "-";
                if (locale.StartsWith("en"))
                    opciones.Add($"Benefit: {benefit} - Status: {st}");
                else if (locale.StartsWith("pt"))
                    opciones.Add($"Benefício: {benefit} - Status: {st}");
                else
                    opciones.Add($"Beneficio: {benefit} - Estado: {st}");
            }
            opciones.Add(BotMessages.OpcionVolverMenuAnterior(locale));
            return opciones;
        }

        private static string ConstruirDetalleYMenuReintegro(ResumenReintegro r, string locale, string? nombre)
        {
            var detalle = ConstruirDetalleReintegro(r, locale);
            var nom = string.IsNullOrWhiteSpace(nombre) ? "" : $"{nombre} ";
            var opciones = BotMessages.ConstruirOpcionesReintegroMenu(r, locale);
            var header = locale.StartsWith("en")
                ? "you can perform the following actions on your reimbursement:\n"
                : locale.StartsWith("pt")
                    ? "você pode realizar as seguintes ações no seu reembolso:\n"
                    : "puedes realizar las siguientes operaciones sobre tu reintegro:\n";
            return detalle + "\n\n" + nom + header + BotMessages.FormatearOpcionesNumeradas(opciones);
        }

        private static string ConstruirDetalleReintegro(ResumenReintegro r, string locale)
        {
            var fecha = r.CreateDate.HasValue ? r.CreateDate.Value.ToString("dd/MM/yyyy") : "-";
            var solicitado = r.TotalRequestedUsd.HasValue ? r.TotalRequestedUsd.Value.ToString("0.##") : "-";
            var aprobado = r.TotalApprovedUsd.HasValue ? r.TotalApprovedUsd.Value.ToString("0.##") : "-";
            var denegado = r.TotalDeniedUsd.HasValue ? r.TotalDeniedUsd.Value.ToString("0.##") : "-";

            var pagoDetalle = r.PaymentDetails?.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p?.Detalle))?.Detalle;
            var paymentDetailsLine = !string.IsNullOrWhiteSpace(pagoDetalle)
                ? locale.StartsWith("en")
                    ? $"\nPayment details: {pagoDetalle}"
                    : locale.StartsWith("pt")
                        ? $"\nDetalhes do pagamento: {pagoDetalle}"
                        : $"\nDetalle de Pago: {pagoDetalle}"
                : string.Empty;

            var docsPendingLine = r.PendingDocuments != null && r.PendingDocuments.Count > 0
                ? locale.StartsWith("en")
                    ? $"\nPending documents: {r.PendingDocuments.Count}"
                    : locale.StartsWith("pt")
                        ? $"\nDocumentos pendentes: {r.PendingDocuments.Count}"
                        : $"\nDocumentos Pendientes: {r.PendingDocuments.Count}"
                : string.Empty;

            if (locale.StartsWith("en"))
                return "Reimbursement details:" +
                       $"\nBenefit: {r.BenefitId ?? "-"}" +
                       $"\nCase: {r.CaseId ?? "-"}" +
                       $"\nDate: {fecha}" +
                       $"\nType: {r.BenefitType ?? "-"}" +
                       $"\nStatus: {r.Status ?? "-"}" +
                       $"\nRequested (USD): {solicitado}" +
                       $"\nApproved (USD): {aprobado}" +
                       $"\nDenied (USD): {denegado}" +
                       paymentDetailsLine +
                       docsPendingLine;

            if (locale.StartsWith("pt"))
                return "Detalhes do reembolso:" +
                       $"\nBenefício: {r.BenefitId ?? "-"}" +
                       $"\nCaso: {r.CaseId ?? "-"}" +
                       $"\nData: {fecha}" +
                       $"\nTipo: {r.BenefitType ?? "-"}" +
                       $"\nStatus: {r.Status ?? "-"}" +
                       $"\nSolicitado (USD): {solicitado}" +
                       $"\nAprovado (USD): {aprobado}" +
                       $"\nNegado (USD): {denegado}" +
                       paymentDetailsLine +
                       docsPendingLine;

            return "Detalle de reintegro:" +
                   $"\nBeneficio: {r.BenefitId ?? "-"}" +
                   $"\nCaso: {r.CaseId ?? "-"}" +
                   $"\nFecha: {fecha}" +
                   $"\nTipo: {r.BenefitType ?? "-"}" +
                   $"\nEstado: {r.Status ?? "-"}" +
                   $"\nSolicitado (USD): {solicitado}" +
                   $"\nAprobado (USD): {aprobado}" +
                   $"\nDenegado (USD): {denegado}" +
                   paymentDetailsLine +
                   docsPendingLine;
        }

        private static string ConstruirLineaItemsDenegados(ResumenReintegro r, string locale)
        {
            if (r.AwaitResult == null || r.AwaitResult.Count == 0)
                return string.Empty;

            var items = r.AwaitResult
                .Where(x => x != null && (!string.IsNullOrWhiteSpace(x.Concept)) && ((x.DeniedUsd ?? 0m) > 0m || (x.Denied ?? 0m) > 0m))
                .Take(3)
                .Select(x =>
                {
                    var usd = (x.DeniedUsd ?? 0m);
                    if (usd > 0m)
                        return $"{x.Concept} (USD {usd:0.##})";

                    var den = (x.Denied ?? 0m);
                    var cur = string.IsNullOrWhiteSpace(x.Currency) ? string.Empty : $" {x.Currency}";
                    return $"{x.Concept} ({den:0.##}{cur})";
                })
                .ToList();

            if (items.Count == 0)
                return string.Empty;

            var header = locale.StartsWith("en")
                ? "\nDenied items: "
                : locale.StartsWith("pt")
                    ? "\nItens negados: "
                    : "\nÍtems denegados: ";

            return header + string.Join(", ", items);
        }

        private static string? ConstruirDetalleDenegacion(ResumenReintegro r, string locale)
        {
            var hasTotal = r.TotalDeniedUsd.HasValue && r.TotalDeniedUsd.Value > 0m;
            var hasItems = r.AwaitResult != null && r.AwaitResult.Any(x => x != null && ((x.DeniedUsd ?? 0m) > 0m || (x.Denied ?? 0m) > 0m));
            var hasReasons = r.AwaitResult != null && r.AwaitResult.Any(x => x != null && x.DenialReason != null && x.DenialReason.Count > 0);
            if (!hasTotal && !hasItems && !hasReasons)
                return null;

            var titulo = locale.StartsWith("en")
                ? "Denied details"
                : locale.StartsWith("pt")
                    ? "Detalhes da recusa"
                    : "Detalle de denegación";

            var lineas = new List<string> { titulo };

            if (hasTotal)
            {
                var total = r.TotalDeniedUsd!.Value;
                lineas.Add(locale.StartsWith("en") ? $"Denied (USD): {total:0.##}" : locale.StartsWith("pt") ? $"Negado (USD): {total:0.##}" : $"Denegado (USD): {total:0.##}");
            }

            if (hasItems)
            {
                var header = locale.StartsWith("en") ? "Denied items:" : locale.StartsWith("pt") ? "Itens negados:" : "Ítems denegados:";
                lineas.Add(header);

                foreach (var it in r.AwaitResult.Where(x => x != null && (!string.IsNullOrWhiteSpace(x.Concept)) && ((x.DeniedUsd ?? 0m) > 0m || (x.Denied ?? 0m) > 0m)).Take(10))
                {
                    var usd = it.DeniedUsd;
                    if (usd.HasValue && usd.Value > 0m)
                    {
                        lineas.Add($"- {it.Concept}: USD {usd.Value:0.##}");
                        continue;
                    }

                    var den = it.Denied;
                    var cur = string.IsNullOrWhiteSpace(it.Currency) ? string.Empty : $" {it.Currency}";
                    if (den.HasValue && den.Value > 0m)
                        lineas.Add($"- {it.Concept}: {den.Value:0.##}{cur}");
                }
            }

            if (hasReasons)
            {
                var header = locale.StartsWith("en") ? "Denial reasons:" : locale.StartsWith("pt") ? "Motivos da recusa:" : "Motivos de denegación:";
                lineas.Add(header);

                var reasons = r.AwaitResult
                    .Where(x => x != null && x.DenialReason != null && x.DenialReason.Count > 0)
                    .SelectMany(x => x!.DenialReason)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(10);

                foreach (var rr in reasons)
                    lineas.Add($"- {rr}");
            }

            return string.Join("\n", lineas);
        }

        private async Task<string> ConstruirFallbackDeterministicoAsync(string telefono, SesionConversacion sesion, string locale, CancellationToken ct)
        {
            if (sesion.Estado == EstadoConversacion.AwaitingName)
                return ConstruirMensajePedirNombre(locale);

            if (sesion.Estado == EstadoConversacion.MenuPrincipal)
                return ConstruirMenuPrincipal(locale, sesion.Nombre);

            if (sesion.Estado == EstadoConversacion.OtrasConsultasMenu)
                return ConstruirMenuOtrasConsultas(locale, sesion.Nombre);

            if (sesion.Estado == EstadoConversacion.AwaitingIdentifier)
                return ConstruirPedidoIdentificador(locale, sesion.Nombre);

            if (sesion.Estado == EstadoConversacion.SelectingReintegro)
            {
                var lista = await ObtenerListaReintegrosAsync(telefono, ct);
                if (lista.Count == 0)
                    return ConstruirPedidoIdentificador(locale, sesion.Nombre);
                return ConstruirMensajeSeleccionReintegro(lista, locale);
            }

            if (sesion.Estado == EstadoConversacion.ReintegroMenu)
            {
                var reintegroActual = await ObtenerReintegroActualAsync(telefono, ct);
                return reintegroActual == null
                    ? ConstruirPedidoIdentificador(locale, sesion.Nombre)
                    : ConstruirDetalleYMenuReintegro(reintegroActual, locale, sesion.Nombre);
            }

            if (sesion.Estado == EstadoConversacion.ReintegroFinancialMenu)
                return BotMessages.MenuFinanciero(locale);

            if (sesion.Estado == EstadoConversacion.ReintegroPaymentsMenu)
                return BotMessages.MenuPagos(locale);

            if (sesion.Estado == EstadoConversacion.ReintegroPendingDocsSelect)
            {
                var reintegroActual = await ObtenerReintegroActualAsync(telefono, ct);
                return reintegroActual == null
                    ? BotMessages.MensajeSinReintegro(locale)
                    : BotMessages.MenuDocumentosPendientes(reintegroActual, locale);
            }

            if (sesion.Estado == EstadoConversacion.ReintegroPendingDocsAction)
                return BotMessages.MenuAccionDocumento(locale, null);

            if (sesion.Estado == EstadoConversacion.ReintegroPendingDocsAskContinue)
                return BotMessages.PreguntaContinuar(locale);

            if (sesion.Estado == EstadoConversacion.ReintegroExit)
                return "\n" + BotMessages.ConstruirExit(locale);

            if (sesion.CurrentBenefitId != null || sesion.CurrentCaseId != null)
            {
                var reintegroActual = await ObtenerReintegroActualAsync(telefono, ct);
                if (reintegroActual != null)
                {
                    sesion.Estado = EstadoConversacion.ReintegroMenu;
                    return ConstruirDetalleYMenuReintegro(reintegroActual, locale, sesion.Nombre);
                }
            }

            sesion.Estado = EstadoConversacion.MenuPrincipal;
            return ConstruirMenuPrincipal(locale, sesion.Nombre);
        }

        private Task GuardarReintegroActualAsync(string telefono, ResumenReintegro resumen, CancellationToken ct)
            => _sesionMgr.GuardarReintegroActualAsync(telefono, resumen, ct);

        private Task GuardarListaReintegrosAsync(string telefono, List<ResumenReintegro> lista, CancellationToken ct)
            => _sesionMgr.GuardarListaReintegrosAsync(telefono, lista, ct);

        private Task AgregarHistorialSiEsPosibleAsync(string telefono, string textoUsuario, string textoRespuesta, CancellationToken ct)
            => _sesionMgr.AgregarHistorialSiEsPosibleAsync(telefono, textoUsuario, textoRespuesta, ct);

        private async Task RegistrarConversacionSiEsPosibleAsync(
            string? telefono,
            string? conversationId,
            string locale,
            string userChat,
            string assistanceChat,
            DateTime inicio,
            CancellationToken ct)
        {
            try
            {
                var language = TextNormalizer.MapearIdioma(locale);

                var convId = (conversationId ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(convId))
                    convId = DerivarConversationIdDesdeIdentificador(telefono);

                await _logger.RegistrarAsync(
                    userChat: userChat ?? string.Empty,
                    assistanceChat: assistanceChat ?? string.Empty,
                    identifier: telefono ?? string.Empty,
                    conversationId: convId,
                    language: language,
                    startSend: inicio,
                    resultSend: DateTime.UtcNow,
                    ct: ct);
            }
            catch
            {
                // No interrumpir el flujo si el logger falla
            }
        }

        private static string DerivarConversationIdDesdeIdentificador(string? telefono)
        {
            var t = (telefono ?? string.Empty).Trim();
            if (t.StartsWith("samu:", StringComparison.OrdinalIgnoreCase))
            {
                var rest = t.Substring("samu:".Length);
                if (rest.StartsWith("phone:", StringComparison.OrdinalIgnoreCase))
                    return string.Empty;
                if (rest.StartsWith("anon:", StringComparison.OrdinalIgnoreCase))
                    return string.Empty;
                return rest;
            }

            return string.Empty;
        }

        private Task<string?> ObtenerLocaleAsync(string telefono, CancellationToken ct)
            => _sesionMgr.ObtenerLocaleAsync(telefono, ct);

        private Task GuardarLocaleAsync(string telefono, string locale, CancellationToken ct)
            => _sesionMgr.GuardarLocaleAsync(telefono, locale, ct);

        /// <summary>
        /// Safety net: detecta el idioma de la respuesta de IA y la traduce si no coincide
        /// con el idioma del usuario. Evita que el bot responda en un idioma incorrecto.
        /// </summary>
        private async Task<string> AsegurarIdiomaRespuestaAsync(string texto, string localeUsuario, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(texto) || texto.Length < 20)
                return texto;

            var target = LocaleHelper.ResolveBase(localeUsuario);

            try
            {
                var idiomaDetectado = await _trad.DetectarAsync(texto, ct);
                var baseDetectado = LocaleHelper.ResolveBase(idiomaDetectado);

                if (baseDetectado != target)
                {
                    _log.LogInformation(
                        "SafetyNet.IdiomaIncorrecto detectado={Detectado} esperado={Esperado} len={Len}",
                        baseDetectado, target, texto.Length);

                    var traducido = await _trad.TraducirAsync(texto, target, ct);
                    if (!string.IsNullOrWhiteSpace(traducido))
                        return traducido;
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "SafetyNet.Error al verificar idioma de respuesta IA");
            }

            return texto;
        }

        private static bool EsTextoAmbiguoParaDeteccion(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return true;
            var t = texto.Trim();

            // Los textos numéricos (IDs/selecciones) no aportan a la detección y generan falsos positivos
            if (t.All(char.IsDigit))
                return true;

            if (t.Length <= 3 && t.All(c => !char.IsLetter(c)))
                return true;

            return false;
        }

        private static string SanitizarEmailEnRespuesta(string mensaje, string locale)
        {
            if (string.IsNullOrWhiteSpace(mensaje)) return mensaje;

            if (locale.StartsWith("pt"))
            {
                // Para PT el correcto es reembolso@; reemplazar si Foundry puso reintegros@
                mensaje = System.Text.RegularExpressions.Regex.Replace(
                    mensaje,
                    @"reintegros@assistcard\.com",
                    "reembolso@assistcard.com",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }
            else
            {
                // Para ES/EN el correcto es reintegros@; reemplazar si Foundry puso reembolso@
                mensaje = System.Text.RegularExpressions.Regex.Replace(
                    mensaje,
                    @"reembolso@assistcard\.com",
                    "reintegros@assistcard.com",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            return mensaje;
        }


        private async Task IntentarEnviarAsync(string destino, string texto, CancellationToken ct)
        {
            try
            {
                var captura = CapturaSalidaActual.Value;
                if (captura != null)
                {
                    captura.TextoRespuesta = texto;
                    return;
                }

                if (!string.IsNullOrWhiteSpace(_waba.AccessToken) && !string.IsNullOrWhiteSpace(_waba.PhoneNumberId))
                {
                    var sample = string.IsNullOrWhiteSpace(texto) ? "<empty>" : (texto.Length <= 350 ? texto : texto.Substring(0, 350));
                    _log.LogInformation("WA outbound text. to={To} len={Len} textSample={TextSample}", destino, texto?.Length ?? 0, sample);
                    await _wa.EnviarTextoAsync(destino, texto, ct);
                }
                // Si falta config, no enviamos pero no fallamos el webhook
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "IntentarEnviar fallo destino={Destino}", destino);
            }
        }

        private async Task ProcesarMediaAsync(WebhookMessage msg, string from, string destinoRespuesta, string tipo, CancellationToken ct)
        {
            var mediaId = tipo.Equals("image", StringComparison.OrdinalIgnoreCase)
                ? msg.Image?.Id
                : msg.Document?.Id;

            var contentType = tipo.Equals("image", StringComparison.OrdinalIgnoreCase)
                ? msg.Image?.MimeType
                : msg.Document?.MimeType;

            var fileName = msg.Document?.FileName;

            var mediaLocale = await ObtenerLocaleAsync(from, ct) ?? "es";

            if (string.IsNullOrWhiteSpace(mediaId))
            {
                await IntentarEnviarAsync(destinoRespuesta, BotMessages.ErrorLeerArchivo(mediaLocale), ct);
                return;
            }

            UploadContext? uploadCtx = null;
            try
            {
                var key = $"uploadctx:{from}";
                var json = await _cache.ObtenerAsync(key, ct);
                if (!string.IsNullOrWhiteSpace(json))
                    uploadCtx = JsonSerializer.Deserialize<UploadContext>(json);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Cache.ObtenerUploadCtx fallo telefono={Telefono}", from);
            }

            if (uploadCtx == null || !uploadCtx.BenefitRequestId.HasValue || !uploadCtx.DocumentId.HasValue)
            {
                await IntentarEnviarAsync(destinoRespuesta, BotMessages.ArchivoSinContexto(mediaLocale), ct);
                return;
            }

            System.IO.Stream contenido;
            try
            {
                contenido = await _wa.DescargarMediaAsync(mediaId, ct);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "DescargarMedia fallo mediaId={MediaId}", mediaId);
                await IntentarEnviarAsync(destinoRespuesta, BotMessages.ErrorDescargarMedia(mediaLocale), ct);
                return;
            }

            using (contenido)
            {
                var archivos = new[]
                {
                    (uploadCtx.DocumentId.Value,
                     uploadCtx.FileName ?? fileName ?? "documento",
                     uploadCtx.ContentType ?? contentType ?? "application/octet-stream",
                     contenido)
                };

                try
                {
                    await _rein.AgregarDocumentosAsync(uploadCtx.BenefitRequestId.Value, archivos, ct);
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "AgregarDocumentos fallo BenefitRequestId={BrId}", uploadCtx.BenefitRequestId);
                    await IntentarEnviarAsync(destinoRespuesta, BotMessages.ErrorAdjuntarArchivo(mediaLocale), ct);
                    return;
                }
            }

            await IntentarEnviarAsync(destinoRespuesta, BotMessages.DocumentoRecibidoOk(mediaLocale), ct);

            var locale = mediaLocale;

            try
            {
                var sesion = await ObtenerSesionAsync(from, ct) ?? new SesionConversacion();
                if (sesion.Estado == EstadoConversacion.Menu)
                    sesion.Estado = string.IsNullOrWhiteSpace(sesion.Nombre) ? EstadoConversacion.AwaitingName : EstadoConversacion.MenuPrincipal;
                if (sesion.Estado == EstadoConversacion.ShowingReintegro)
                    sesion.Estado = EstadoConversacion.ReintegroMenu;

                sesion.Estado = EstadoConversacion.ReintegroPendingDocsAskContinue;
                await GuardarSesionAsync(from, sesion, ct);

                try
                {
                    await _cache.GuardarAsync($"uploadctx:{from}", string.Empty, TimeSpan.FromSeconds(1), ct);
                }
                catch (Exception exCache)
                {
                    _log.LogDebug(exCache, "Cache.LimpiarUploadCtx fallo telefono={Telefono}", from);
                }

                await IntentarEnviarAsync(destinoRespuesta, BotMessages.PreguntaContinuar(locale), ct);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "ProcesarMedia post-upload fallo telefono={Telefono}", from);
                await IntentarEnviarAsync(destinoRespuesta, BotMessages.PreguntaContinuar(locale), ct);
            }
        }

    }
}
