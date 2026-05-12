using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using ServicioReintegros.AssistCard.Aplicacion.Puertos;

namespace ServicioReintegros.AssistCard.Infraestructura.Almacenamiento
{
    public sealed class AzureBlobAdapter : IAlmacenamientoArchivos
    {
        private readonly BlobServiceClient _blobService;
        private readonly StorageOpciones _cfg;

        public AzureBlobAdapter(IOptions<StorageOpciones> cfg)
        {
            _cfg = cfg.Value;
            _blobService = new BlobServiceClient(_cfg.ConnectionString);
        }

        public async Task GuardarObjetoAsync(string contenedor, string clave, Stream contenido, string contentType, CancellationToken ct = default)
        {
            contenedor = string.IsNullOrWhiteSpace(contenedor) ? _cfg.DefaultContainer : contenedor;
            var container = _blobService.GetBlobContainerClient(contenedor);
            await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);
            var blob = container.GetBlobClient(clave);
            await blob.UploadAsync(contenido, new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } }, ct);
        }

        public async Task<Stream> ObtenerObjetoAsync(string contenedor, string clave, CancellationToken ct = default)
        {
            contenedor = string.IsNullOrWhiteSpace(contenedor) ? _cfg.DefaultContainer : contenedor;
            var container = _blobService.GetBlobContainerClient(contenedor);
            var blob = container.GetBlobClient(clave);
            var resp = await blob.DownloadAsync(ct);
            return resp.Value.Content;
        }
    }

    public sealed class StorageOpciones
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string DefaultContainer { get; set; } = "wa-media";
    }
}
