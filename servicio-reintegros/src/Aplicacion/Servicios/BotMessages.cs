using System;
using System.Collections.Generic;
using System.Linq;
using ServicioReintegros.AssistCard.Aplicacion.Dtos;

namespace ServicioReintegros.AssistCard.Aplicacion.Servicios
{
    /// <summary>
    /// Centraliza TODOS los mensajes del bot en los tres idiomas (es/en/pt).
    /// Elimina los ~120+ ternarios locale.StartsWith dispersos en el orquestador.
    /// </summary>
    public static class BotMessages
    {
        // ──────────────────────────────────────────────
        //  Menú principal
        // ──────────────────────────────────────────────

        public static string MenuPrincipalHeader(string locale, string? nombre)
        {
            var saludo = string.IsNullOrWhiteSpace(nombre)
                ? LocaleHelper.Loc(locale, "¡Hola!", "Hello!", "Olá!")
                : LocaleHelper.Loc(locale, $"¡Hola {nombre}!", $"Hello {nombre}!", $"Olá {nombre}!");

            var intro = LocaleHelper.Loc(locale,
                "Soy el asistente virtual de AssistCard para consultas de reintegros. ¿En qué puedo ayudarte?",
                "I'm AssistCard's virtual assistant for reimbursement inquiries. How can I help you?",
                "Sou o assistente virtual da AssistCard para consultas de reembolsos. Como posso ajudá-lo?");

            return saludo + " " + intro + "\n" + FormatearOpcionesNumeradas(OpcionesMenuPrincipal(locale));
        }

        public static List<string> OpcionesMenuPrincipal(string locale)
            => new()
            {
                LocaleHelper.Loc(locale,
                    "Consultar reintegro",
                    "Check reimbursement",
                    "Consultar reembolso"),
                LocaleHelper.Loc(locale,
                    "Otras consultas",
                    "Other inquiries",
                    "Outras consultas")
            };

        // ──────────────────────────────────────────────
        //  Otras consultas
        // ──────────────────────────────────────────────

        public static string MenuOtrasConsultasHeader(string locale, string? nombre)
        {
            var intro = LocaleHelper.Loc(locale,
                "¿Sobre qué tema necesitás ayuda?",
                "What topic do you need help with?",
                "Sobre qual assunto você precisa de ajuda?");
            return intro + "\n" + FormatearOpcionesNumeradas(OpcionesOtrasConsultas(locale));
        }

        public static List<string> OpcionesOtrasConsultas(string locale)
            => new()
            {
                LocaleHelper.Loc(locale,
                    "Requisitos y pasos para iniciar un reintegro",
                    "Requirements and steps to start a reimbursement",
                    "Requisitos e passos para iniciar um reembolso"),
                LocaleHelper.Loc(locale,
                    "Problemas con la App My Assist Card",
                    "Issues with My Assist Card App",
                    "Problemas com o App My Assist Card"),
                LocaleHelper.Loc(locale,
                    "Detallar un problema",
                    "Describe an issue",
                    "Descrever um problema"),
                OpcionVolverMenuAnterior(locale)
            };

        // ──────────────────────────────────────────────
        //  Pedir nombre
        // ──────────────────────────────────────────────

        public static string PedirNombre(string locale)
            => LocaleHelper.Loc(locale,
                "¡Hola! Soy el asistente de reintegros de AssistCard. Para comenzar, ¿podrías indicarme tu nombre?",
                "Hello! I'm AssistCard's reimbursement assistant. To get started, could you tell me your name?",
                "Olá! Sou o assistente de reembolsos da AssistCard. Para começar, poderia me dizer seu nome?");

        public static string PedirNombreTrasConsulta(string locale)
            => LocaleHelper.Loc(locale,
                "Antes de continuar, necesito tu nombre. ¿Podrías indicármelo?",
                "Before we continue, I need your name. Could you please tell me?",
                "Antes de continuar, preciso do seu nome. Poderia me informar?");

        // ──────────────────────────────────────────────
        //  Pedir identificador
        // ──────────────────────────────────────────────

        public static string PedidoIdentificador(string locale, string? nombre)
        {
            var saludo = string.IsNullOrWhiteSpace(nombre) ? "" : $"{nombre}, ";
            return LocaleHelper.Loc(locale,
                $"{saludo}por favor enviame tu número de Beneficio (Benefit ID), Case ID, email o voucher para buscar tu reintegro.",
                $"{saludo}please send me your Benefit ID, Case ID, email or voucher to look up your reimbursement.",
                $"{saludo}por favor envie seu número de Benefício (Benefit ID), Case ID, email ou voucher para buscar seu reembolso.");
        }

        // ──────────────────────────────────────────────
        //  Opciones individuales reutilizables
        // ──────────────────────────────────────────────

