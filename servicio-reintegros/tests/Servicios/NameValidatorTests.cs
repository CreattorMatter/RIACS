using ServicioReintegros.AssistCard.Aplicacion.Servicios;
using Xunit;

namespace ServicioReintegros.Tests.Servicios
{
    public class NameValidatorTests
    {
        [Theory]
        [InlineData("Juan Perez", true)]
        [InlineData("María García López", true)]
        [InlineData("Jean-Pierre", true)]
        [InlineData("O'Connor", true)]
        public void EsNombreProbable_NombresValidos_DevuelveTrue(string input, bool expected)
        {
            Assert.Equal(expected, NameValidator.EsNombreProbable(input));
        }

        [Theory]
        [InlineData("", false)]
        [InlineData(null, false)]
        [InlineData("   ", false)]
        [InlineData("12345", false)]
        [InlineData("abc123", false)]
        [InlineData("test@email.com", false)]
        [InlineData("hola", false)]
        [InlineData("menu", false)]
        [InlineData("reintegro consulta", false)]
        [InlineData("necesito ayuda", false)]
        [InlineData("AB", false)]
        public void EsNombreProbable_InputsInvalidos_DevuelveFalse(string? input, bool expected)
        {
            Assert.Equal(expected, NameValidator.EsNombreProbable(input!));
        }

        [Fact]
        public void EsNombreProbable_TextoMuyLargo_DevuelveFalse()
        {
            var largo = new string('a', 71);
            Assert.False(NameValidator.EsNombreProbable(largo));
        }

        [Fact]
        public void EsNombreProbable_MasDe5Partes_DevuelveFalse()
        {
            Assert.False(NameValidator.EsNombreProbable("Uno Dos Tres Cuatro Cinco Seis"));
        }
    }
}
