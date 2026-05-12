using ServicioReintegros.AssistCard.Aplicacion.Servicios;
using Xunit;

namespace ServicioReintegros.Tests.Servicios
{
    public class IdentificadorParserTests
    {
        [Theory]
        [InlineData("73124")]
        [InlineData("12345678")]
        public void TryParse_BenefitRequestId_Valido(string input)
        {
            Assert.True(IdentificadorParser.TryParse(input, out var c));
            Assert.NotNull(c);
            Assert.NotNull(c!.BenefitRequestId);
        }

        [Theory]
        [InlineData("1234")]
        [InlineData("123456789")]
        [InlineData("0")]
        public void TryParse_NumeroFueraDeRango_DevuelveFalse(string input)
        {
            Assert.False(IdentificadorParser.TryParse(input, out _));
        }

        [Theory]
        [InlineData("test@mail.com")]
        [InlineData("user@assistcard.com")]
        public void TryParse_Email_Valido(string input)
        {
            Assert.True(IdentificadorParser.TryParse(input, out var c));
            Assert.NotNull(c);
            Assert.Equal(input, c!.Email);
        }

        [Theory]
        [InlineData("ABC1234")]
        [InlineData("XYZ9876")]
        public void TryParse_CaseId_7Alfanum_Valido(string input)
        {
            Assert.True(IdentificadorParser.TryParse(input, out var c));
            Assert.NotNull(c);
            Assert.NotNull(c!.CaseId);
        }

        [Fact]
        public void TryParse_CaseIdConGuiones_Sanitizado()
        {
            Assert.True(IdentificadorParser.TryParse("ABC-1234", out var c));
            Assert.NotNull(c);
            Assert.Equal("ABC1234", c!.CaseId);
        }

        [Theory]
        [InlineData("AR123456", "AR", "123456")]
        [InlineData("US1234567", "US", "1234567")]
        [InlineData("AR22546519", "AR", "22546519")]
        [InlineData("ARZZZ111010920192Z", "AR", "ZZZ111010920192Z")]
        public void TryParse_Voucher_Valido(string input, string expectedIso, string expectedCode)
        {
            Assert.True(IdentificadorParser.TryParse(input, out var c));
            Assert.NotNull(c);
            Assert.Equal(expectedIso, c!.CountryIsoCode);
            Assert.Equal(expectedCode, c!.VoucherCode);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("   ")]
        [InlineData("hola mundo como estas")]
        public void TryParse_InputInvalido_DevuelveFalse(string? input)
        {
            Assert.False(IdentificadorParser.TryParse(input!, out _));
        }
    }
}