        public static string OpcionVolverMenuAnterior(string locale)
            => LocaleHelper.Loc(locale,
                "Volver al menu anterior",
                "Back to previous menu",
                "Voltar ao menu anterior");

        public static string OpcionCerrarChat(string locale)
            => LocaleHelper.Loc(locale,
                "Cerrar chat",
                "Close chat",
                "Encerrar chat");

        public static string OpcionDetalleEstado(string locale)
            => LocaleHelper.Loc(locale,
                "Consultar detalle de estado",
                "Status details",
                "Detalhes do status");

        public static string OpcionDetalleFinanciero(string locale)
            => LocaleHelper.Loc(locale,
                "Consultar detalle financiero",
                "Financial details",
                "Detalhes financeiros");

        public static string OpcionDetallePagos(string locale)
            => LocaleHelper.Loc(locale,
                "Consultar detalle pagos",
                "Payment details",
                "Detalhes de pagamento");

        public static string OpcionProximosPasos(string locale)
            => LocaleHelper.Loc(locale,
                "Consultar detalle proximos pasos",
                "Next steps",
                "Próximos passos");

        public static string OpcionDocumentacionPendiente(string locale)
            => LocaleHelper.Loc(locale,
                "Consultar documentacion pendiente",
                "Pending documents",
                "Documentos pendentes");

        public static string OpcionDetallarProblemaReintegro(string locale)
            => LocaleHelper.Loc(locale,
                "Detallar problema con mi reintegro",
                "Describe an issue with my reimbursement",
                "Descrever um problema com meu reembolso");

        public static string OpcionDetalleAprobacion(string locale)
            => LocaleHelper.Loc(locale,
                "Detalle de aprobacion",
                "Approval details",
                "Detalhe de aprovação");

        public static string OpcionMotivoDenegacion(string locale)
            => LocaleHelper.Loc(locale,
                "Motivo de denegacion",
                "Denial reason",
                "Motivo da recusa");

        public static string OpcionTipoCambio(string locale)
            => LocaleHelper.Loc(locale,
                "Tipo de cambio",
                "Exchange rate",
                "Taxa de câmbio");

        public static string OpcionProblemasPago(string locale)
            => LocaleHelper.Loc(locale,
                "Problemas con el pago",
                "Payment issues",
                "Problemas com o pagamento");

        public static string OpcionVerDetallePago(string locale)
            => LocaleHelper.Loc(locale,
                "Ver detalle de mi pago",
                "View payment details",
                "Ver detalhes do pagamento");

        // ──────────────────────────────────────────────
        //  Opciones agrupadas Exit
        // ──────────────────────────────────────────────

        public static List<string> OpcionesExit(string locale)
            => new() { OpcionVolverMenuAnterior(locale), OpcionCerrarChat(locale) };

        // ──────────────────────────────────────────────
        //  Exit / Cierre
        // ──────────────────────────────────────────────

        public static string ConstruirExit(string locale)
            => FormatearOpcionesNumeradas(OpcionesExit(locale));

        public static string ConversacionFinalizada(string locale)
            => LocaleHelper.Loc(locale,
                "La conversación ha finalizado.",
                "The conversation has ended.",
                "A conversa foi encerrada.");

        public static string ConversacionFinalizadaConMenu(string locale)
            => LocaleHelper.Loc(locale,
                "La conversación ha finalizado. Escribí 'menú' para empezar de nuevo.",
                "The conversation has ended. Write 'menu' to start again.",
                "A conversa foi encerrada. Escreva 'menu' para começar de novo.");

        // ──────────────────────────────────────────────
        //  Mensajes de estado del servicio
        // ──────────────────────────────────────────────

        public static string ServicioNoDisponible(string locale)
            => LocaleHelper.Loc(locale,
                "Servicio temporalmente no disponible. Intentá más tarde.",
                "Service temporarily unavailable. Please try again later.",
                "Serviço temporariamente indisponível. Tente novamente mais tarde.");

        public static string SinReintegro(string locale)
            => LocaleHelper.Loc(locale,
                "Todavía no tengo un reintegro seleccionado. Enviame tu Nro Beneficio, Case ID, email o voucher.",
                "I don't have a reimbursement selected yet. Please send your Benefit ID, Case ID, email or voucher.",
                "Ainda não tenho um reembolso selecionado. Envie seu número de Benefício, Case ID, email ou voucher.");

        public static string NoEncontreReintegro(string locale)
            => LocaleHelper.Loc(locale,
                "No encontré un reintegro con esos datos. Probá de nuevo con Nro Beneficio, Case ID, email o voucher.",
                "I couldn't find a reimbursement with those details. Please try again with your Benefit ID, Case ID, email or voucher.",
                "Não encontrei um reembolso com esses dados. Tente novamente com seu número de Benefício, Case ID, email ou voucher.");

