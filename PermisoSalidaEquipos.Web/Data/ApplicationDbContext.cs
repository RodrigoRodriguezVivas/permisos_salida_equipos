using Microsoft.EntityFrameworkCore;
using PermisoSalidaEquipos.Web.Models;

namespace PermisoSalidaEquipos.Web.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Rol> Roles => Set<Rol>();
        public DbSet<Usuario> Usuarios => Set<Usuario>();
        public DbSet<Solicitud> Solicitudes => Set<Solicitud>();
        public DbSet<HistorialSolicitud> HistorialSolicitudes => Set<HistorialSolicitud>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Rol>(entity =>
            {
                entity.ToTable("Roles");
                entity.Property(r => r.Nombre).HasMaxLength(50).IsRequired();
                entity.HasIndex(r => r.Nombre).IsUnique();
            });

            modelBuilder.Entity<Usuario>(entity =>
            {
                entity.ToTable("Usuarios");
                entity.Property(u => u.NombreUsuarioDominio).HasMaxLength(256).IsRequired();
                entity.HasIndex(u => u.NombreUsuarioDominio).IsUnique();
                entity.Property(u => u.NombreCompleto).HasMaxLength(200).IsRequired();
                entity.Property(u => u.Correo).HasMaxLength(256);
                entity.Property(u => u.Cedula).HasMaxLength(30);
                entity.Property(u => u.Cargo).HasMaxLength(150);

                entity.HasOne(u => u.Rol)
                    .WithMany(r => r.Usuarios)
                    .HasForeignKey(u => u.RolId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(u => u.JefeInmediato)
                    .WithMany(u => u.Subordinados)
                    .HasForeignKey(u => u.JefeInmediatoId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Solicitud>(entity =>
            {
                entity.ToTable("Solicitudes");
                entity.Property(s => s.CedulaSolicitante).HasMaxLength(30).IsRequired();
                entity.Property(s => s.CargoSolicitante).HasMaxLength(150).IsRequired();
                entity.Property(s => s.TipoEquipo).HasMaxLength(100).IsRequired();
                entity.Property(s => s.Marca).HasMaxLength(100).IsRequired();
                entity.Property(s => s.Modelo).HasMaxLength(100).IsRequired();
                entity.Property(s => s.NumeroSerie).HasMaxLength(100).IsRequired();
                entity.Property(s => s.Accesorios).HasMaxLength(500);
                entity.Property(s => s.Motivo).HasMaxLength(100).IsRequired();
                entity.Property(s => s.MotivoDetalle).HasMaxLength(500);
                entity.Property(s => s.Observaciones).HasMaxLength(500);
                entity.Property(s => s.ComentarioJefe).HasMaxLength(500);
                entity.Property(s => s.ComentarioDirectorTI).HasMaxLength(500);
                entity.Property(s => s.Estado).HasConversion<int>();

                entity.HasOne(s => s.Solicitante)
                    .WithMany(u => u.SolicitudesCreadas)
                    .HasForeignKey(s => s.SolicitanteId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(s => s.JefeInmediatoAsignado)
                    .WithMany()
                    .HasForeignKey(s => s.JefeInmediatoId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(s => s.DirectorTIRevisor)
                    .WithMany()
                    .HasForeignKey(s => s.DirectorTIRevisorId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<HistorialSolicitud>(entity =>
            {
                entity.ToTable("HistorialSolicitudes");
                entity.Property(h => h.Estado).HasConversion<int>();
                entity.Property(h => h.Comentario).HasMaxLength(500);

                entity.HasOne(h => h.Solicitud)
                    .WithMany(s => s.Historial)
                    .HasForeignKey(h => h.SolicitudId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(h => h.Usuario)
                    .WithMany()
                    .HasForeignKey(h => h.UsuarioId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
