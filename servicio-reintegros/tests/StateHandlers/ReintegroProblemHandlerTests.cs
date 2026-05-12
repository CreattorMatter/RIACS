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
    public class ReintegroProblemHandlerTests
    {
        private readonly ReintegroProblemHandler _handler = new();

        [Fact]
        public void CanHandle_ReintegroProblem_True()
        {
            Assert.True(_handler.CanHandle(EstadoConversacion.ReintegroProblem));
        }

        [Fact]
        public async Task Handle_OpcionCerrar_CambiaAEnded()
        {
            var sesion = new SesionConversacion { Estado = EstadoConversacion.ReintegroProblem, Nombre = "Juan" };
            var builder = new TestContextBuilder()
                .ConTexto("2")
                .ConSesion(sesion);
            var ctx = builder.Build();

            var result = await _handler.HandleAsync(ctx, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(EstadoConversacion.Ended, sesion.Estado);
        }

        [Fact]
        public async Task Handle_OpcionVolver_VaAReintegroMenu()
        {
            var reintegro = TestContextBuilder.CrearReintegroTest();
            var sesion = new SesionConversacion { Estado = EstadoConversacion.ReintegroProblem, Nombre = "Juan" };
            var builder = new TestContextBuilder()
                .ConTexto("1")
                .ConSesion(sesion)
                .ConReintegroActual(reintegro);
            var ctx = builder.Build();

            var result = await _handler.HandleAsync(ctx, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(EstadoConversacion.ReintegroMenu, sesion.Estado);
        }

        [Fact]
        public async Task Handle_ProblemaGenerico_LlamaIAYVaAExit()
        {
            var reintegro = TestContextBuilder.CrearReintegroTest();
            var sesion = new SesionConversacion { Estado = EstadoConversacion.ReintegroProblem, Nombre = "Juan" };
            var builder = new TestContextBuilder()
                .ConTexto("tengo un problema con mi reintegro")
                .ConSesion(sesion)
                .ConReintegroActual(reintegro);

            builder.MockIa
                .Setup(ia => ia.EnviarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IaRequestContext?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AgentResponse { Message = "Entendemos tu problema..." });

            var ctx = builder.Build();
            var result = await _handler.HandleAsync(ctx, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(EstadoConversacion.ReintegroExit, sesion.Estado);
            Assert.Contains("Entendemos tu problema", result.Mensaje);
        }

        [Fact]
        public async Task Handle_ProblemaGenerico_IASinRespuesta_UsaFallbackNoVacio()
        {
            var reintegro = TestContextBuilder.CrearReintegroTest();
            var sesion = new SesionConversacion { Estado = EstadoConversacion.ReintegroProblem, Nombre = "Juan" };
            var builder = new TestContextBuilder()
                .ConTexto("el resumen del reintegro no me queda claro")
                .ConSesion(sesion)
                .ConReintegroActual(reintegro);

            builder.MockIa
                .Setup(ia => ia.EnviarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IaRequestContext?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AgentResponse { Message = null, Error = "FoundryResponsesFailed" });

            var ctx = builder.Build();
            var result = await _handler.HandleAsync(ctx, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(EstadoConversacion.ReintegroExit, sesion.Estado);
            Assert.False(string.IsNullOrWhiteSpace(result.Mensaje));
            Assert.Contains("No pude generar", result.Mensaje);
        }

        [Fact]
        public async Task Handle_ReclamoDemora_ConReintegro_LlamaIAConPoliticaNoAceleracion()
        {
            var reintegro = TestContextBuilder.CrearReintegroTest();
            var sesion = new SesionConversacion { Estado = EstadoConversacion.ReintegroProblem, Nombre = "Juan" };
            var builder = new TestContextBuilder()
                .ConTexto("hace meses que no me pagan el reintegro, demora mucho")
                .ConSesion(sesion)
                .ConReintegroActual(reintegro);

            builder.MockIa
                .Setup(ia => ia.EnviarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IaRequestContext?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AgentResponse { Message = "Entendemos la demora..." });

            var ctx = builder.Build();
            var result = await _handler.HandleAsync(ctx, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(EstadoConversacion.ReintegroExit, sesion.Estado);
        }

        [Fact]
        public async Task Handle_ReclamoDemora_FoundryFalla_UsaFallback()
        {
            var reintegro = TestContextBuilder.CrearReintegroTest();
            var sesion = new SesionConversacion { Estado = EstadoConversacion.ReintegroProblem, Nombre = "Juan" };
            var builder = new TestContextBuilder()
                .ConTexto("hace meses que no me pagan el reintegro, demora mucho")
                .ConSesion(sesion)
                .ConReintegroActual(reintegro);

            builder.MockIa
                .Setup(ia => ia.EnviarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IaRequestContext?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AgentResponse { Message = null, Error = "FoundryKnowledgeIndexDuplicated" });

            var ctx = builder.Build();
            var result = await _handler.HandleAsync(ctx, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(EstadoConversacion.ReintegroExit, sesion.Estado);
            Assert.False(string.IsNullOrWhiteSpace(result.Mensaje));
        }
    }
}
