namespace ServicioReintegros.AssistCard.Aplicacion.Puertos
{
    public interface IProveedorTraduccion
    {
        Task<string> DetectarAsync(string texto, CancellationToken ct = default);
        Task<string> TraducirAsync(string texto, string destino, CancellationToken ct = default);
    }
}
