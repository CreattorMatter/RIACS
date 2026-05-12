using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ServicioReintegros.AssistCard.Aplicacion.Puertos
{
    public interface IProveedorWhatsApp
    {
        Task EnviarTextoAsync(string destino, string mensaje, CancellationToken ct = default);
        Task EnviarPlantillaAsync(string destino, string plantillaId, string locale, IEnumerable<string> parametros, CancellationToken ct = default);
        Task<Stream> DescargarMediaAsync(string mediaId, CancellationToken ct = default);
        void VerificarFirma(Microsoft.AspNetCore.Http.IHeaderDictionary headers, Stream bodyStream);
    }
}
