using System;
using ServicioReintegros.AssistCard.Aplicacion.Dtos;
using ServicioReintegros.AssistCard.Aplicacion.Servicios;
using Xunit;

namespace ServicioReintegros.Tests.Servicios
{
    public class ReintegroStatusHelperTests
    {
        [Theory]
        [InlineData("Under Review", true)]
        [InlineData("Received", true)]
        [InlineData("en revision", true)]
        [InlineData("Approved", false)]
        [InlineData("Denied", false)]
        public void EstaEnRevision_DetectaCorrectamente(string status, bool expected)
        {
            var r = new ResumenReintegro { StatusOriginal = status };
            Assert.Equal(expected, ReintegroStatusHelper.EstaEnRevision(r));
        }

        [Fact]
        public void EstaEnRevision_Null_DevuelveFalse()
        {
            Assert.False(ReintegroStatusHelper.EstaEnRevision(null));
        }

        [Theory]
        [InlineData("Pending Payment", true)]
        [InlineData("pago pendiente", true)]
        [InlineData("pagamento pendente", true)]
        [InlineData("Approved", false)]
        public void EsEstadoPagoPendiente_DetectaCorrectamente(string status, bool expected)
        {
            var r = new ResumenReintegro { StatusOriginal = status };
            Assert.Equal(expected, ReintegroStatusHelper.EsEstadoPagoPendiente(r));
        }

        [Theory]
        [InlineData("12345", null, true)]
        [InlineData(null, "ABC1234", true)]
        [InlineData("12345", "ABC1234", true)]
        [InlineData(null, null, false)]
        [InlineData("", "", false)]
        public void EsResumenValido_ValidaCorrectamente(string? bid, string? cid, bool expected)
        {
            var r = new ResumenReintegro { BenefitId = bid, CaseId = cid };
            Assert.Equal(expected, ReintegroStatusHelper.EsResumenValido(r));
        }

        [Fact]
        public void EsResumenValido_Null_DevuelveFalse()
        {
            Assert.False(ReintegroStatusHelper.EsResumenValido(null));
        }

        [Fact]
        public void DentroPrimeros20Dias_RecienCreado_DevuelveTrue()
        {
            var r = new ResumenReintegro { CreateDate = DateTime.UtcNow.AddDays(-5) };
            Assert.True(ReintegroStatusHelper.DentroPrimeros20Dias(r));
        }

        [Fact]
        public void DentroPrimeros20Dias_MasDe20Dias_DevuelveFalse()
        {
            var r = new ResumenReintegro { CreateDate = DateTime.UtcNow.AddDays(-25) };
            Assert.False(ReintegroStatusHelper.DentroPrimeros20Dias(r));
        }

        [Fact]
        public void DiasDesdeCreacion_SinFecha_DevuelveNull()
        {
            var r = new ResumenReintegro { CreateDate = null };
            Assert.Null(ReintegroStatusHelper.DiasDesdeCreacion(r));
        }
    }
}
