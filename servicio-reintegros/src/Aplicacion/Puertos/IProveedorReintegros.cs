using System.IO;
using ServicioReintegros.AssistCard.Aplicacion.Dtos;

namespace ServicioReintegros.AssistCard.Aplicacion.Puertos
{
    public interface IProveedorReintegros
    {
        Task<IEnumerable<ResumenReintegro>> BuscarPorIdentificadorAsync(ConsultaIdentificador consulta, CancellationToken ct = default);
        Task AgregarDocumentosAsync(int benefitRequestId, IEnumerable<(int DocumentId, string Nombre, string ContentType, Stream Contenido)> archivos, CancellationToken ct = default);
        Task ActualizarDatosBancariosAsync(int benefitRequestId, object datosBancarios, CancellationToken ct = default);
    }
}