        public static string NoEncontreRegistroValido(string locale)
            => LocaleHelper.Loc(locale,
                "No encontré un registro válido con esos datos. Intentá de nuevo.",
                "I couldn't find a valid reimbursement record with those details. Please try again.",
                "Não encontrei um registro válido com esses dados. Tente novamente.");

        public static string ErrorConsultarReintegro(string locale)
            => LocaleHelper.Loc(locale,
                "No pude consultar tu reintegro en este momento. Intentá más tarde.",
                "I couldn't retrieve your reimbursement information right now. Please try again later.",
                "Não consegui consultar seu reembolso agora. Tente novamente mais tarde.");

        public static string RespuestaIaVacia(string locale)
            => LocaleHelper.Loc(locale,
                "No pude generar una respuesta en este momento. Intentá más tarde.",
                "I could not generate an answer at this moment. Please try again later.",
                "Não consegui gerar uma resposta no momento. Tente novamente mais tarde.");

        // ──────────────────────────────────────────────
        //  Menú reintegro
        // ──────────────────────────────────────────────

        public static List<string> ConstruirOpcionesReintegroMenu(ResumenReintegro r, string locale)
        {
            var opciones = new List<string> { OpcionDetalleEstado(locale) };
            if (!EstaEnRevision(r))
                opciones.Add(OpcionDetalleFinanciero(locale));
            if (r.PaymentDetails != null && r.PaymentDetails.Any(p => p != null && !string.IsNullOrWhiteSpace(p.Detalle)))
                opciones.Add(OpcionDetallePagos(locale));
            opciones.Add(OpcionProximosPasos(locale));
            if (r.PendingDocuments != null && r.PendingDocuments.Count > 0)
                opciones.Add(OpcionDocumentacionPendiente(locale));
            opciones.Add(OpcionDetallarProblemaReintegro(locale));
            opciones.Add(OpcionVolverMenuAnterior(locale));
            return opciones;
        }

        public static string ConstruirDetalleYMenuReintegro(ResumenReintegro r, string locale, string? nombre)
            => FormatearResumenReintegro(r, locale) + "\n\n" + FormatearOpcionesNumeradas(ConstruirOpcionesReintegroMenu(r, locale));

        // ──────────────────────────────────────────────
        //  Menú financiero
        // ──────────────────────────────────────────────

        public static List<string> OpcionesFinanciero(string locale)
            => new() { OpcionDetalleAprobacion(locale), OpcionMotivoDenegacion(locale), OpcionVolverMenuAnterior(locale) };

        public static string MenuFinancieroHeader(string locale)
            => LocaleHelper.Loc(locale,
                "Para consultar sobre el detalle financiero seleccione la opcion que mejor se ajuste a su requerimiento",
                "To check the financial details, select the option that best matches your request",
                "Para consultar os detalhes financeiros, selecione a opção que melhor corresponda à sua solicitação")
            + "\n" + FormatearOpcionesNumeradas(OpcionesFinanciero(locale));

        // ──────────────────────────────────────────────
        //  Menú pagos
        // ──────────────────────────────────────────────

        public static List<string> OpcionesPagos(string locale)
            => new() { OpcionTipoCambio(locale), OpcionProblemasPago(locale), OpcionVerDetallePago(locale), OpcionVolverMenuAnterior(locale) };

        public static string MenuPagosHeader(string locale)
            => LocaleHelper.Loc(locale,
                "Que desea consultar sobre su pago?",
                "What would you like to check about your payment?",
                "O que você deseja consultar sobre seu pagamento?")
            + "\n" + FormatearOpcionesNumeradas(OpcionesPagos(locale));

        // ──────────────────────────────────────────────
        //  Documentos pendientes
        // ──────────────────────────────────────────────

        public static string MenuDocumentosPendientesHeader(ResumenReintegro r, string locale)
        {
            if (r.PendingDocuments == null || r.PendingDocuments.Count == 0)
                return LocaleHelper.Loc(locale,
                    "Tu reintegro NO tiene documentos pendientes",
                    "Your reimbursement has NO pending documents",
                    "Seu reembolso NÃO tem documentos pendentes")
                + "\n" + FormatearOpcionesNumeradas(OpcionesExit(locale));

            var opciones = ConstruirOpcionesDocumentosPendientes(r, locale);
            var count = r.PendingDocuments.Count;
            var header = LocaleHelper.Loc(locale,
                $"Tu reintegro tiene {count} documentos pendientes, selecciona cual deseas cargar:",
                $"Your reimbursement has {count} pending documents, select which one you want to upload:",
                $"Seu reembolso tem {count} documentos pendentes. Selecione qual você deseja enviar:");
            return header + "\n" + FormatearOpcionesNumeradas(opciones);
        }

