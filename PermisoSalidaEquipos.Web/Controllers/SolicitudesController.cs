using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PermisoSalidaEquipos.Web.Authorization;
using PermisoSalidaEquipos.Web.Data;
using PermisoSalidaEquipos.Web.Models;
using PermisoSalidaEquipos.Web.Services;
using PermisoSalidaEquipos.Web.ViewModels;

namespace PermisoSalidaEquipos.Web.Controllers
{
    [Authorize]
    public class SolicitudesController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ICurrentUserService _currentUserService;
        private readonly NotificacionService _notificaciones;

        public SolicitudesController(ApplicationDbContext db, ICurrentUserService currentUserService, NotificacionService notificaciones)
        {
            _db = db;
            _currentUserService = currentUserService;
            _notificaciones = notificaciones;
        }

        public async Task<IActionResult> Mias()
        {
            var usuario = await _currentUserService.ObtenerUsuarioActualAsync();
            if (usuario == null) return Challenge();

            var solicitudes = await _db.Solicitudes
                .Where(s => s.SolicitanteId == usuario.Id)
                .OrderByDescending(s => s.FechaCreacion)
                .Select(s => new SolicitudListItemViewModel
                {
                    Id = s.Id,
                    SolicitanteNombre = s.Solicitante!.NombreCompleto,
                    TipoEquipo = s.TipoEquipo,
                    Marca = s.Marca,
                    Modelo = s.Modelo,
                    FechaSalida = s.FechaSalida,
                    FechaCreacion = s.FechaCreacion,
                    Estado = s.Estado
                })
                .ToListAsync();

            return View(solicitudes);
        }

