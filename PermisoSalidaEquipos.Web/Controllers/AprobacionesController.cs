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
    /// <summary>
    /// Bandejas de aprobación para Jefe Inmediato y Director de TI. El acceso a cada
    /// acción se restringe con las políticas registradas en Program.cs, que a su vez
    /// consultan el Rol de aplicación guardado en la tabla Usuarios.
    /// </summary>
    [Authorize]
    public class AprobacionesController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ICurrentUserService _currentUserService;
        private readonly NotificacionService _notificaciones;

        public AprobacionesController(ApplicationDbContext db, ICurrentUserService currentUserService, NotificacionService notificaciones)
        {
            _db = db;
            _currentUserService = currentUserService;
            _notificaciones = notificaciones;
        }

        [Authorize(Policy = PolicyNames.RequiereJefeInmediato)]
        public async Task<IActionResult> PendientesJefe()
        {
            var usuario = await _currentUserService.ObtenerUsuarioActualAsync();
            if (usuario == null) return Challenge();

            var pendientes = await _db.Solicitudes
                .Where(s => s.JefeInmediatoId == usuario.Id && s.Estado == EstadoSolicitud.PendienteJefe)
                .OrderBy(s => s.FechaCreacion)
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

            return View(pendientes);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = PolicyNames.RequiereJefeInmediato)]
        public async Task<IActionResult> DecidirJefe(DecisionSolicitudViewModel modelo)
        {
            var usuario = await _currentUserService.ObtenerUsuarioActualAsync();
            if (usuario == null) return Challenge();

            var solicitud = await _db.Solicitudes
                .Include(s => s.Solicitante)
                .FirstOrDefaultAsync(s => s.Id == modelo.SolicitudId);

            if (solicitud == null) return NotFound();

            if (solicitud.JefeInmediatoId != usuario.Id || solicitud.Estado != EstadoSolicitud.PendienteJefe)
            {
                return Forbid();
            }

            solicitud.ComentarioJefe = modelo.Comentario;
            solicitud.FechaDecisionJefe = DateTime.Now;
            solicitud.Estado = modelo.Aprobar ? EstadoSolicitud.PendienteDirectorTI : EstadoSolicitud.RechazadaJefe;

            _db.HistorialSolicitudes.Add(new HistorialSolicitud
            {
                SolicitudId = solicitud.Id,
                Estado = solicitud.Estado,
                UsuarioId = usuario.Id,
                Fecha = DateTime.Now,
                Comentario = modelo.Comentario
            });

            await _db.SaveChangesAsync();

            if (modelo.Aprobar)
            {
                var directoresTI = await _db.Usuarios
                    .Where(u => u.Activo && u.Rol!.Nombre == RoleNames.DirectorTI)
                    .ToListAsync();
                foreach (var director in directoresTI)
                {
                    await _notificaciones.NotificarPendienteDirectorTIAsync(solicitud, director);
                }
            }
            else
            {
                await _notificaciones.NotificarDecisionAlSolicitanteAsync(solicitud, EstadoSolicitudTexto.Descripcion(solicitud.Estado), modelo.Comentario);
            }

            TempData["Mensaje"] = modelo.Aprobar
                ? $"Aprobaste la solicitud #{solicitud.Id}. Se envió al Director de TI."
                : $"Rechazaste la solicitud #{solicitud.Id}.";

            return RedirectToAction(nameof(PendientesJefe));
        }

        [Authorize(Policy = PolicyNames.RequiereDirectorTI)]
        public async Task<IActionResult> PendientesDirector()
        {
            var pendientes = await _db.Solicitudes
                .Where(s => s.Estado == EstadoSolicitud.PendienteDirectorTI)
                .OrderBy(s => s.FechaCreacion)
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

            return View(pendientes);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = PolicyNames.RequiereDirectorTI)]
        public async Task<IActionResult> DecidirDirector(DecisionSolicitudViewModel modelo)
        {
            var usuario = await _currentUserService.ObtenerUsuarioActualAsync();
            if (usuario == null) return Challenge();

            var solicitud = await _db.Solicitudes
                .Include(s => s.Solicitante)
                .FirstOrDefaultAsync(s => s.Id == modelo.SolicitudId);

            if (solicitud == null) return NotFound();

            if (solicitud.Estado != EstadoSolicitud.PendienteDirectorTI)
            {
                return Forbid();
            }

            solicitud.ComentarioDirectorTI = modelo.Comentario;
            solicitud.FechaDecisionDirectorTI = DateTime.Now;
            solicitud.DirectorTIRevisorId = usuario.Id;
            solicitud.Estado = modelo.Aprobar ? EstadoSolicitud.Aprobada : EstadoSolicitud.RechazadaDirectorTI;

            _db.HistorialSolicitudes.Add(new HistorialSolicitud
            {
                SolicitudId = solicitud.Id,
                Estado = solicitud.Estado,
                UsuarioId = usuario.Id,
                Fecha = DateTime.Now,
                Comentario = modelo.Comentario
            });

            await _db.SaveChangesAsync();

            await _notificaciones.NotificarDecisionAlSolicitanteAsync(solicitud, EstadoSolicitudTexto.Descripcion(solicitud.Estado), modelo.Comentario);

            TempData["Mensaje"] = modelo.Aprobar
                ? $"Aprobaste definitivamente la solicitud #{solicitud.Id}."
                : $"Rechazaste la solicitud #{solicitud.Id}.";

            return RedirectToAction(nameof(PendientesDirector));
        }
    }
}
