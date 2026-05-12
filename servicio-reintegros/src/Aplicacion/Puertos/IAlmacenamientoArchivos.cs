namespace ServicioReintegros.AssistCard.Aplicacion.Puertos
{
    public interface IAlmacenamientoArchivos
    {
        Task GuardarObjetoAsync(string contenedor, string clave, Stream contenido, string contentType, CancellationToken ct = default);
        Task<Stream> ObtenerObjetoAsync(string contenedor, string clave, CancellationToken ct = default);
    }
}
