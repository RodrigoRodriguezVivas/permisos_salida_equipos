using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PermisoSalidaEquipos.Web.Authorization;
using PermisoSalidaEquipos.Web.Data;
using PermisoSalidaEquipos.Web.Services;
using PermisoSalidaEquipos.Web.ViewModels;

namespace PermisoSalidaEquipos.Web.Controllers
{
    [Authorize]
    public class PerfilController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ICurrentUserService _currentUserService;

        public PerfilController(ApplicationDbContext db, ICurrentUserService currentUserService)
        {
            _db = db;
            _currentUserService = currentUserService;
        }

        public async Task<IActionResult> Completar()
        {
            var usuario = await _currentUserService.ObtenerUsuarioActualAsync();
            if (usuario == null)
            {
                return Challenge();
            }

            // Si ya está completo, no hay nada que hacer aquí: al inicio.
            if (usuario.PerfilCompleto())
            {
                return RedirectToAction("Index", "Home");
            }

            var modelo = new PerfilViewModel
            {
                UsuarioId = usuario.Id,
                NombreUsuarioDominio = usuario.NombreUsuarioDominio,
                NombreCompleto = usuario.NombreCompleto,
                Correo = usuario.Correo,
                Cedula = usuario.Cedula ?? string.Empty,
                Cargo = usuario.Cargo ?? string.Empty,
                JefeInmediatoId = usuario.JefeInmediatoId,
                ExentoDeJefeInmediato = RoleNames.ExentoDeJefeInmediato(usuario.Rol?.Nombre),
                JefesDisponibles = await ObtenerJefesDisponiblesAsync(usuario.Id)
            };

            return View(modelo);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Completar(PerfilViewModel modelo)
        {
            var usuario = await _currentUserService.ObtenerUsuarioActualAsync();
            if (usuario == null)
            {
                return Challenge();
            }

            var exentoDeJefe = RoleNames.ExentoDeJefeInmediato(usuario.Rol?.Nombre);
            if (!exentoDeJefe && modelo.JefeInmediatoId == null)
            {
                ModelState.AddModelError(nameof(modelo.JefeInmediatoId), "Selecciona tu jefe inmediato.");
            }

            if (!ModelState.IsValid)
            {
                modelo.UsuarioId = usuario.Id;
                modelo.NombreUsuarioDominio = usuario.NombreUsuarioDominio;
                modelo.ExentoDeJefeInmediato = exentoDeJefe;
                modelo.JefesDisponibles = await ObtenerJefesDisponiblesAsync(usuario.Id);
                return View(modelo);
            }

            usuario.NombreCompleto = modelo.NombreCompleto.Trim();
            usuario.Correo = modelo.Correo.Trim();
            usuario.Cedula = modelo.Cedula.Trim();
            usuario.Cargo = modelo.Cargo.Trim();
            usuario.JefeInmediatoId = exentoDeJefe ? null : modelo.JefeInmediatoId;

            await _db.SaveChangesAsync();

            TempData["Mensaje"] = "Tu perfil se guardó correctamente.";
            return RedirectToAction("Index", "Home");
        }

        private async Task<System.Collections.Generic.List<UsuarioOpcionViewModel>> ObtenerJefesDisponiblesAsync(int usuarioActualId)
        {
            return await _db.Usuarios
                .Where(u => u.Activo && u.Id != usuarioActualId)
                .OrderBy(u => u.NombreCompleto)
                .Select(u => new UsuarioOpcionViewModel { Id = u.Id, NombreCompleto = u.NombreCompleto, Cargo = u.Cargo })
                .ToListAsync();
        }
    }
}
