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
    public class ReintegroPaymentsMenuHandlerTests
    {
        private readonly ReintegroPaymentsMenuHandler _handler = new();

        [Fact]
        public void CanHandle_ReintegroPaymentsMenu_True()
        {
            Assert.True(_handler.CanHandle(EstadoConversacion.ReintegroPaymentsMenu));
        }

        [Fact]
        public async Task Handle_OpcionVolver_VuelveAReintegroMenu()
        {
            var reintegro = TestContextBuilder.CrearReintegroTest();
            var opciones = BotMessages.OpcionesPagos("es");
            var sesion = new SesionConversacion { Estado = EstadoConversacion.ReintegroPaymentsMenu, Nombre = "Juan" };
            var builder = new TestContextBuilder()
                .ConTexto(opciones.Count.ToString())
                .ConSesion(sesion)
                .ConReintegroActual(reintegro);
            var ctx = builder.Build();

            var result = await _handler.HandleAsync(ctx, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(EstadoConversacion.ReintegroMenu, sesion.Estado);
        }

        [Fact]
        public async Task Handle_OpcionTipoCambio_LlamaIAyVaAExit()
        {
            var reintegro = TestContextBuilder.CrearReintegroTest();
            var sesion = new SesionConversacion { Estado = EstadoConversacion.ReintegroPaymentsMenu, Nombre = "Juan" };
            var builder = new TestContextBuilder()
                .ConTexto("1")
                .ConSesion(sesion)
                .ConReintegroActual(reintegro);

            builder.MockIa
                .Setup(ia => ia.ResponderFaqAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("El tipo de cambio...");

            var ctx = builder.Build();
            var result = await _handler.HandleAsync(ctx, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(EstadoConversacion.ReintegroExit, sesion.Estado);
            Assert.Contains("tipo de cambio", result.Mensaje);
        }

        [Fact]
        public async Task Handle_TextoInvalido_RepiteMenu()
        {
            var sesion = new SesionConversacion { Estado = EstadoConversacion.ReintegroPaymentsMenu, Nombre = "Juan" };
            var builder = new TestContextBuilder()
                .ConTexto("texto random")
                .ConSesion(sesion);
            var ctx = builder.Build();

            var result = await _handler.HandleAsync(ctx, CancellationToken.None);

            Assert.NotNull(result);
        }
    }
}