        public static List<string> ConstruirOpcionesDocumentosPendientes(ResumenReintegro r, string locale)
        {
            var opciones = new List<string>();
            if (r.PendingDocuments != null)
            {
                foreach (var d in r.PendingDocuments)
                {
                    if (d == null) continue;
                    opciones.Add(FormatearDocOpcion(d, locale));
                }
            }
            opciones.Add(OpcionVolverMenuAnterior(locale));
            return opciones;
        }

        public static string FormatearDocOpcion(DocumentoPendiente d, string locale)
            => LocaleHelper.Loc(locale,
                $"ID: {d.DocumentId?.ToString() ?? "-"} | Tipo: {d.DocumentType ?? "-"} | Descripción: {d.Description ?? "-"}",
                $"ID: {d.DocumentId?.ToString() ?? "-"} | Type: {d.DocumentType ?? "-"} | Description: {d.Description ?? "-"}",
                $"ID: {d.DocumentId?.ToString() ?? "-"} | Tipo: {d.DocumentType ?? "-"} | Descrição: {d.Description ?? "-"}");

        // ──────────────────────────────────────────────
        //  Acción documento
        // ──────────────────────────────────────────────

        public static List<string> OpcionesAccionDoc(string locale)
            => new()
            {
                LocaleHelper.Loc(locale,
                    "Cargar Documentos Pendientes",
                    "Upload pending documents",
                    "Enviar documentos pendentes"),
                OpcionVolverMenuAnterior(locale),
                OpcionCerrarChat(locale)
            };

        public static string MenuAccionDocumentoHeader(string locale, string? doc)
        {
            var header = string.IsNullOrWhiteSpace(doc)
                ? LocaleHelper.Loc(locale, "Documento seleccionado", "Selected document", "Documento selecionado")
                : doc;
            return header + "\n" + FormatearOpcionesNumeradas(OpcionesAccionDoc(locale));
        }

        // ──────────────────────────────────────────────
        //  SI/NO y continuar
        // ──────────────────────────────────────────────

        public static List<string> OpcionesSiNo(string locale)
            => LocaleHelper.Loc(locale, "es", "en", "pt") switch
            {
                "en" => new List<string> { "YES", "NO" },
                "pt" => new List<string> { "SIM", "NÃO" },
                _ => new List<string> { "SI", "NO" }
            };

        public static string PreguntaContinuar(string locale)
            => LocaleHelper.Loc(locale,
                "¡Documento recibido! Desea continuar?",
                "Document received! Do you want to continue?",
                "Documento recebido! Deseja continuar?")
            + "\n" + FormatearOpcionesNumeradas(OpcionesSiNo(locale));

        // ──────────────────────────────────────────────
        //  Upload / Media
        // ──────────────────────────────────────────────

        public static string PedirUploadDocumento(string locale)
            => LocaleHelper.Loc(locale,
                "Por favor enviá la foto/documento del documento pendiente seleccionado.",
                "Please upload the image/document for the selected pending document.",
                "Por favor envie a imagem/documento do documento pendente selecionado.");

        public static string ErrorLeerArchivo(string locale)
            => LocaleHelper.Loc(locale,
                "No pude leer el archivo que enviaste. Por favor, intentá nuevamente.",
                "I couldn't read the file you sent. Please try again.",
                "Não consegui ler o arquivo que você enviou. Por favor, tente novamente.");

        public static string ArchivoSinContexto(string locale)
            => LocaleHelper.Loc(locale,
                "Recibí tu archivo, pero todavía no tengo asociado a qué reintegro corresponde. Por favor indicame el número de reintegro o volvé a iniciar el flujo de carga de documentos.",
                "I received your file, but I don't have it associated with a reimbursement yet. Please provide the reimbursement number or restart the document upload flow.",
                "Recebi seu arquivo, mas ainda não tenho associado a qual reembolso corresponde. Por favor, informe o número do reembolso ou reinicie o fluxo de envio de documentos.");

        public static string ErrorDescargarMedia(string locale)
            => LocaleHelper.Loc(locale,
                "Hubo un problema al descargar tu archivo. Intentá nuevamente más tarde.",
                "There was a problem downloading your file. Please try again later.",
                "Houve um problema ao baixar seu arquivo. Tente novamente mais tarde.");

        public static string ErrorAdjuntarArchivo(string locale)
            => LocaleHelper.Loc(locale,
                "No pude adjuntar el archivo a tu reintegro. Intentá nuevamente más tarde.",
                "I couldn't attach the file to your reimbursement. Please try again later.",
                "Não consegui anexar o arquivo ao seu reembolso. Tente novamente mais tarde.");

        public static string DocumentoRecibidoOk(string locale)
            => LocaleHelper.Loc(locale,
                "Tu documento fue recibido correctamente y se está procesando en tu reintegro.",
                "Your document was received and is being processed for your reimbursement.",
                "Seu documento foi recebido corretamente e está sendo processado em seu reembolso.");

        // ──────────────────────────────────────────────
        //  Problema con reintegro
        // ──────────────────────────────────────────────

