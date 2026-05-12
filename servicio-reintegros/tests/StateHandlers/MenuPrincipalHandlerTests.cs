using System.Threading;
using System.Threading.Tasks;
using ServicioReintegros.AssistCard.Aplicacion.Servicios.StateHandlers;
using ServicioReintegros.AssistCard.Dominio.Entidades;
using ServicioReintegros.Tests.Helpers;
using Xunit;

namespace ServicioReintegros.Tests.StateHandlers
{
    public class MenuPrincipalHandlerTests
    {
        private readonly MenuPrincipalHandler _handler = new();

        [Fact]
        public void CanHandle_MenuPrincipal_True()
        {
            Assert.True(_handler.CanHandle(EstadoConversacion.MenuPrincipal));
        }

        [Fact]
        public async Task Handle_Opcion1_CambiaAAwaitingIdentifier()
        {
            var sesion = new SesionConversacion { Estado = EstadoConversacion.MenuPrincipal, Nombre = "Juan" };
            var builder = new TestContextBuilder()
                .ConTexto("1")
                .ConSesion(sesion);
            var ctx = builder.Build();

            var result = await _handler.HandleAsync(ctx, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(EstadoConversacion.AwaitingIdentifier, sesion.Estado);
            Assert.Single(builder.SesionesGuardadas);
        }

        [Fact]
        public async Task Handle_Opcion2_CambiaAOtrasConsultasMenu()
        {
            var sesion = new SesionConversacion { Estado = EstadoConversacion.MenuPrincipal, Nombre = "Juan" };
            var builder = new TestContextBuilder()
                .ConTexto("2")
                .ConSesion(sesion);
            var ctx = builder.Build();

            var result = await _handler.HandleAsync(ctx, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(EstadoConversacion.OtrasConsultasMenu, sesion.Estado);
            Assert.Single(builder.SesionesGuardadas);
        }

        [Fact]
        public async Task Handle_TextoInvalido_RepiteMenu()
        {
            var sesion = new SesionConversacion { Estado = EstadoConversacion.MenuPrincipal, Nombre = "Juan" };
            var builder = new TestContextBuilder()
                .ConTexto("algo random")
                .ConSesion(sesion);
            var ctx = builder.Build();

            var result = await _handler.HandleAsync(ctx, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(EstadoConversacion.MenuPrincipal, sesion.Estado);
            Assert.Empty(builder.SesionesGuardadas);
        }
    }
}
