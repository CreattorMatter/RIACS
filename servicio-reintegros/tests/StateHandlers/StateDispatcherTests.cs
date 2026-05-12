using System.Threading;
using System.Threading.Tasks;
using ServicioReintegros.AssistCard.Aplicacion.Servicios.StateHandlers;
using ServicioReintegros.AssistCard.Dominio.Entidades;
using ServicioReintegros.Tests.Helpers;
using Xunit;

namespace ServicioReintegros.Tests.StateHandlers
{
    public class StateDispatcherTests
    {
        private readonly StateDispatcher _dispatcher = new();

        [Theory]
        [InlineData(EstadoConversacion.AwaitingName)]
        [InlineData(EstadoConversacion.MenuPrincipal)]
        [InlineData(EstadoConversacion.OtrasConsultasMenu)]
        [InlineData(EstadoConversacion.OtrasConsultasProblem)]
        [InlineData(EstadoConversacion.AwaitingIdentifier)]
        [InlineData(EstadoConversacion.SelectingReintegro)]
        [InlineData(EstadoConversacion.ReintegroMenu)]
        [InlineData(EstadoConversacion.ReintegroFinancialMenu)]
        [InlineData(EstadoConversacion.ReintegroPaymentsMenu)]
        [InlineData(EstadoConversacion.ReintegroPendingDocsSelect)]
        [InlineData(EstadoConversacion.ReintegroPendingDocsAction)]
        [InlineData(EstadoConversacion.ReintegroPendingDocsAskContinue)]
        [InlineData(EstadoConversacion.ReintegroProblem)]
        [InlineData(EstadoConversacion.ReintegroExit)]
        public void Resolve_ParaCadaEstadoRegistrado_DevuelveHandler(EstadoConversacion estado)
        {
            var handler = _dispatcher.Resolve(estado);
            Assert.NotNull(handler);
            Assert.True(handler!.CanHandle(estado));
        }

        [Fact]
        public void Resolve_EstadoEnded_DevuelveNull()
        {
            Assert.Null(_dispatcher.Resolve(EstadoConversacion.Ended));
        }

        [Fact]
        public async Task DispatchAsync_EstadoSinHandler_DevuelveNull()
        {
            var builder = new TestContextBuilder()
                .ConTexto("test")
                .ConSesion(new SesionConversacion { Estado = EstadoConversacion.Ended });
            var ctx = builder.Build();

            var result = await _dispatcher.DispatchAsync(ctx, CancellationToken.None);
            Assert.Null(result);
        }

        [Fact]
        public async Task DispatchAsync_AwaitingName_DevuelveResultado()
        {
            var builder = new TestContextBuilder()
                .ConTexto("Juan Perez")
                .ConSesion(new SesionConversacion { Estado = EstadoConversacion.AwaitingName });
            var ctx = builder.Build();

            var result = await _dispatcher.DispatchAsync(ctx, CancellationToken.None);
            Assert.NotNull(result);
            Assert.True(result!.Procesado);
            Assert.False(string.IsNullOrEmpty(result.Mensaje));
        }
    }
}