        public static string PedirDetalleProblemaReintegro(string locale)
            => LocaleHelper.Loc(locale,
                "Por favor ingresá el detalle del problema con tu reintegro:",
                "Please describe the issue with your reimbursement.",
                "Por favor descreva o problema com seu reembolso.");

        public static string PedirDetalleProblemaGeneral(string locale)
            => LocaleHelper.Loc(locale,
                "Por favor ingresá el detalle de tu problema:",
                "Please describe your issue.",
                "Por favor descreva seu problema.");

        // ──────────────────────────────────────────────
        //  Estado y pagos (respuestas rápidas)
        // ──────────────────────────────────────────────

        public static string RespuestaEstado(ResumenReintegro r, string locale)
        {
            var estado = string.IsNullOrWhiteSpace(r.Status) ? "-" : r.Status;
            return LocaleHelper.Loc(locale, $"Estado actual: {estado}", $"Current status: {estado}", $"Status atual: {estado}");
        }

        public static string RespuestaPagos(ResumenReintegro r, string locale)
        {
            if (r.PaymentDetails == null || r.PaymentDetails.Count == 0)
                return LocaleHelper.Loc(locale,
                    "Todavía no hay pagos registrados.",
                    "No payments are registered yet.",
                    "Ainda não há pagamentos registrados.");

            var titulo = LocaleHelper.Loc(locale, "Pagos:", "Payments:", "Pagamentos:");
            var lineas = new List<string> { titulo };
            foreach (var p in r.PaymentDetails.Take(5))
            {
                var fecha = p.FechaPago.HasValue ? p.FechaPago.Value.ToString("dd/MM/yyyy") : "-";
                var metodo = p.Metodo ?? "-";
                var est = p.Estado ?? "-";
                var monto = p.MontoUsd.HasValue ? p.MontoUsd.Value.ToString("0.##") : "-";
                lineas.Add($"- {fecha} | {metodo} | {est} | USD {monto}");
            }
            if (r.PaymentDetails.Count > 5)
                lineas.Add(LocaleHelper.Loc(locale, "(mostrando últimos 5)", "(showing last 5)", "(mostrando últimos 5)"));
            return string.Join("\n", lineas);
        }

        // ──────────────────────────────────────────────
        //  Resumen de reintegro (LOCALIZADO)
        // ──────────────────────────────────────────────

        public static string FormatearResumenReintegro(ResumenReintegro r, string locale)
        {
            var fecha = r.CreateDate.HasValue ? r.CreateDate.Value.ToString("dd/MM/yyyy") : "-";
            var solicitado = r.TotalRequestedUsd.HasValue ? r.TotalRequestedUsd.Value.ToString("0.##") : "-";
            var aprobado = r.TotalApprovedUsd.HasValue ? r.TotalApprovedUsd.Value.ToString("0.##") : "-";
            var denegado = r.TotalDeniedUsd.HasValue ? r.TotalDeniedUsd.Value.ToString("0.##") : "-";

            var lineas = new List<string>
            {
                LocaleHelper.Loc(locale, "Detalle de reintegro:", "Reimbursement details:", "Detalhes do reembolso:"),
                $"{LocaleHelper.Loc(locale, "Beneficio", "Benefit", "Benefício")}: {r.BenefitId ?? "-"}",
                $"{LocaleHelper.Loc(locale, "Caso", "Case", "Caso")}: {r.CaseId ?? "-"}",
                $"{LocaleHelper.Loc(locale, "Fecha", "Date", "Data")}: {fecha}",
                $"{LocaleHelper.Loc(locale, "Tipo", "Type", "Tipo")}: {r.BenefitType ?? "-"}",
                $"{LocaleHelper.Loc(locale, "Estado", "Status", "Status")}: {r.Status ?? "-"}",
                $"{LocaleHelper.Loc(locale, "Solicitado (USD)", "Requested (USD)", "Solicitado (USD)")}: {solicitado}",
                $"{LocaleHelper.Loc(locale, "Aprobado (USD)", "Approved (USD)", "Aprovado (USD)")}: {aprobado}",
                $"{LocaleHelper.Loc(locale, "Denegado (USD)", "Denied (USD)", "Negado (USD)")}: {denegado}"
            };

            if (r.PaymentDetails != null && r.PaymentDetails.Count > 0)
                lineas.Add($"{LocaleHelper.Loc(locale, "Pagos registrados", "Registered payments", "Pagamentos registrados")}: {r.PaymentDetails.Count}");

            return string.Join("\n", lineas);
        }

        // ──────────────────────────────────────────────
        //  Detalle denegación (LOCALIZADO)
        // ──────────────────────────────────────────────

