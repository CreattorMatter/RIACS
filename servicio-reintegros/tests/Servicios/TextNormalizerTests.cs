using ServicioReintegros.AssistCard.Aplicacion.Servicios;
using Xunit;

namespace ServicioReintegros.Tests.Servicios
{
    public class TextNormalizerTests
    {
        [Theory]
        [InlineData("Hola Mundo", "hola mundo")]
        [InlineData("  CAFÉ  ", "cafe")]
        [InlineData("José María", "jose maria")]
        [InlineData("múltiples   espacios", "multiples espacios")]
        public void Normalizar_QuitaAcentosYMinuscula(string input, string expected)
        {
            Assert.Equal(expected, TextNormalizer.Normalizar(input));
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("   ")]
        public void Normalizar_InputVacio_DevuelveEmpty(string? input)
        {
            Assert.Equal(string.Empty, TextNormalizer.Normalizar(input!));
        }

        [Theory]
        [InlineData("12345", true)]
        [InlineData("", true)]
        [InlineData(null, true)]
        [InlineData("abc", false)]
        [InlineData("hola mundo", false)]
        public void EsTextoAmbiguoParaDeteccion(string? input, bool expected)
        {
            Assert.Equal(expected, TextNormalizer.EsTextoAmbiguoParaDeteccion(input!));
        }

        [Theory]
        [InlineData("es", "es-ES")]
        [InlineData("en", "en-US")]
        [InlineData("pt", "pt-BR")]
        [InlineData("", "es-ES")]
        [InlineData(null, "es-ES")]
        public void MapearIdioma_DevuelveLocaleCorrecto(string? input, string expected)
        {
            Assert.Equal(expected, TextNormalizer.MapearIdioma(input!));
        }
    }
}
