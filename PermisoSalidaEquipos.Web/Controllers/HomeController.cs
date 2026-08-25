using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PermisoSalidaEquipos.Web.Authorization;
using PermisoSalidaEquipos.Web.Data;
using PermisoSalidaEquipos.Web.Models;
using PermisoSalidaEquipos.Web.Services;

namespace PermisoSalidaEquipos.Web.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ICurrentUserService _currentUserService;

        public HomeController(ApplicationDbContext db, ICurrentUserService currentUserService)
        {
            _db = db;
            _currentUserService = currentUserService;
        }

        public async Task<IActionResult> Index()
        {
            var usuario = await _currentUserService.ObtenerUsuarioActualAsync();
            if (usuario == null)
            {
                return Challenge();
            }

            var misSolicitudesPendientes = await _db.Solicitudes
                .CountAsync(s => s.SolicitanteId == usuario.Id &&
                    (s.Estado == EstadoSolicitud.PendienteJefe || s.Estado == EstadoSolicitud.PendienteDirectorTI));

            var misSolicitudesTotal = await _db.Solicitudes.CountAsync(s => s.SolicitanteId == usuario.Id);

            var pendientesComoJefe = 0;
            if (usuario.Rol?.Nombre == RoleNames.JefeInmediato || usuario.Rol?.Nombre == RoleNames.DirectorTI)
            {
                pendientesComoJefe = await _db.Solicitudes
                    .CountAsync(s => s.JefeInmediatoId == usuario.Id && s.Estado == EstadoSolicitud.PendienteJefe);
            }

            var pendientesComoDirector = 0;
            if (usuario.Rol?.Nombre == RoleNames.DirectorTI)
            {
                pendientesComoDirector = await _db.Solicitudes.CountAsync(s => s.Estado == EstadoSolicitud.PendienteDirectorTI);
            }

            var pendientesComoGuarda = 0;
            if (usuario.Rol?.Nombre == RoleNames.GuardaSeguridad || usuario.Rol?.Nombre == RoleNames.DirectorTI)
            {
                pendientesComoGuarda = await _db.Solicitudes.CountAsync(s => s.Estado == EstadoSolicitud.Aprobada);
            }

            ViewBag.Usuario = usuario;
            ViewBag.MisSolicitudesPendientes = misSolicitudesPendientes;
            ViewBag.MisSolicitudesTotal = misSolicitudesTotal;
            ViewBag.PendientesComoJefe = pendientesComoJefe;
            ViewBag.PendientesComoDirector = pendientesComoDirector;
            ViewBag.PendientesComoGuarda = pendientesComoGuarda;

            return View();
        }

        public IActionResult Error()
        {
            return View();
        }
    }
}
