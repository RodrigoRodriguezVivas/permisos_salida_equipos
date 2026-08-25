using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PermisoSalidaEquipos.Web.Authorization;
using PermisoSalidaEquipos.Web.Models;

namespace PermisoSalidaEquipos.Web.Data
{
    /// <summary>
    /// Siembra los tres roles fijos de la aplicación y, opcionalmente, el primer
    /// usuario con rol Director de TI (tomado de appsettings, sección
    /// "AdministradorInicial") para que alguien pueda entrar a asignar los demás roles
    /// la primera vez que se despliega la aplicación.
    ///
    /// La creación del esquema NO ocurre aquí: en producción se debe ejecutar el
    /// script database/01_CreateDatabase.sql contra el SQL Server de Aligraf. Este
    /// método solo crea el esquema automáticamente (EnsureCreated) cuando
    /// crearEsquemaAutomaticamente es true, pensado para ambientes de desarrollo sin
    /// acceso al DBA.
    /// </summary>
    public static class DbInitializer
    {
        public static async Task InicializarAsync(ApplicationDbContext db, string? usuarioDominioAdministradorInicial, bool crearEsquemaAutomaticamente)
        {
            if (crearEsquemaAutomaticamente)
            {
                await db.Database.EnsureCreatedAsync();
            }

            if (!db.Roles.Any())
            {
                db.Roles.AddRange(
                    new Rol { Nombre = RoleNames.Usuario },
                    new Rol { Nombre = RoleNames.JefeInmediato },
                    new Rol { Nombre = RoleNames.DirectorTI });
                await db.SaveChangesAsync();
            }

            if (!string.IsNullOrWhiteSpace(usuarioDominioAdministradorInicial) && !db.Usuarios.Any())
            {
                var rolDirectorTI = db.Roles.Single(r => r.Nombre == RoleNames.DirectorTI);
                db.Usuarios.Add(new Usuario
                {
                    NombreUsuarioDominio = usuarioDominioAdministradorInicial.Trim(),
                    NombreCompleto = usuarioDominioAdministradorInicial.Trim(),
                    Correo = string.Empty,
                    RolId = rolDirectorTI.Id,
                    Activo = true,
                    FechaCreacion = DateTime.Now
                });
                await db.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Datos de ejemplo para el sitio de demostración pública (ModoDemo=true):
        /// un usuario por cada rol, con una relación jefe/subordinado coherente, y
        /// varias solicitudes en distintos estados del flujo para que se pueda
        /// mostrar de inmediato cómo se ve cada paso sin tener que crear datos a
        /// mano. Es idempotente: si ya hay solicitudes de ejemplo, no hace nada
        /// (así una reinstalación o un reinicio del contenedor no duplica datos).
        /// </summary>
        public static async Task SembrarDatosDemoAsync(ApplicationDbContext db)
        {
            if (await db.Solicitudes.AnyAsync())
            {
                return;
            }

            var rolUsuario = await db.Roles.SingleAsync(r => r.Nombre == RoleNames.Usuario);
            var rolJefe = await db.Roles.SingleAsync(r => r.Nombre == RoleNames.JefeInmediato);
            var rolDirector = await db.Roles.SingleAsync(r => r.Nombre == RoleNames.DirectorTI);

            var director = await ObtenerOCrearUsuarioDemoAsync(db, "demo.director", "Rodrigo Rodriguez Vivas", "rodrigo.demo@alianzagrafica.com",
                "1000111222", "Director de TI", rolDirector.Id, jefeInmediatoId: null);

            var jefe = await ObtenerOCrearUsuarioDemoAsync(db, "demo.jefe", "Juan Gabriel Silva", "juan.demo@alianzagrafica.com",
                "1000333444", "Jefe de Producción", rolJefe.Id, jefeInmediatoId: director.Id);

            var usuario1 = await ObtenerOCrearUsuarioDemoAsync(db, "demo.usuario", "Laura Gómez", "laura.demo@alianzagrafica.com",
                "1000555666", "Diseñadora Gráfica", rolUsuario.Id, jefeInmediatoId: jefe.Id);

            var usuario2 = await ObtenerOCrearUsuarioDemoAsync(db, "demo.usuario2", "Julián Rojas", "julian.demo@alianzagrafica.com",
                "1000777888", "Auxiliar de Preprensa", rolUsuario.Id, jefeInmediatoId: jefe.Id);

            await db.SaveChangesAsync();

            var ahora = DateTime.Now;

            // 1) Pendiente de aprobación del jefe inmediato (para probar ese paso).
            await CrearSolicitudDemoAsync(db, usuario1, jefe.Id, EstadoSolicitud.PendienteJefe, ahora.AddHours(-2),
                "Portátil", "Dell", "Latitude 5440", "DEMO-SN-001", "Cargador, mouse",
                "Préstamo / trabajo remoto", "Trabajo desde casa por dos días", ahora.AddDays(1), ahora.AddDays(3));

            // 2) Ya aprobada por el jefe, pendiente de aprobación final del Director de TI.
            var solicitud2 = await CrearSolicitudDemoAsync(db, usuario2, jefe.Id, EstadoSolicitud.PendienteDirectorTI, ahora.AddDays(-1),
                "Monitor", "LG", "24MK430H", "DEMO-SN-002", "Cable HDMI",
                "Reparación externa", "Pantalla con línea vertical, se lleva a garantía", ahora.AddDays(-1).AddHours(3), ahora.AddDays(5));
            solicitud2.FechaDecisionJefe = ahora.AddHours(-20);
            solicitud2.ComentarioJefe = "Aprobado, es para garantía.";
            await AgregarHistorialAsync(db, solicitud2, EstadoSolicitud.PendienteDirectorTI, jefe.Id, ahora.AddHours(-20), "Aprobado, es para garantía.");

            // 3) Aprobada completamente (para ver el historial completo del flujo).
            var solicitud3 = await CrearSolicitudDemoAsync(db, usuario1, jefe.Id, EstadoSolicitud.Aprobada, ahora.AddDays(-5),
                "Proyector", "Epson", "PowerLite X39", "DEMO-SN-003", "Control remoto, cable de poder",
                "Evento externo", "Presentación con un cliente fuera de la planta", ahora.AddDays(-4), ahora.AddDays(-3));
            solicitud3.FechaDecisionJefe = ahora.AddDays(-5).AddHours(4);
            solicitud3.ComentarioJefe = "Aprobado.";
            solicitud3.DirectorTIRevisorId = director.Id;
            solicitud3.FechaDecisionDirectorTI = ahora.AddDays(-5).AddHours(6);
            solicitud3.ComentarioDirectorTI = "Aprobado, buen viaje.";
            await AgregarHistorialAsync(db, solicitud3, EstadoSolicitud.PendienteDirectorTI, jefe.Id, ahora.AddDays(-5).AddHours(4), "Aprobado.");
            await AgregarHistorialAsync(db, solicitud3, EstadoSolicitud.Aprobada, director.Id, ahora.AddDays(-5).AddHours(6), "Aprobado, buen viaje.");

            // 4) Rechazada por el jefe inmediato (para ver ese caso también).
            var solicitud4 = await CrearSolicitudDemoAsync(db, usuario2, jefe.Id, EstadoSolicitud.RechazadaJefe, ahora.AddDays(-3),
                "Tablet", "Samsung", "Galaxy Tab A9", "DEMO-SN-004", "-",
                "Otro", "Uso personal fuera de horario", ahora.AddDays(-3).AddHours(1), null);
            solicitud4.FechaDecisionJefe = ahora.AddDays(-3).AddHours(2);
            solicitud4.ComentarioJefe = "No aplica para uso personal.";
            await AgregarHistorialAsync(db, solicitud4, EstadoSolicitud.RechazadaJefe, jefe.Id, ahora.AddDays(-3).AddHours(2), "No aplica para uso personal.");

            await db.SaveChangesAsync();
        }

        private static async Task<Usuario> ObtenerOCrearUsuarioDemoAsync(ApplicationDbContext db, string nombreUsuarioDominio, string nombreCompleto,
            string correo, string cedula, string cargo, int rolId, int? jefeInmediatoId)
        {
            var existente = await db.Usuarios.FirstOrDefaultAsync(u => u.NombreUsuarioDominio == nombreUsuarioDominio);
            if (existente != null)
            {
                return existente;
            }

            var usuario = new Usuario
            {
                NombreUsuarioDominio = nombreUsuarioDominio,
                NombreCompleto = nombreCompleto,
                Correo = correo,
                Cedula = cedula,
                Cargo = cargo,
                RolId = rolId,
                JefeInmediatoId = jefeInmediatoId,
                Activo = true,
                FechaCreacion = DateTime.Now
            };
            db.Usuarios.Add(usuario);
            await db.SaveChangesAsync();
            return usuario;
        }

        private static async Task<Solicitud> CrearSolicitudDemoAsync(ApplicationDbContext db, Usuario solicitante, int jefeInmediatoId,
            EstadoSolicitud estado, DateTime fechaCreacion, string tipoEquipo, string marca, string modelo, string numeroSerie,
            string accesorios, string motivo, string motivoDetalle, DateTime fechaSalida, DateTime? fechaRetorno)
        {
            var solicitud = new Solicitud
            {
                SolicitanteId = solicitante.Id,
                CedulaSolicitante = solicitante.Cedula!,
                CargoSolicitante = solicitante.Cargo!,
                TipoEquipo = tipoEquipo,
                Marca = marca,
                Modelo = modelo,
                NumeroSerie = numeroSerie,
                Accesorios = accesorios,
                Motivo = motivo,
                MotivoDetalle = motivoDetalle,
                FechaSalida = fechaSalida,
                FechaRetornoEstimada = fechaRetorno,
                FechaCreacion = fechaCreacion,
                JefeInmediatoId = jefeInmediatoId,
                Estado = estado
            };
            db.Solicitudes.Add(solicitud);
            await db.SaveChangesAsync();

            await AgregarHistorialAsync(db, solicitud, EstadoSolicitud.PendienteJefe, solicitante.Id, fechaCreacion, "Solicitud creada.");

            return solicitud;
        }

        private static async Task AgregarHistorialAsync(ApplicationDbContext db, Solicitud solicitud, EstadoSolicitud estado, int usuarioId, DateTime fecha, string? comentario)
        {
            db.HistorialSolicitudes.Add(new HistorialSolicitud
            {
                SolicitudId = solicitud.Id,
                Estado = estado,
                UsuarioId = usuarioId,
                Fecha = fecha,
                Comentario = comentario
            });
            await db.SaveChangesAsync();
        }
    }
}
