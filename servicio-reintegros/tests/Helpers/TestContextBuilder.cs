using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using ServicioReintegros.AssistCard.Aplicacion.Dtos;
using ServicioReintegros.AssistCard.Aplicacion.Puertos;
using ServicioReintegros.AssistCard.Aplicacion.Servicios;
using ServicioReintegros.AssistCard.Aplicacion.Servicios.StateHandlers;
using ServicioReintegros.AssistCard.Dominio.Entidades;

namespace ServicioReintegros.Tests.Helpers
{
    public sealed class TestContextBuilder
    {
        private string _telefono = "+5491100000000";
        private string _destino = "+5491100000000";
        private string _texto = "";
        private string _locale = "es";
        private string? _msgId = "msg1";
        private DateTime _inicio = DateTime.UtcNow;
        private SesionConversacion _sesion = new();
        private ResumenReintegro? _reintegroActual;
        private List<ResumenReintegro> _listaReintegros = new();

        public Mock<IProveedorIA> MockIa { get; } = new();
        public Mock<IProveedorReintegros> MockReintegros { get; } = new();
        public Mock<IReintegrosLocalizer> MockLocalizer { get; } = new();
        public Mock<ILogger> MockLog { get; } = new();

        public List<(string Tel, SesionConversacion Sesion)> SesionesGuardadas { get; } = new();
        public List<(string Tel, ResumenReintegro R)> ReintegrosGuardados { get; } = new();
        public List<(string Tel, List<ResumenReintegro> L)> ListasGuardadas { get; } = new();
        public List<(string Dest, string Texto)> MensajesEnviados { get; } = new();
        public List<(string Tel, UploadContext Ctx)> UploadContextsGuardados { get; } = new();

        public TestContextBuilder ConTexto(string texto) { _texto = texto; return this; }
        public TestContextBuilder ConLocale(string locale) { _locale = locale; return this; }
        public TestContextBuilder ConSesion(SesionConversacion sesion) { _sesion = sesion; return this; }
        public TestContextBuilder ConTelefono(string tel) { _telefono = tel; _destino = tel; return this; }
        public TestContextBuilder ConReintegroActual(ResumenReintegro? r) { _reintegroActual = r; return this; }
        public TestContextBuilder ConListaReintegros(List<ResumenReintegro> l) { _listaReintegros = l; return this; }

        public StateHandlerContext Build()
        {
            return new StateHandlerContext
            {
                Telefono = _telefono,
                DestinoRespuesta = _destino,
                TextoUsuario = _texto,
                Locale = _locale,
                MsgId = _msgId,
                Inicio = _inicio,
                Sesion = _sesion,
                Ia = MockIa.Object,
                Reintegros = MockReintegros.Object,
                Localizer = MockLocalizer.Object,
                Log = MockLog.Object,
                ObtenerSesion = (tel, ct) => Task.FromResult<SesionConversacion?>(_sesion),
                GuardarSesion = (tel, ses, ct) => { SesionesGuardadas.Add((tel, ses)); return Task.CompletedTask; },
                ObtenerReintegroActual = (tel, ct) => Task.FromResult(_reintegroActual),
                GuardarReintegroActual = (tel, r, ct) => { ReintegrosGuardados.Add((tel, r)); return Task.CompletedTask; },
                ObtenerListaReintegros = (tel, ct) => Task.FromResult(_listaReintegros),
                GuardarListaReintegros = (tel, l, ct) => { ListasGuardadas.Add((tel, l)); return Task.CompletedTask; },
                IntentarEnviar = (dest, txt, ct) => { MensajesEnviados.Add((dest, txt)); return Task.CompletedTask; },
                ResponderYRegistrar = (tel, dest, mid, loc, txtIn, txtOut, ini, ct) => Task.CompletedTask,
                GuardarUploadContext = (tel, up, ct) => { UploadContextsGuardados.Add((tel, up)); return Task.CompletedTask; }
            };
        }

        public static ResumenReintegro CrearReintegroTest(
            string? benefitId = "12345",
            string? caseId = "ABC1234",
            string? status = "Approved",
            string? statusOriginal = "Approved",
            string? benefitType = "Medical",
            decimal? totalRequested = 100m,
            decimal? totalApproved = 80m)
        {
            return new ResumenReintegro
            {
                BenefitId = benefitId,
                CaseId = caseId,
                Status = status,
                StatusOriginal = statusOriginal,
                BenefitType = benefitType,
                BenefitTypeOriginal = benefitType,
                TotalRequestedUsd = totalRequested,
                TotalApprovedUsd = totalApproved,
                CreateDate = DateTime.UtcNow.AddDays(-5)
            };
        }
    }
}
