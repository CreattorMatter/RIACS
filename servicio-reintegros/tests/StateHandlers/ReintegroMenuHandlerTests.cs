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
    public class ReintegroMenuHandlerTests
    {
        private readonly ReintegroMenuHandler _handler = new();

        [Fact]
        public void CanHandle_ReintegroMenu_True()
        {
            Assert.True(_handler.CanHandle(EstadoConversacion.ReintegroMenu));
        }

        [Fact]
        public async Task Handle_SinReintegroActual_VuelveAAwaitingIdentifier()
        {
            var sesion = new SesionConversacion { Estado = EstadoConversacion.ReintegroMenu, Nombre = "Juan" };
            var builder = new TestContextBuilder()
                .ConTexto("1")
                .ConSesion(sesion)
                .ConReintegroActual(null);
            var ctx = builder.Build();

            var result = await _handler.HandleAsync(ctx, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(EstadoConversacion.AwaitingIdentifier, sesion.Estado);
        }

        [Fact]
        public async Task Handle_OpcionVolver_VuelveAMenuPrincipal()
        {
            var reintegro = TestContextBuilder.CrearReintegroTest();
            var sesion = new SesionConversacion
            {
                Estado = EstadoConversacion.ReintegroMenu,
                Nombre = "Juan",
                CurrentBenefitId = "12345"
            };

            var opciones = BotMessages.ConstruirOpcionesReintegroMenu(reintegro, "es");
            var volverIdx = opciones.Count;

            var builder = new TestContextBuilder()
                .ConTexto(volverIdx.ToString())
                .ConSesion(sesion)
                .ConReintegroActual(reintegro);
            var ctx = builder.Build();

            var result = await _handler.HandleAsync(ctx, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(EstadoConversacion.MenuPrincipal, sesion.Estado);
            Assert.Null(sesion.CurrentBenefitId);
        }

        [Fact]
        public async Task Handle_OpcionInvalida_RepiteMenu()
        {
            var reintegro = TestContextBuilder.CrearReintegroTest();
            var sesion = new SesionConversacion { Estado = EstadoConversacion.ReintegroMenu, Nombre = "Juan" };
            var builder = new TestContextBuilder()
                .ConTexto("texto random")
                .ConSesion(sesion)
                .ConReintegroActual(reintegro);
            var ctx = builder.Build();

            var result = await _handler.HandleAsync(ctx, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(EstadoConversacion.ReintegroMenu, sesion.Estado);
        }

        [Fact]
        public async Task Handle_OpcionDetalleEstado_LlamaIAyVaAExit()
        {
            var reintegro = TestContextBuilder.CrearReintegroTest(status: "Approved");
            var sesion = new SesionConversacion { Estado = EstadoConversacion.ReintegroMenu, Nombre = "Juan" };

            var opcionEstado = BotMessages.OpcionDetalleEstado("es");

            var builder = new TestContextBuilder()
                .ConTexto(opcionEstado)
                .ConSesion(sesion)
                .ConReintegroActual(reintegro);

            builder.MockIa
                .Setup(ia => ia.ExplicarEstadoAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("El estado es aprobado");

            var ctx = builder.Build();
            var result = await _handler.HandleAsync(ctx, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(EstadoConversacion.ReintegroExit, sesion.Estado);
            Assert.Contains("aprobado", result.Mensaje);
        }
    }
}
