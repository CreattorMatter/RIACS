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
    public class ReintegroExitHandlerTests
    {
        private readonly ReintegroExitHandler _handler = new();

        [Fact]
        public void CanHandle_ReintegroExit_True()
        {
            Assert.True(_handler.CanHandle(EstadoConversacion.ReintegroExit));
        }

        [Fact]
        public async Task Handle_TextoLibre_ConsultaFaqPorIA()
        {
            var sesion = new SesionConversacion { Estado = EstadoConversacion.ReintegroExit, Nombre = "Juan" };
            var builder = new TestContextBuilder()
                .ConTexto("¿cuánto tarda el reintegro?")
                .ConSesion(sesion);

            builder.MockIa
                .Setup(ia => ia.EnviarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IaRequestContext?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AgentResponse { Message = "Normalmente tarda 20 días." });

            var ctx = builder.Build();
            var result = await _handler.HandleAsync(ctx, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Contains("20 días", result.Mensaje);
            Assert.Equal(EstadoConversacion.ReintegroExit, sesion.Estado);
        }

        [Fact]
        public async Task Handle_OpcionCerrar_CambiaAEnded()
        {
            var opciones = ServicioReintegros.AssistCard.Aplicacion.Servicios.BotMessages.OpcionesExit("es");
            var sesion = new SesionConversacion { Estado = EstadoConversacion.ReintegroExit, Nombre = "Juan" };
            var builder = new TestContextBuilder()
                .ConTexto("2")
                .ConSesion(sesion);
            var ctx = builder.Build();

            var result = await _handler.HandleAsync(ctx, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(EstadoConversacion.Ended, sesion.Estado);
        }

        [Fact]
        public async Task Handle_OpcionVolver_ConBenefitId_VaAReintegroMenu()
        {
            var reintegro = TestContextBuilder.CrearReintegroTest();
            var sesion = new SesionConversacion
            {
                Estado = EstadoConversacion.ReintegroExit,
                Nombre = "Juan",
                CurrentBenefitId = "12345"
            };
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
        public async Task Handle_OpcionVolver_SinContexto_VaAOtrasConsultasMenu()
        {
            var sesion = new SesionConversacion
            {
                Estado = EstadoConversacion.ReintegroExit,
                Nombre = "Juan",
                CurrentBenefitId = null,
                CurrentCaseId = null
            };
            var builder = new TestContextBuilder()
                .ConTexto("1")
                .ConSesion(sesion);
            var ctx = builder.Build();

            var result = await _handler.HandleAsync(ctx, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(EstadoConversacion.OtrasConsultasMenu, sesion.Estado);
        }
    }
}
