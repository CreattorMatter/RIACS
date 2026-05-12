using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ServicioReintegros.AssistCard.Aplicacion.Dtos;
using ServicioReintegros.AssistCard.Aplicacion.Servicios;
using ServicioReintegros.AssistCard.Aplicacion.Servicios.StateHandlers;
using ServicioReintegros.AssistCard.Dominio.Entidades;
using ServicioReintegros.Tests.Helpers;
using Xunit;

namespace ServicioReintegros.Tests.StateHandlers
{
    public class PendingDocsSelectHandlerTests
    {
        private readonly PendingDocsSelectHandler _handler = new();

        [Fact]
        public void CanHandle_PendingDocsSelect_True()
        {
            Assert.True(_handler.CanHandle(EstadoConversacion.ReintegroPendingDocsSelect));
        }

        [Fact]
        public async Task Handle_SinReintegro_VuelveAReintegroMenu()
        {
            var sesion = new SesionConversacion { Estado = EstadoConversacion.ReintegroPendingDocsSelect, Nombre = "Juan" };
            var builder = new TestContextBuilder()
                .ConTexto("1")
                .ConSesion(sesion)
                .ConReintegroActual(null);
            var ctx = builder.Build();

            var result = await _handler.HandleAsync(ctx, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(EstadoConversacion.ReintegroMenu, sesion.Estado);
        }

        [Fact]
        public async Task Handle_TextoInvalido_RepiteMenu()
        {
            var reintegro = TestContextBuilder.CrearReintegroTest();
            reintegro.PendingDocuments = new List<DocumentoPendiente>
            {
                new() { DocumentId = 1, DocumentType = "Factura", Description = "Factura médica" }
            };
            var sesion = new SesionConversacion { Estado = EstadoConversacion.ReintegroPendingDocsSelect, Nombre = "Juan" };
            var builder = new TestContextBuilder()
                .ConTexto("texto random")
                .ConSesion(sesion)
                .ConReintegroActual(reintegro);
            var ctx = builder.Build();

            var result = await _handler.HandleAsync(ctx, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(EstadoConversacion.ReintegroPendingDocsSelect, sesion.Estado);
        }
    }

    public class PendingDocsActionHandlerTests
    {
        private readonly PendingDocsActionHandler _handler = new();

        [Fact]
        public void CanHandle_PendingDocsAction_True()
        {
            Assert.True(_handler.CanHandle(EstadoConversacion.ReintegroPendingDocsAction));
        }

        [Fact]
        public async Task Handle_OpcionCerrar_CambiaAEnded()
        {
            var sesion = new SesionConversacion { Estado = EstadoConversacion.ReintegroPendingDocsAction, Nombre = "Juan" };
            var builder = new TestContextBuilder()
                .ConTexto("3")
                .ConSesion(sesion);
            var ctx = builder.Build();

            var result = await _handler.HandleAsync(ctx, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(EstadoConversacion.Ended, sesion.Estado);
        }

        [Fact]
        public async Task Handle_OpcionVolverADocs_VuelveAPendingDocsSelect()
        {
            var reintegro = TestContextBuilder.CrearReintegroTest();
            var sesion = new SesionConversacion { Estado = EstadoConversacion.ReintegroPendingDocsAction, Nombre = "Juan" };
            var builder = new TestContextBuilder()
                .ConTexto("2")
                .ConSesion(sesion)
                .ConReintegroActual(reintegro);
            var ctx = builder.Build();

            var result = await _handler.HandleAsync(ctx, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(EstadoConversacion.ReintegroPendingDocsSelect, sesion.Estado);
        }

        [Fact]
        public async Task Handle_OpcionCargar_GuardaUploadContext()
        {
            var reintegro = TestContextBuilder.CrearReintegroTest(benefitId: "99999");
            var sesion = new SesionConversacion
            {
                Estado = EstadoConversacion.ReintegroPendingDocsAction,
                Nombre = "Juan",
                PendingDocIdSeleccionado = 42
            };
            var builder = new TestContextBuilder()
                .ConTexto("1")
                .ConSesion(sesion)
                .ConReintegroActual(reintegro);
            var ctx = builder.Build();

            var result = await _handler.HandleAsync(ctx, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Single(builder.UploadContextsGuardados);
            Assert.Equal(42, builder.UploadContextsGuardados[0].Ctx.DocumentId);
        }

        [Fact]
        public async Task Handle_TextoInvalido_RepiteMenu()
        {
            var sesion = new SesionConversacion { Estado = EstadoConversacion.ReintegroPendingDocsAction, Nombre = "Juan" };
            var builder = new TestContextBuilder()
                .ConTexto("texto random")
                .ConSesion(sesion);
            var ctx = builder.Build();

            var result = await _handler.HandleAsync(ctx, CancellationToken.None);

            Assert.NotNull(result);
        }
    }

    public class PendingDocsAskContinueHandlerTests
    {
        private readonly PendingDocsAskContinueHandler _handler = new();

        [Fact]
        public void CanHandle_PendingDocsAskContinue_True()
        {
            Assert.True(_handler.CanHandle(EstadoConversacion.ReintegroPendingDocsAskContinue));
        }

        [Fact]
        public async Task Handle_Si_VuelveAPendingDocsSelect()
        {
            var reintegro = TestContextBuilder.CrearReintegroTest();
            var sesion = new SesionConversacion { Estado = EstadoConversacion.ReintegroPendingDocsAskContinue, Nombre = "Juan" };
            var builder = new TestContextBuilder()
                .ConTexto("1")
                .ConSesion(sesion)
                .ConReintegroActual(reintegro);
            var ctx = builder.Build();

            var result = await _handler.HandleAsync(ctx, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(EstadoConversacion.ReintegroPendingDocsSelect, sesion.Estado);
        }

        [Fact]
        public async Task Handle_No_VuelveAReintegroMenu()
        {
            var reintegro = TestContextBuilder.CrearReintegroTest();
            var sesion = new SesionConversacion
            {
                Estado = EstadoConversacion.ReintegroPendingDocsAskContinue,
                Nombre = "Juan",
                CurrentBenefitId = "12345"
            };
            var builder = new TestContextBuilder()
                .ConTexto("2")
                .ConSesion(sesion)
                .ConReintegroActual(reintegro);

            builder.MockReintegros
                .Setup(r => r.BuscarPorIdentificadorAsync(It.IsAny<ConsultaIdentificador>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ResumenReintegro> { reintegro });

            var ctx = builder.Build();
            var result = await _handler.HandleAsync(ctx, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(EstadoConversacion.ReintegroMenu, sesion.Estado);
        }

        [Fact]
        public async Task Handle_TextoInvalido_RepitePregunta()
        {
            var sesion = new SesionConversacion { Estado = EstadoConversacion.ReintegroPendingDocsAskContinue, Nombre = "Juan" };
            var builder = new TestContextBuilder()
                .ConTexto("algo random")
                .ConSesion(sesion);
            var ctx = builder.Build();

            var result = await _handler.HandleAsync(ctx, CancellationToken.None);

            Assert.NotNull(result);
        }
    }
}
