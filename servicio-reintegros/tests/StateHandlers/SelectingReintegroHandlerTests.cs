using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ServicioReintegros.AssistCard.Aplicacion.Dtos;
using ServicioReintegros.AssistCard.Aplicacion.Servicios.StateHandlers;
using ServicioReintegros.AssistCard.Dominio.Entidades;
using ServicioReintegros.Tests.Helpers;
using Xunit;

namespace ServicioReintegros.Tests.StateHandlers
{
    public class SelectingReintegroHandlerTests
    {
        private readonly SelectingReintegroHandler _handler = new();

        [Fact]
        public void CanHandle_SelectingReintegro_True()
        {
            Assert.True(_handler.CanHandle(EstadoConversacion.SelectingReintegro));
        }

        [Fact]
        public async Task Handle_ListaVacia_VuelveAAwaitingIdentifier()
        {
            var sesion = new SesionConversacion { Estado = EstadoConversacion.SelectingReintegro, Nombre = "Juan" };
            var builder = new TestContextBuilder()
                .ConTexto("1")
                .ConSesion(sesion)
                .ConListaReintegros(new List<ResumenReintegro>());
            var ctx = builder.Build();

            var result = await _handler.HandleAsync(ctx, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(EstadoConversacion.AwaitingIdentifier, sesion.Estado);
        }

        [Fact]
        public async Task Handle_SeleccionaReintegroValido_VaAReintegroMenu()
        {
            var r1 = TestContextBuilder.CrearReintegroTest(benefitId: "111", caseId: "CASE001");
            var r2 = TestContextBuilder.CrearReintegroTest(benefitId: "222", caseId: "CASE002");
            var lista = new List<ResumenReintegro> { r1, r2 };

            var sesion = new SesionConversacion { Estado = EstadoConversacion.SelectingReintegro, Nombre = "Juan" };
            var builder = new TestContextBuilder()
                .ConTexto("1")
                .ConSesion(sesion)
                .ConListaReintegros(lista);
            var ctx = builder.Build();

            var result = await _handler.HandleAsync(ctx, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(EstadoConversacion.ReintegroMenu, sesion.Estado);
            Assert.Equal("111", sesion.CurrentBenefitId);
        }

        [Fact]
        public async Task Handle_OpcionInvalida_RepiteMenu()
        {
            var r1 = TestContextBuilder.CrearReintegroTest(benefitId: "111");
            var lista = new List<ResumenReintegro> { r1 };

            var sesion = new SesionConversacion { Estado = EstadoConversacion.SelectingReintegro, Nombre = "Juan" };
            var builder = new TestContextBuilder()
                .ConTexto("texto invalido")
                .ConSesion(sesion)
                .ConListaReintegros(lista);
            var ctx = builder.Build();

            var result = await _handler.HandleAsync(ctx, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(EstadoConversacion.SelectingReintegro, sesion.Estado);
        }
    }
}