        public static string? DetalleDenegacion(ResumenReintegro r, string locale)
        {
            var hasTotal = r.TotalDeniedUsd.HasValue && r.TotalDeniedUsd.Value > 0m;
            var hasItems = r.AwaitResult != null && r.AwaitResult.Any(x => x != null && ((x.DeniedUsd ?? 0m) > 0m || (x.Denied ?? 0m) > 0m));
            var hasReasons = r.AwaitResult != null && r.AwaitResult.Any(x => x != null && x.DenialReason != null && x.DenialReason.Count > 0);
            if (!hasTotal && !hasItems && !hasReasons)
                return null;

            var titulo = LocaleHelper.Loc(locale, "Detalle de denegación", "Denied details", "Detalhes da recusa");
            var lineas = new List<string> { titulo };

            if (hasTotal)
            {
                var total = r.TotalDeniedUsd!.Value;
                lineas.Add(LocaleHelper.Loc(locale, $"Denegado (USD): {total:0.##}", $"Denied (USD): {total:0.##}", $"Negado (USD): {total:0.##}"));
            }

            if (hasItems)
            {
                lineas.Add(LocaleHelper.Loc(locale, "Ítems denegados:", "Denied items:", "Itens negados:"));
                foreach (var it in r.AwaitResult.Where(x => x != null && !string.IsNullOrWhiteSpace(x.Concept) && ((x.DeniedUsd ?? 0m) > 0m || (x.Denied ?? 0m) > 0m)).Take(10))
                {
                    var usd = it.DeniedUsd;
                    if (usd.HasValue && usd.Value > 0m) { lineas.Add($"- {it.Concept}: USD {usd.Value:0.##}"); continue; }
                    var den = it.Denied;
                    var cur = string.IsNullOrWhiteSpace(it.Currency) ? string.Empty : $" {it.Currency}";
                    if (den.HasValue && den.Value > 0m) lineas.Add($"- {it.Concept}: {den.Value:0.##}{cur}");
                }
            }

            if (hasReasons)
            {
                lineas.Add(LocaleHelper.Loc(locale, "Motivos de denegación:", "Denial reasons:", "Motivos da recusa:"));
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

        // ──────────────────────────────────────────────
        //  Selección de reintegro (múltiples resultados)
        // ──────────────────────────────────────────────

        public static string MensajeSeleccionReintegro(List<ResumenReintegro> lista, string locale)
        {
            var header = LocaleHelper.Loc(locale,
                $"Encontré {lista.Count} reintegros. Seleccioná cuál querés consultar:",
                $"I found {lista.Count} reimbursements. Select which one you'd like to check:",
                $"Encontrei {lista.Count} reembolsos. Selecione qual você deseja consultar:");
            return header + "\n" + FormatearOpcionesNumeradas(ConstruirOpcionesSeleccionReintegro(lista, locale));
        }

        public static List<string> ConstruirOpcionesSeleccionReintegro(List<ResumenReintegro> lista, string locale)
        {
            var opciones = new List<string>();
            foreach (var r in lista)
            {
                var fecha = r.CreateDate.HasValue ? r.CreateDate.Value.ToString("dd/MM/yyyy") : "-";
                var bid = r.BenefitId ?? "-";
                var st = r.Status ?? "-";
                opciones.Add($"Benefit {bid} | {st} | {fecha}");
            }
            opciones.Add(OpcionVolverMenuAnterior(locale));
            return opciones;
        }

        // ──────────────────────────────────────────────
        //  Aprobación no disponible por revisión
        // ──────────────────────────────────────────────

        public static string AprobacionNoDisponiblePorRevision(string locale)
            => LocaleHelper.Loc(locale,
                "El reintegro se encuentra en revisión, por lo que el detalle de aprobación aún no está disponible. Una vez que sea evaluado, podrás consultar esta información.",
                "The reimbursement is under review, so approval details are not yet available. Once it's evaluated, you can check this information.",
                "O reembolso está em revisão, então o detalhe de aprovação ainda não está disponível. Após a avaliação, você poderá consultar essa informação.");

        // ──────────────────────────────────────────────
        //  Construir mensaje WhatsApp desde AgentResponse
        // ──────────────────────────────────────────────

        public static string ConstruirMensajeWhatsApp(AgentResponse respuesta, string locale)
        {
            var texto = respuesta.Message;
            if (string.IsNullOrWhiteSpace(texto))
                texto = RespuestaIaVacia(locale);
            if (respuesta.Options.Count > 0)
            {
                var opciones = respuesta.Options.Select((opt, idx) => $"{idx + 1}) {opt}").ToArray();
                texto += "\n\n" + string.Join("\n", opciones);
            }
            return texto;
        }

        // ──────────────────────────────────────────────
        //  Fallbacks hardcodeados (por intent)
        // ──────────────────────────────────────────────

        public static string FallbackDemora(string locale, bool dentroPrimeros20, int? dias, string email)
        {
            if (dentroPrimeros20 && dias.HasValue)
                return LocaleHelper.Loc(locale,
                    $"Tu reintegro fue cargado hace {dias} días. El plazo de evaluación suele ser de aproximadamente 30 días corridos desde la carga. Si necesitás más información, escribinos a {email}.",
                    $"Your reimbursement was submitted {dias} days ago. The evaluation process usually takes around 30 calendar days from submission. If you need more information, please contact us at {email}.",
                    $"Seu reembolso foi solicitado há {dias} dias. O prazo de avaliação costuma ser de aproximadamente 30 dias corridos desde a solicitação. Se precisar de mais informações, entre em contato pelo email {email}.");
            return LocaleHelper.Loc(locale,
                $"La evaluación de tu reintegro puede demorar aproximadamente 30 días corridos. Si necesitás más información o asistencia, escribinos a {email}.",
                $"Your reimbursement evaluation can take around 30 calendar days. If you need more information or assistance, please contact us at {email}.",
                $"A avaliação do seu reembolso pode levar aproximadamente 30 dias corridos. Se precisar de mais informações ou assistência, entre em contato pelo email {email}.");
        }

        public static string FallbackProblemasApp(string locale, string email)
            => LocaleHelper.Loc(locale,
                $"Para problemas con la app MyAC, te recomendamos: 1) Intentá nuevamente. 2) Verificá que la app esté actualizada a su última versión; si no, desinstalá y volvé a descargarla. Si el problema persiste, enviá a {email}: tus datos de contacto, nº de documento/caso, datos bancarios, una captura del error y los datos de tu equipo (marca, modelo, sistema operativo, versión y correo con el que te registraste en la app).",
                $"For issues with the MyAC app, we recommend: 1) Try again. 2) Check that the app is updated to its latest version; if not, uninstall and reinstall it. If the problem persists, please send the following documentation to {email}: your contact details, ID/case number, bank details, a screenshot of the error, and your device details (brand, model, OS version, and the email you used to register in the app).",
                $"Para problemas com o app MyAC, recomendamos: 1) Tente novamente. 2) Verifique que o app está atualizado; caso contrário, desinstale e reinstale. Se o problema persistir, envie para {email}: seus dados de contato, nº de documento/caso, dados bancários, uma captura do erro e os dados do dispositivo (marca, modelo, SO, versão e email usado no cadastro do app).");

        public static string FallbackRequisitosReintegro(string locale, string email)
            => LocaleHelper.Loc(locale,
                $"Para iniciar un reintegro desde la app My Assist Card: 1) Ingresá a la app, 2) Buscá el servicio con tu número de Assist Card / documento, 3) Seleccioná 'Pedir/Solicitar un reintegro', 4) Elegí un integrante o incorporá uno nuevo, 5) Seleccioná la asistencia, 6) Elegí el tipo de reintegro y continuá con la carga de la documentación requerida. Si necesitás más información, escribinos a {email}.",
                $"To start a reimbursement request, please follow these steps in the My Assist Card app: 1) Sign in, 2) Search the service using your Assist Card / document number, 3) Select 'Request reimbursement', 4) Choose a member or add a new one, 5) Select the assistance, 6) Select the reimbursement type and continue with the required documents. If you need more information, email {email}.",
                $"Para iniciar um reembolso no app My Assist Card: 1) Acesse o app, 2) Busque o serviço com seu número de Assist Card / documento, 3) Selecione 'Solicitar reembolso', 4) Escolha um integrante ou adicione um novo, 5) Selecione a assistência, 6) Selecione o tipo de reembolso e continue com os documentos exigidos. Se precisar de mais informações, envie um email para {email}.");

        public static string FallbackPlazoPago(string locale)
            => LocaleHelper.Loc(locale,
                "Le enviamos un saludo desde el Departamento de Atención al Cliente. El monto autorizado se encuentra en proceso de programación de pago y usted podrá verlo reflejado en su cuenta bancaria en un máximo de 7 días hábiles, en la moneda local con la cual se adquirió su producto. El pago será realizado utilizando el cambio oficial tipo vendedor del día anterior al pago.",
                "We send you greetings from the Customer Care Department. The approved amount is being scheduled for payment and you should see it reflected in your bank account within a maximum of 7 business days, in the local currency in which your product was purchased. The payment will be made using the official exchange rate from the day before the payment.",
                "Enviamos uma saudação do Departamento de Atendimento ao Cliente. O valor aprovado está em processo de programação de pagamento e você poderá vê-lo refletido em sua conta bancária em um prazo máximo de 7 dias úteis, na moeda local com a qual o seu produto foi adquirido. O pagamento será realizado utilizando a taxa de câmbio oficial do dia anterior ao pagamento.");

        public static string FallbackMonedaPago(string locale)
            => LocaleHelper.Loc(locale,
                "Según la Cláusula 4.4.2.7 REEMBOLSOS, ASSIST CARD reintegrará al Titular la suma que corresponda en moneda local. En aquellos casos en los que proceda un reembolso y se hayan realizado los pagos en cualquier otra moneda, el pago será realizado utilizando el cambio oficial tipo vendedor del día anterior al pago. NO se te abonará en la moneda del gasto (USD), sino en tu moneda local.",
                "According to Clause 4.4.2.7 REIMBURSEMENTS, ASSIST CARD will reimburse the corresponding amount in local currency. In cases where payments were made in another currency, the payment will be made using the official exchange rate from the day before the payment. You will NOT be paid in the currency of the expense (USD), but in your local currency.",
                "De acordo com a Cláusula 4.4.2.7 REEMBOLSOS, a ASSIST CARD reembolsará o valor correspondente em moeda local. Nos casos em que os pagamentos foram feitos em outra moeda, o pagamento será feito usando a taxa de câmbio oficial do dia anterior ao pagamento. Você NÃO será pago na moeda da despesa (USD), mas em sua moeda local.");

        public static string FallbackTipoCambio(string locale)
            => LocaleHelper.Loc(locale,
                "Le enviamos un saludo desde el Departamento de Atención al Cliente. Su solicitud fue aprobada y el pago se realizará en su MONEDA LOCAL (no en la moneda del gasto original) utilizando el CAMBIO OFICIAL TIPO VENDEDOR del día anterior al pago.",
                "Greetings from the Customer Care Department. Your request was approved and payment will be made in your LOCAL CURRENCY (not in the original expense currency) using the OFFICIAL EXCHANGE RATE from the day before the payment is processed.",
                "Saudações do Departamento de Atendimento ao Cliente. Sua solicitação foi aprovada e o pagamento será feito em sua MOEDA LOCAL (não na moeda da despesa original) usando a TAXA DE CÂMBIO OFICIAL do dia anterior ao processamento do pagamento.");

        public static string FallbackProblemasAppDesdeReintegro(string locale, string email)
            => LocaleHelper.Loc(locale,
                $"Si la App MyAC no funciona, te recomendamos seguir los siguientes pasos:\n\n1) Volver a intentar\n2) Verificar que tu App esté actualizada a su última versión, o reinstalarla si es necesario\n3) Si tu sistema operativo no descarga las actualizaciones de forma automática, desinstalar y volver a descargar la App\n\nSi el problema persiste, enviá la documentación requerida (datos de contacto, documento de identidad, información bancaria, captura del error, datos del equipo) a {email} y nos pondremos en contacto a la brevedad.",
                $"If the MyAC App is not working, we recommend the following steps:\n\n1) Try again\n2) Check if your App is updated to the latest version, or reinstall it if necessary\n3) If your operating system does not download updates automatically, uninstall and reinstall the App\n\nIf the issue persists, please send the required documentation (contact details, ID, bank information, error screenshot, device details) to {email} and we will assist you as soon as possible.",
                $"Se o App MyAC não está funcionando, recomendamos os seguintes passos:\n\n1) Tente novamente\n2) Verifique se seu App está atualizado para a versão mais recente, ou reinstale-o se necessário\n3) Se o sistema operacional não baixa atualizações automaticamente, desinstale e reinstale o App\n\nSe o problema persistir, envie a documentação necessária (dados de contato, documento de identidade, informações bancárias, captura do erro, dados do equipamento) para {email} e entraremos em contato o mais breve possível.");

        // ──────────────────────────────────────────────
        //  Aliases para StateHandlers
        // ──────────────────────────────────────────────

        public static string MenuFinanciero(string locale) => MenuFinancieroHeader(locale);
        public static string MenuPagos(string locale) => MenuPagosHeader(locale);
        public static string MenuDocumentosPendientes(ResumenReintegro r, string locale) => MenuDocumentosPendientesHeader(r, locale);
        public static string MenuAccionDocumento(string locale, string? doc) => MenuAccionDocumentoHeader(locale, doc);
        public static string MensajeSinReintegro(string locale) => SinReintegro(locale);
        public static string FallbackPlazosPago(string locale) => FallbackPlazoPago(locale);

        // ──────────────────────────────────────────────
        //  Helpers
        // ──────────────────────────────────────────────

        public static string FormatearOpcionesNumeradas(List<string> opciones)
        {
            if (opciones == null || opciones.Count == 0) return string.Empty;
            return string.Join("\n", opciones.Select((o, i) => $"{i + 1}) {o}"));
        }

        private static bool EstaEnRevision(ResumenReintegro r)
        {
            if (r == null) return false;
            var status = (r.StatusOriginal ?? r.Status ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(status)) return false;
            return status.Contains("under review") || status.Contains("received")
                || status.Contains("en revision") || status.Contains("en revisao")
                || status.Contains("recebido");
        }
    }
}
