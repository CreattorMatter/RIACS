namespace ServicioReintegros.AssistCard.Aplicacion.Puertos
{
    public interface ICacheAplicacion
    {
        Task<string?> ObtenerAsync(string clave, CancellationToken ct = default);
        Task GuardarAsync(string clave, string valor, TimeSpan ttl, CancellationToken ct = default);
        Task<long> IncrementarAsync(string clave, CancellationToken ct = default);
        Task<TimeSpan?> TtlAsync(string clave, CancellationToken ct = default);
    }
}