        public IActionResult Crear()
        {
            return View(new CrearSolicitudViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear(CrearSolicitudViewModel input)
        {
            // El parámetro se llama "input" (y no "modelo") a propósito: el modelo
            // tiene una propiedad "Modelo" (marca/modelo del equipo) y, si el parámetro
            // de la acción se llamara igual, el model binder de ASP.NET Core puede
            // confundirse (advertencia MVC1004) e intentar leer "modelo.Modelo" del
            // formulario en vez de "Modelo", rompiendo el binding.
            var usuario = await _currentUserService.ObtenerUsuarioActualAsync();
            if (usuario == null) return Challenge();

            if (!usuario.JefeInmediatoId.HasValue)
            {
                // No debería ocurrir: PerfilCompletoFilter ya exige jefe inmediato salvo
                // para el Director de TI, que en ese caso radica directamente al Director.
                ModelState.AddModelError(string.Empty, "No tienes un jefe inmediato asignado. Contacta al Director de TI.");
            }

            if (input.FechaRetornoEstimada.HasValue && input.FechaRetornoEstimada.Value.Date < input.FechaSalida.Date)
            {
                ModelState.AddModelError(nameof(input.FechaRetornoEstimada), "La fecha de retorno no puede ser anterior a la fecha de salida.");
            }

            if (!ModelState.IsValid)
            {
                return View(input);
            }

            var esDirectorTI = usuario.Rol?.Nombre == RoleNames.DirectorTI;

            var solicitud = new Solicitud
            {
                SolicitanteId = usuario.Id,
                CedulaSolicitante = usuario.Cedula!,
                CargoSolicitante = usuario.Cargo!,
                TipoEquipo = input.TipoEquipo,
                Marca = input.Marca,
                Modelo = input.Modelo,
                NumeroSerie = input.NumeroSerie,
                Accesorios = input.Accesorios,
                Motivo = input.Motivo,
                MotivoDetalle = input.MotivoDetalle,
                FechaSalida = input.FechaSalida,
                FechaRetornoEstimada = input.FechaRetornoEstimada,
                Observaciones = input.Observaciones,
                FechaCreacion = DateTime.Now,
                // El Director de TI no tiene jefe inmediato: su propia solicitud entra
                // directamente a la etapa de aprobación del Director de TI.
                JefeInmediatoId = esDirectorTI ? usuario.Id : usuario.JefeInmediatoId!.Value,
                Estado = esDirectorTI ? EstadoSolicitud.PendienteDirectorTI : EstadoSolicitud.PendienteJefe
            };

            _db.Solicitudes.Add(solicitud);
            await _db.SaveChangesAsync();

            _db.HistorialSolicitudes.Add(new HistorialSolicitud
            {
                SolicitudId = solicitud.Id,
                Estado = solicitud.Estado,
                UsuarioId = usuario.Id,
                Fecha = DateTime.Now,
                Comentario = "Solicitud creada."
            });
            await _db.SaveChangesAsync();

            solicitud.Solicitante = usuario;

            if (esDirectorTI)
            {
                await _notificaciones.NotificarPendienteDirectorTIAsync(solicitud, usuario);
            }
            else
            {
                var jefe = await _db.Usuarios.FindAsync(usuario.JefeInmediatoId!.Value);
                if (jefe != null)
                {
                    await _notificaciones.NotificarNuevaSolicitudAsync(solicitud, jefe);
                }
            }

            TempData["Mensaje"] = $"Tu solicitud #{solicitud.Id} fue creada y enviada para aprobación.";
            return RedirectToAction(nameof(Detalle), new { id = solicitud.Id });
        }

        public async Task<IActionResult> Detalle(int id)
        {
            var usuario = await _currentUserService.ObtenerUsuarioActualAsync();
            if (usuario == null) return Challenge();

            var solicitud = await _db.Solicitudes
                .Include(s => s.Solicitante)
                .Include(s => s.JefeInmediatoAsignado)
                .Include(s => s.DirectorTIRevisor)
                .Include(s => s.Historial).ThenInclude(h => h.Usuario)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (solicitud == null) return NotFound();

            var esRolJefe = usuario.Rol?.Nombre == RoleNames.JefeInmediato || usuario.Rol?.Nombre == RoleNames.DirectorTI;
            var esDirector = usuario.Rol?.Nombre == RoleNames.DirectorTI;

            var esDueno = solicitud.SolicitanteId == usuario.Id;
            var esJefeAsignado = solicitud.JefeInmediatoId == usuario.Id;

            // Solo puede ver la solicitud: el dueño, su jefe asignado, o cualquier
            // Director de TI (para poder hacer seguimiento y reportes).
            if (!esDueno && !esJefeAsignado && !esDirector)
            {
                return Forbid();
            }

            var modelo = new SolicitudDetalleViewModel
            {
                Solicitud = solicitud,
                PuedeDecidirComoJefe = esJefeAsignado && esRolJefe && solicitud.Estado == EstadoSolicitud.PendienteJefe,
                PuedeDecidirComoDirector = esDirector && solicitud.Estado == EstadoSolicitud.PendienteDirectorTI,
                PuedeCancelar = esDueno && (solicitud.Estado == EstadoSolicitud.PendienteJefe || solicitud.Estado == EstadoSolicitud.PendienteDirectorTI)
            };

            return View(modelo);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancelar(int id)
        {
            var usuario = await _currentUserService.ObtenerUsuarioActualAsync();
            if (usuario == null) return Challenge();

            var solicitud = await _db.Solicitudes.Include(s => s.Solicitante).FirstOrDefaultAsync(s => s.Id == id);
            if (solicitud == null) return NotFound();

            if (solicitud.SolicitanteId != usuario.Id ||
                (solicitud.Estado != EstadoSolicitud.PendienteJefe && solicitud.Estado != EstadoSolicitud.PendienteDirectorTI))
            {
                return Forbid();
            }

            solicitud.Estado = EstadoSolicitud.CanceladaPorSolicitante;
            _db.HistorialSolicitudes.Add(new HistorialSolicitud
            {
                SolicitudId = solicitud.Id,
                Estado = solicitud.Estado,
                UsuarioId = usuario.Id,
                Fecha = DateTime.Now,
                Comentario = "Solicitud cancelada por el solicitante."
            });
            await _db.SaveChangesAsync();

            TempData["Mensaje"] = $"La solicitud #{solicitud.Id} fue cancelada.";
            return RedirectToAction(nameof(Detalle), new { id });
        }
    }
}
