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
    /// <summary>
    /// Administración de usuarios: asignar Rol (Usuario / JefeInmediato / DirectorTI /
    /// GuardaSeguridad) y el jefe inmediato de cada persona. Exclusivo del Director de TI.
    /// </summary>
    [Authorize(Policy = PolicyNames.RequiereDirectorTI)]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IActiveDirectoryService _activeDirectoryService;

        public AdminController(ApplicationDbContext db, IActiveDirectoryService activeDirectoryService)
        {
            _db = db;
            _activeDirectoryService = activeDirectoryService;
        }

        public async Task<IActionResult> Usuarios(string? busqueda)
        {
            var modelo = new UsuariosAdminViewModel
            {
                Usuarios = await ObtenerUsuariosAsync(),
                Busqueda = busqueda
            };

            if (!string.IsNullOrWhiteSpace(busqueda))
            {
                var resultadoBusqueda = await _activeDirectoryService.BuscarUsuariosAsync(busqueda);
                if (resultadoBusqueda.Candidatos == null)
                {
                    modelo.ActiveDirectoryDisponible = false;
                    modelo.ActiveDirectoryError = resultadoBusqueda.Error;
                }
                else
                {
                    var yaRegistrados = new System.Collections.Generic.HashSet<string>(
                        modelo.Usuarios.Select(u => u.NombreUsuarioDominio),
                        System.StringComparer.OrdinalIgnoreCase);

                    modelo.ResultadosAD = resultadoBusqueda.Candidatos
                        .Where(c => !yaRegistrados.Contains(c.NombreUsuarioDominio))
                        .ToList();
                }
            }

            return View(modelo);
        }

        /// <summary>
        /// Crea el registro de un usuario a partir de una cuenta de Active Directory
        /// encontrada en la búsqueda, sin esperar a que esa persona inicie sesión.
        /// Queda con el rol "Usuario" por defecto; de inmediato se redirige a
        /// "Editar" para que el Director de TI le asigne el rol y el jefe inmediato
        /// reales.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AgregarDesdeAD(string nombreUsuarioDominio, string? busqueda)
        {
            if (string.IsNullOrWhiteSpace(nombreUsuarioDominio))
            {
                return RedirectToAction(nameof(Usuarios), new { busqueda });
            }

            var existente = await _db.Usuarios.FirstOrDefaultAsync(u => u.NombreUsuarioDominio == nombreUsuarioDominio);
            if (existente != null)
            {
                TempData["Mensaje"] = $"{existente.NombreCompleto} ya estaba en el sistema.";
                return RedirectToAction(nameof(Editar), new { id = existente.Id });
            }

            var datosAd = await _activeDirectoryService.ObtenerDatosAsync(nombreUsuarioDominio);
            var rolUsuario = await _db.Roles.FirstAsync(r => r.Nombre == RoleNames.Usuario);

            var usuario = new Models.Usuario
            {
                NombreUsuarioDominio = nombreUsuarioDominio,
                NombreCompleto = datosAd?.NombreCompleto ?? nombreUsuarioDominio,
                Correo = datosAd?.Correo ?? string.Empty,
                Cargo = datosAd?.Cargo,
                RolId = rolUsuario.Id,
                Activo = true,
                FechaCreacion = System.DateTime.Now
            };

            _db.Usuarios.Add(usuario);
            await _db.SaveChangesAsync();

            TempData["Mensaje"] = $"Se agregó a {usuario.NombreCompleto} desde Active Directory. Asígnale su rol y jefe inmediato.";
            return RedirectToAction(nameof(Editar), new { id = usuario.Id });
        }

        private async Task<System.Collections.Generic.List<UsuarioAdminListItemViewModel>> ObtenerUsuariosAsync()
        {
            return await _db.Usuarios
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
                Correo = usuario.Correo,
                Cedula = usuario.Cedula,
                Cargo = usuario.Cargo,
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
                modelo.RolesDisponibles = await ObtenerRolesAsync();
                modelo.JefesDisponibles = await ObtenerJefesDisponiblesAsync(usuario.Id);
                return View(modelo);
            }

            usuario.NombreCompleto = modelo.NombreCompleto;
            usuario.Correo = modelo.Correo ?? string.Empty;
            usuario.Cedula = modelo.Cedula;
            usuario.Cargo = modelo.Cargo;
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
