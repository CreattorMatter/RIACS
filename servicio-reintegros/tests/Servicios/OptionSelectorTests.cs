using System.Collections.Generic;
using ServicioReintegros.AssistCard.Aplicacion.Servicios;
using Xunit;

namespace ServicioReintegros.Tests.Servicios
{
    public class OptionSelectorTests
    {
        private static readonly List<string> Opciones = new()
        {
            "Consultar reintegro",
            "Otras consultas",
            "Volver al menu anterior"
        };

        [Theory]
        [InlineData("1", "Consultar reintegro")]
        [InlineData("2", "Otras consultas")]
        [InlineData("3", "Volver al menu anterior")]
        public void ElegirOpcion_PorNumero_DevuelveOpcionCorrecta(string input, string expected)
        {
            var result = OptionSelector.ElegirOpcion(input, Opciones);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("consultar reintegro", "Consultar reintegro")]
        [InlineData("OTRAS CONSULTAS", "Otras consultas")]
        public void ElegirOpcion_PorTextoNormalizado_DevuelveOpcionCorrecta(string input, string expected)
        {
            var result = OptionSelector.ElegirOpcion(input, Opciones);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("0")]
        [InlineData("4")]
        [InlineData("99")]
        [InlineData("algo random")]
        [InlineData("")]
        [InlineData(null)]
        public void ElegirOpcion_InputInvalido_DevuelveNull(string? input)
        {
            var result = OptionSelector.ElegirOpcion(input!, Opciones);
            Assert.Null(result);
        }

        [Fact]
        public void ElegirOpcion_ListaVacia_DevuelveNull()
        {
            Assert.Null(OptionSelector.ElegirOpcion("1", new List<string>()));
        }

        [Fact]
        public void ElegirOpcion_ListaNull_DevuelveNull()
        {
            Assert.Null(OptionSelector.ElegirOpcion("1", null!));
        }
    }
}
