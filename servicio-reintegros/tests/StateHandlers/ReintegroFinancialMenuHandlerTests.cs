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
    public class ReintegroFinancialMenuHandlerTests
    {
        private readonly ReintegroFinancialMenuHandler _handler = new();

        [Fact]
        public void CanHandle_ReintegroFinancialMenu_True()
        {
            Assert.True(_handler.CanHandle(EstadoConversacion.ReintegroFinancialMenu));
        }

        [Fact]
        public async Task Handle_OpcionVolver_VuelveAReintegroMenu()
        {
            var reintegro = TestContextBuilder.CrearReintegroTest();
            var opciones = BotMessages.OpcionesFinanciero("es");
            var sesion = new SesionConversacion { Estado = EstadoConversacion.ReintegroFinancialMenu, Nombre = "Juan" };
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
        public async Task Handle_OpcionAprobacion_EnRevision_MuestraMensajeRevision()
        {
            var reintegro = TestContextBuilder.CrearReintegroTest(statusOriginal: "Under Review");
            var sesion = new SesionConversacion { Estado = EstadoConversacion.ReintegroFinancialMenu, Nombre = "Juan" };
            var builder = new TestContextBuilder()
                .ConTexto("1")
                .ConSesion(sesion)
                .ConReintegroActual(reintegro);
            var ctx = builder.Build();

            var result = await _handler.HandleAsync(ctx, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Contains(BotMessages.MenuFinanciero("es"), result.Mensaje);
        }

        [Fact]
        public async Task Handle_OpcionAprobacion_NoEnRevision_LlamaIA()
        {
            var reintegro = TestContextBuilder.CrearReintegroTest(statusOriginal: "Approved");
            var sesion = new SesionConversacion { Estado = EstadoConversacion.ReintegroFinancialMenu, Nombre = "Juan" };
            var builder = new TestContextBuilder()
                .ConTexto("1")
                .ConSesion(sesion)
                .ConReintegroActual(reintegro);

            builder.MockIa
                .Setup(ia => ia.ResponderFaqAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("Detalle de aprobación...");

            var ctx = builder.Build();
            var result = await _handler.HandleAsync(ctx, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(EstadoConversacion.ReintegroExit, sesion.Estado);
            Assert.Equal(EstadoConversacion.ReintegroFinancialMenu, sesion.EstadoVolver);
        }

        [Fact]
        public async Task Handle_TextoInvalido_RepiteMenu()
        {
            var sesion = new SesionConversacion { Estado = EstadoConversacion.ReintegroFinancialMenu, Nombre = "Juan" };
            var builder = new TestContextBuilder()
                .ConTexto("texto random")
                .ConSesion(sesion);
            var ctx = builder.Build();

            var result = await _handler.HandleAsync(ctx, CancellationToken.None);

            Assert.NotNull(result);
        }
    }
}
