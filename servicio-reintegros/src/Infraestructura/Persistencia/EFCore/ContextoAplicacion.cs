using Microsoft.EntityFrameworkCore;
using ServicioReintegros.AssistCard.Dominio.Entidades;

namespace ServicioReintegros.AssistCard.Infraestructura.Persistencia.EFCore
{
    public sealed class ContextoAplicacion : DbContext
    {
        public ContextoAplicacion(DbContextOptions<ContextoAplicacion> options) : base(options) { }

        public DbSet<SesionUsuario> SesionesUsuario => Set<SesionUsuario>();
        public DbSet<CacheReintegros> CacheReintegros => Set<CacheReintegros>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SesionUsuario>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Telefono).HasMaxLength(32).IsRequired();
                e.Property(x => x.Idioma).HasMaxLength(8).IsRequired();
                e.Property(x => x.Estado).HasMaxLength(64);
                e.Property(x => x.UltimaIntencion).HasMaxLength(64);
                e.Property(x => x.ContextoJson).HasColumnType("nvarchar(max)");
                e.Property(x => x.ActualizadoEn).HasColumnType("datetime2");
                e.HasIndex(x => x.Telefono);
            });

            modelBuilder.Entity<CacheReintegros>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Clave).HasMaxLength(128).IsRequired();
                e.Property(x => x.ValorJson).HasColumnType("nvarchar(max)");
                e.Property(x => x.ExpiraEn).HasColumnType("datetime2");
                e.HasIndex(x => x.Clave).IsUnique();
            });
        }
    }
}
