using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ServicioReintegros.AssistCard.Aplicacion.Dtos;
using ServicioReintegros.AssistCard.Aplicacion.Servicios.StateHandlers;
using ServicioReintegros.AssistCard.Dominio.Entidades;
using ServicioReintegros.Tests.Helpers;
using Xunit;

namespace ServicioReintegros.Tests.StateHandlers
{
    public class AwaitingIdentifierHandlerTests
    {
        private readonly AwaitingIdentifierHandler _handler = new();

        [Fact]
        public void CanHandle_AwaitingIdentifier_True()
        {
            Assert.True(_handler.CanHandle(EstadoConversacion.AwaitingIdentifier));
        }

        [Fact]
        public async Task Handle_TextoNoEsIdentificador_PideIdentificador()
        {
            var sesion = new SesionConversacion { Estado = EstadoConversacion.AwaitingIdentifier, Nombre = "Juan" };
            var builder = new TestContextBuilder()
                .ConTexto("hola como estas")
                .ConSesion(sesion);
            var ctx = builder.Build();

            var result = await _handler.HandleAsync(ctx, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(EstadoConversacion.AwaitingIdentifier, sesion.Estado);
        }

        [Fact]
        public async Task Handle_IdentificadorValido_UnResultado_VaAReintegroMenu()
        {
            var reintegro = TestContextBuilder.CrearReintegroTest();
            var sesion = new SesionConversacion { Estado = EstadoConversacion.AwaitingIdentifier, Nombre = "Juan" };
            var builder = new TestContextBuilder()
                .ConTexto("73124")
                .ConSesion(sesion);

            builder.MockReintegros
                .Setup(r => r.BuscarPorIdentificadorAsync(It.IsAny<ConsultaIdentificador>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ResumenReintegro> { reintegro });

            var ctx = builder.Build();
            var result = await _handler.HandleAsync(ctx, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(EstadoConversacion.ReintegroMenu, sesion.Estado);
            Assert.Equal(reintegro.BenefitId, sesion.CurrentBenefitId);
            Assert.Single(builder.ReintegrosGuardados);
        }

        [Fact]
        public async Task Handle_IdentificadorValido_MultipleResultados_VaASelectingReintegro()
        {
            var r1 = TestContextBuilder.CrearReintegroTest(benefitId: "111");
            var r2 = TestContextBuilder.CrearReintegroTest(benefitId: "222");
            var sesion = new SesionConversacion { Estado = EstadoConversacion.AwaitingIdentifier, Nombre = "Juan" };
            var builder = new TestContextBuilder()
                .ConTexto("test@mail.com")
                .ConSesion(sesion);

            builder.MockReintegros
                .Setup(r => r.BuscarPorIdentificadorAsync(It.IsAny<ConsultaIdentificador>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ResumenReintegro> { r1, r2 });

            var ctx = builder.Build();
            var result = await _handler.HandleAsync(ctx, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(EstadoConversacion.SelectingReintegro, sesion.Estado);
            Assert.Single(builder.ListasGuardadas);
        }

        [Fact]
        public async Task Handle_IdentificadorValido_SinResultados_MuestraMensajeNoEncontrado()
        {
            var sesion = new SesionConversacion { Estado = EstadoConversacion.AwaitingIdentifier, Nombre = "Juan" };
            var builder = new TestContextBuilder()
                .ConTexto("73124")
                .ConSesion(sesion);

            builder.MockReintegros
                .Setup(r => r.BuscarPorIdentificadorAsync(It.IsAny<ConsultaIdentificador>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ResumenReintegro>());

            var ctx = builder.Build();
            var result = await _handler.HandleAsync(ctx, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(EstadoConversacion.AwaitingIdentifier, sesion.Estado);
        }

        [Fact]
        public async Task Handle_ErrorEnBusqueda_DevuelveMensajeError()
        {
            var sesion = new SesionConversacion { Estado = EstadoConversacion.AwaitingIdentifier, Nombre = "Juan" };
            var builder = new TestContextBuilder()
                .ConTexto("73124")
                .ConSesion(sesion);

            builder.MockReintegros
                .Setup(r => r.BuscarPorIdentificadorAsync(It.IsAny<ConsultaIdentificador>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("API error"));

            var ctx = builder.Build();
            var result = await _handler.HandleAsync(ctx, CancellationToken.None);

            Assert.NotNull(result);
            Assert.False(string.IsNullOrWhiteSpace(result.Mensaje));
        }
    }
}
