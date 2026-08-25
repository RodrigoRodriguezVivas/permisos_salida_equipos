using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PermisoSalidaEquipos.Web.Authorization;
using PermisoSalidaEquipos.Web.Data;
using PermisoSalidaEquipos.Web.ViewModels;

namespace PermisoSalidaEquipos.Web.Controllers
{
    /// <summary>
    /// Administración de usuarios: asignar Rol (Usuario / JefeInmediato / DirectorTI /
    /// GuardaSeguridad) y el jefe inmediato de cada persona. Exclusivo del Director de TI.
    /// </summary>
    [Authorize(Policy = PolicyNames.RequiereDirectorTI)]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _db;

        public AdminController(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Usuarios()
        {
            var usuarios = await _db.Usuarios
                .Include(u => u.Rol)
                .Include(u => u.JefeInmediato)
                .OrderBy(u => u.NombreCompleto)
                .Select(u => new UsuarioAdminListItemViewModel
                {
                    Id = u.Id,
                    NombreUsuarioDominio = u.NombreUsuarioDominio,
                    NombreCompleto = u.NombreCompleto,
                    Cargo = u.Cargo,
                    RolNombre = u.Rol!.Nombre,
                    JefeInmediatoNombre = u.JefeInmediato != null ? u.JefeInmediato.NombreCompleto : null,
                    Activo = u.Activo
                })
                .ToListAsync();

            return View(usuarios);
        }

        public async Task<IActionResult> Editar(int id)
        {
            var usuario = await _db.Usuarios.FindAsync(id);
            if (usuario == null) return NotFound();

            var modelo = new EditarUsuarioAdminViewModel
            {
                Id = usuario.Id,
                NombreUsuarioDominio = usuario.NombreUsuarioDominio,
                NombreCompleto = usuario.NombreCompleto,
                RolId = usuario.RolId,
                JefeInmediatoId = usuario.JefeInmediatoId,
                Activo = usuario.Activo,
                RolesDisponibles = await ObtenerRolesAsync(),
                JefesDisponibles = await ObtenerJefesDisponiblesAsync(usuario.Id)
            };

            return View(modelo);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(EditarUsuarioAdminViewModel modelo)
        {
            var usuario = await _db.Usuarios.FindAsync(modelo.Id);
            if (usuario == null) return NotFound();

            if (modelo.JefeInmediatoId == usuario.Id)
            {
                ModelState.AddModelError(nameof(modelo.JefeInmediatoId), "Un usuario no puede ser su propio jefe inmediato.");
            }

            if (!ModelState.IsValid)
            {
                modelo.NombreUsuarioDominio = usuario.NombreUsuarioDominio;
                modelo.NombreCompleto = usuario.NombreCompleto;
                modelo.RolesDisponibles = await ObtenerRolesAsync();
                modelo.JefesDisponibles = await ObtenerJefesDisponiblesAsync(usuario.Id);
                return View(modelo);
            }

            usuario.RolId = modelo.RolId;
            usuario.JefeInmediatoId = modelo.JefeInmediatoId;
            usuario.Activo = modelo.Activo;

            await _db.SaveChangesAsync();

            TempData["Mensaje"] = $"Se actualizó el usuario {usuario.NombreCompleto}.";
            return RedirectToAction(nameof(Usuarios));
        }

        private async Task<System.Collections.Generic.List<UsuarioOpcionViewModel>> ObtenerRolesAsync()
        {
            return await _db.Roles
                .OrderBy(r => r.Id)
                .Select(r => new UsuarioOpcionViewModel { Id = r.Id, NombreCompleto = r.Nombre })
                .ToListAsync();
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
