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
    public class OtrasConsultasMenuHandlerTests
    {
        private readonly OtrasConsultasMenuHandler _handler = new();

        [Fact]
        public void CanHandle_OtrasConsultasMenu_True()
        {
            Assert.True(_handler.CanHandle(EstadoConversacion.OtrasConsultasMenu));
        }

        [Fact]
        public async Task Handle_OpcionVolver_VuelveAMenuPrincipal()
        {
            var opciones = ServicioReintegros.AssistCard.Aplicacion.Servicios.BotMessages.OpcionesOtrasConsultas("es");
            var sesion = new SesionConversacion { Estado = EstadoConversacion.OtrasConsultasMenu, Nombre = "Juan" };
            var builder = new TestContextBuilder()
                .ConTexto(opciones.Count.ToString())
                .ConSesion(sesion);
            var ctx = builder.Build();

            var result = await _handler.HandleAsync(ctx, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(EstadoConversacion.MenuPrincipal, sesion.Estado);
        }

        [Fact]
        public async Task Handle_OpcionRequisitos_LlamaIA()
        {
            var sesion = new SesionConversacion { Estado = EstadoConversacion.OtrasConsultasMenu, Nombre = "Juan" };
            var builder = new TestContextBuilder()
                .ConTexto("1")
                .ConSesion(sesion);

            builder.MockIa
                .Setup(ia => ia.EnviarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IaRequestContext?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AgentResponse { Message = "Los requisitos son..." });

            var ctx = builder.Build();
            var result = await _handler.HandleAsync(ctx, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(EstadoConversacion.ReintegroExit, sesion.Estado);
        }

        [Fact]
        public async Task Handle_OpcionProblemasApp_LlamaIA()
        {
            var sesion = new SesionConversacion { Estado = EstadoConversacion.OtrasConsultasMenu, Nombre = "Juan" };
            var builder = new TestContextBuilder()
                .ConTexto("2")
                .ConSesion(sesion);

            builder.MockIa
                .Setup(ia => ia.EnviarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IaRequestContext?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AgentResponse { Message = "Para problemas con la app..." });

            var ctx = builder.Build();
            var result = await _handler.HandleAsync(ctx, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(EstadoConversacion.ReintegroExit, sesion.Estado);
        }

        [Fact]
        public async Task Handle_OpcionDetallarProblema_VaAOtrasConsultasProblem()
        {
            var sesion = new SesionConversacion { Estado = EstadoConversacion.OtrasConsultasMenu, Nombre = "Juan" };
            var builder = new TestContextBuilder()
                .ConTexto("3")
                .ConSesion(sesion);
            var ctx = builder.Build();

            var result = await _handler.HandleAsync(ctx, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(EstadoConversacion.OtrasConsultasProblem, sesion.Estado);
        }

        [Fact]
        public async Task Handle_TextoInvalido_RepiteMenu()
        {
            var sesion = new SesionConversacion { Estado = EstadoConversacion.OtrasConsultasMenu, Nombre = "Juan" };
            var builder = new TestContextBuilder()
                .ConTexto("algo invalido")
                .ConSesion(sesion);
            var ctx = builder.Build();

            var result = await _handler.HandleAsync(ctx, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(EstadoConversacion.OtrasConsultasMenu, sesion.Estado);
        }
    }

    public class OtrasConsultasProblemHandlerTests
    {
        private readonly OtrasConsultasProblemHandler _handler = new();

        [Fact]
        public void CanHandle_OtrasConsultasProblem_True()
        {
            Assert.True(_handler.CanHandle(EstadoConversacion.OtrasConsultasProblem));
        }

        [Fact]
        public async Task Handle_OpcionCerrar_CambiaAEnded()
        {
            var sesion = new SesionConversacion { Estado = EstadoConversacion.OtrasConsultasProblem, Nombre = "Juan" };
            var builder = new TestContextBuilder()
                .ConTexto("2")
                .ConSesion(sesion);
            var ctx = builder.Build();

            var result = await _handler.HandleAsync(ctx, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(EstadoConversacion.Ended, sesion.Estado);
        }

        [Fact]
        public async Task Handle_OpcionVolver_VaAOtrasConsultasMenu()
        {
            var sesion = new SesionConversacion { Estado = EstadoConversacion.OtrasConsultasProblem, Nombre = "Juan" };
            var builder = new TestContextBuilder()
                .ConTexto("1")
                .ConSesion(sesion);
            var ctx = builder.Build();

            var result = await _handler.HandleAsync(ctx, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(EstadoConversacion.OtrasConsultasMenu, sesion.Estado);
        }

        [Fact]
        public async Task Handle_ProblemaGenerico_LlamaIAYVaAExit()
        {
            var sesion = new SesionConversacion { Estado = EstadoConversacion.OtrasConsultasProblem, Nombre = "Juan" };
            var builder = new TestContextBuilder()
                .ConTexto("tengo un problema con mi cuenta");

            builder.MockIa
                .Setup(ia => ia.EnviarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IaRequestContext?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AgentResponse { Message = "Para resolver ese problema..." });

            var ctx = builder.ConSesion(sesion).Build();
            var result = await _handler.HandleAsync(ctx, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(EstadoConversacion.ReintegroExit, sesion.Estado);
        }
    }
}
