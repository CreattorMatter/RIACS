using System.Threading;
using System.Threading.Tasks;
using ServicioReintegros.AssistCard.Aplicacion.Servicios.StateHandlers;
using ServicioReintegros.AssistCard.Dominio.Entidades;
using ServicioReintegros.Tests.Helpers;
using Xunit;

namespace ServicioReintegros.Tests.StateHandlers
{
    public class AwaitingNameHandlerTests
    {
        private readonly AwaitingNameHandler _handler = new();

        [Fact]
        public void CanHandle_AwaitingName_True()
        {
            Assert.True(_handler.CanHandle(EstadoConversacion.AwaitingName));
        }

        [Fact]
        public void CanHandle_OtroEstado_False()
        {
            Assert.False(_handler.CanHandle(EstadoConversacion.MenuPrincipal));
        }

        [Fact]
        public async Task Handle_NombreValido_CambiaEstadoYDevuelveMenuPrincipal()
        {
            var sesion = new SesionConversacion { Estado = EstadoConversacion.AwaitingName };
            var builder = new TestContextBuilder()
                .ConTexto("Juan Perez")
                .ConSesion(sesion);
            var ctx = builder.Build();

            var result = await _handler.HandleAsync(ctx, CancellationToken.None);

            Assert.NotNull(result);
            Assert.True(result.Procesado);
            Assert.Equal("Juan Perez", sesion.Nombre);
            Assert.Equal(EstadoConversacion.MenuPrincipal, sesion.Estado);
            Assert.Single(builder.SesionesGuardadas);
        }

        [Fact]
        public async Task Handle_TextoVacio_PideNombre()
        {
            var sesion = new SesionConversacion { Estado = EstadoConversacion.AwaitingName };
            var builder = new TestContextBuilder()
                .ConTexto("")
                .ConSesion(sesion);
            var ctx = builder.Build();

            var result = await _handler.HandleAsync(ctx, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(EstadoConversacion.AwaitingName, sesion.Estado);
            Assert.Empty(builder.SesionesGuardadas);
        }

        [Fact]
        public async Task Handle_TextoNoEsNombre_PideNombreTrasConsulta()
        {
            var sesion = new SesionConversacion { Estado = EstadoConversacion.AwaitingName };
            var builder = new TestContextBuilder()
                .ConTexto("necesito ayuda con reintegro")
                .ConSesion(sesion);
            var ctx = builder.Build();

            var result = await _handler.HandleAsync(ctx, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(EstadoConversacion.AwaitingName, sesion.Estado);
            Assert.Empty(builder.SesionesGuardadas);
        }

        [Theory]
        [InlineData("es")]
        [InlineData("en")]
        [InlineData("pt")]
        public async Task Handle_NombreValido_RespetaLocale(string locale)
        {
            var sesion = new SesionConversacion { Estado = EstadoConversacion.AwaitingName };
            var builder = new TestContextBuilder()
                .ConTexto("Maria Garcia")
                .ConLocale(locale)
                .ConSesion(sesion);
            var ctx = builder.Build();

            var result = await _handler.HandleAsync(ctx, CancellationToken.None);

            Assert.NotNull(result);
            Assert.False(string.IsNullOrWhiteSpace(result.Mensaje));
        }
    }
}
