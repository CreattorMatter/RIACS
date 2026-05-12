using ServicioReintegros.AssistCard.Aplicacion.Servicios;
using Xunit;

namespace ServicioReintegros.Tests.Servicios
{
    public class GuardrailServiceTests
    {
        [Fact]
        public void SanitizarReferenciasSoporte_ReemplazaFraseGenericaConEmail_Es()
        {
            var input = "Puedes contactar a nuestro soporte para más info.";
            var result = GuardrailService.SanitizarReferenciasSoporte(input, "es");
            Assert.Contains("reintegros@assistcard.com", result);
            Assert.DoesNotContain("contactar a nuestro soporte", result);
        }

        [Fact]
        public void SanitizarReferenciasSoporte_CorrigeEmailCruzado_Pt()
        {
            var input = "Envíe un email a reintegros@assistcard.com";
            var result = GuardrailService.SanitizarReferenciasSoporte(input, "pt");
            Assert.Contains("reembolso@assistcard.com", result);
        }

        [Fact]
        public void SanitizarReferenciasSoporte_CorrigeEmailCruzado_Es()
        {
            var input = "Envíe un email a reembolso@assistcard.com";
            var result = GuardrailService.SanitizarReferenciasSoporte(input, "es");
            Assert.Contains("reintegros@assistcard.com", result);
        }

        [Theory]
        [InlineData("soporte tecnico disponible", true)]
        [InlineData("por este medio no podemos", true)]
        [InlineData("texto normal sin problemas", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void ContieneFraseProhibida_DetectaCorrectamente(string? input, bool expected)
        {
            Assert.Equal(expected, GuardrailService.ContieneFraseProhibida(input!));
        }

        [Fact]
        public void AsegurarEmailEnRespuesta_NoAgregaSiYaExiste()
        {
            var input = "Contacte a reintegros@assistcard.com para ayuda.";
            var result = GuardrailService.AsegurarEmailEnRespuesta(input, "es");
            Assert.Equal(input, result);
        }

        [Fact]
        public void AsegurarEmailEnRespuesta_AgregaSiNoExiste()
        {
            var input = "El estado de su reintegro es aprobado.";
            var result = GuardrailService.AsegurarEmailEnRespuesta(input, "es");
            Assert.Contains("reintegros@assistcard.com", result);
        }

        [Theory]
        [InlineData("Vamos a acelerar su trámite", true)]
        [InlineData("We will accelerate the process", true)]
        [InlineData("Estamos procesando su solicitud", false)]
        public void ContienePromesaAceleracion_DetectaCorrectamente(string input, bool expected)
        {
            Assert.Equal(expected, GuardrailService.ContienePromesaAceleracion(input));
        }

        [Fact]
        public void AplicarGuardrails_TextoVacio_DevuelveFallback()
        {
            var result = GuardrailService.AplicarGuardrails("", "es", "fallback msg");
            Assert.True(result.UsedFallback);
            Assert.Equal("fallback msg", result.Mensaje);
            Assert.Equal("empty_ia_response", result.Reason);
        }

        [Fact]
        public void AplicarGuardrails_ConFraseProhibida_DevuelveFallback()
        {
            var result = GuardrailService.AplicarGuardrails("soporte tecnico disponible", "es", "fallback");
            Assert.True(result.UsedFallback);
            Assert.Equal("fallback", result.Mensaje);
        }

        [Fact]
        public void AplicarGuardrails_ConPromesaAceleracion_PolicyNoAceleracion_DevuelveFallback()
        {
            var result = GuardrailService.AplicarGuardrails(
                "Vamos a acelerar el pago", "es", "fallback", GuardrailPolicy.NoAceleracion);
            Assert.True(result.UsedFallback);
        }

        [Fact]
        public void AplicarGuardrails_TextoLimpio_NoUsaFallback()
        {
            var result = GuardrailService.AplicarGuardrails(
                "Su reintegro fue aprobado por USD 100.", "es", "fallback");
            Assert.False(result.UsedFallback);
            Assert.Contains("reintegros@assistcard.com", result.Mensaje);
        }
    }
}
