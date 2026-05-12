using System;
using System.Threading;
using System.Threading.Tasks;
using ServicioReintegros.AssistCard.Aplicacion.Dtos;
using ServicioReintegros.AssistCard.Aplicacion.Puertos;
using ServicioReintegros.AssistCard.Dominio.Entidades;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace ServicioReintegros.AssistCard.Aplicacion.Servicios.StateHandlers
{
    /// <summary>
    /// Contexto inmutable que se pasa a cada state handler con todo lo necesario
    /// para procesar el turno sin depender del orquestador.
    /// </summary>
    public sealed class StateHandlerContext
    {
        public required string Telefono { get; init; }
        public required string DestinoRespuesta { get; init; }
        public required string TextoUsuario { get; init; }
        public required string Locale { get; init; }
        public required string? MsgId { get; init; }
        public required DateTime Inicio { get; init; }
        public required SesionConversacion Sesion { get; init; }

        // Servicios inyectados
        public required IProveedorIA Ia { get; init; }
        public required IProveedorReintegros Reintegros { get; init; }
        public required IReintegrosLocalizer Localizer { get; init; }
        public required ILogger Log { get; init; }

        // Helpers delegados al orquestador
        public required Func<string, CancellationToken, Task<SesionConversacion?>> ObtenerSesion { get; init; }
        public required Func<string, SesionConversacion, CancellationToken, Task> GuardarSesion { get; init; }
        public required Func<string, CancellationToken, Task<ResumenReintegro?>> ObtenerReintegroActual { get; init; }
        public required Func<string, ResumenReintegro, CancellationToken, Task> GuardarReintegroActual { get; init; }
        public required Func<string, CancellationToken, Task<List<ResumenReintegro>>> ObtenerListaReintegros { get; init; }
        public required Func<string, List<ResumenReintegro>, CancellationToken, Task> GuardarListaReintegros { get; init; }
        public required Func<string, string, CancellationToken, Task> IntentarEnviar { get; init; }
        public required Func<string, string, string?, string, string, string, DateTime, CancellationToken, Task> ResponderYRegistrar { get; init; }
        public required Func<string, UploadContext, CancellationToken, Task> GuardarUploadContext { get; init; }
    }
}
