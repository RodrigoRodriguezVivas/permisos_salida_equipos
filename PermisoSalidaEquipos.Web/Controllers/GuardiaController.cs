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
    /// Pantalla de portería para el Guarda de Seguridad: consulta las solicitudes ya
    /// aprobadas que aún no han salido físicamente de la empresa y confirma la
    /// salida del equipo. El Director de TI también tiene acceso (ver
    /// RoleAuthorizationHandler) para poder hacer seguimiento.
    /// </summary>
    [Authorize(Policy = PolicyNames.RequiereGuardaSeguridad)]
    public class GuardiaController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ICurrentUserService _currentUserService;
        private readonly NotificacionService _notificaciones;

        public GuardiaController(ApplicationDbContext db, ICurrentUserService currentUserService, NotificacionService notificaciones)
        {
            _db = db;
            _currentUserService = currentUserService;
            _notificaciones = notificaciones;
        }

        public async Task<IActionResult> PendientesSalida()
        {
            var pendientes = await _db.Solicitudes
                .Where(s => s.Estado == EstadoSolicitud.Aprobada)
                .OrderBy(s => s.FechaSalida)
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
        public async Task<IActionResult> ConfirmarSalida(ConfirmarSalidaViewModel input)
        {
            var usuario = await _currentUserService.ObtenerUsuarioActualAsync();
            if (usuario == null) return Challenge();

            var solicitud = await _db.Solicitudes
                .Include(s => s.Solicitante)
                .FirstOrDefaultAsync(s => s.Id == input.SolicitudId);

            if (solicitud == null) return NotFound();

            if (solicitud.Estado != EstadoSolicitud.Aprobada)
            {
                return Forbid();
            }

            solicitud.Estado = EstadoSolicitud.SalioDeLaEmpresa;
            solicitud.RegistradaSalidaPorId = usuario.Id;
            solicitud.FechaSalidaRegistrada = DateTime.Now;
            solicitud.ComentarioGuarda = input.Comentario;

            _db.HistorialSolicitudes.Add(new HistorialSolicitud
            {
                SolicitudId = solicitud.Id,
                Estado = solicitud.Estado,
                UsuarioId = usuario.Id,
                Fecha = DateTime.Now,
                Comentario = input.Comentario ?? "Equipo verificado y autorizado para salir de la empresa."
            });

            await _db.SaveChangesAsync();

            await _notificaciones.NotificarDecisionAlSolicitanteAsync(solicitud, EstadoSolicitudTexto.Descripcion(solicitud.Estado), input.Comentario);

            TempData["Mensaje"] = $"Se registró la salida del equipo de la solicitud #{solicitud.Id}.";
            return RedirectToAction(nameof(PendientesSalida));
        }
    }
}
