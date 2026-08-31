using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PermisoSalidaEquipos.Web.Authorization;
using PermisoSalidaEquipos.Web.Data;
using PermisoSalidaEquipos.Web.Models;

namespace PermisoSalidaEquipos.Web.Services
{
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ApplicationDbContext _db;
        private readonly IActiveDirectoryService _activeDirectoryService;
        private Usuario? _cache;
        private bool _resuelto;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor, ApplicationDbContext db, IActiveDirectoryService activeDirectoryService)
        {
            _httpContextAccessor = httpContextAccessor;
            _db = db;
            _activeDirectoryService = activeDirectoryService;
        }

        public async Task<Usuario?> ObtenerUsuarioActualAsync()
        {
            // Se cachea por instancia (el servicio es Scoped, es decir, por petición HTTP)
            // para no consultar la base de datos más de una vez por request.
            if (_resuelto)
            {
                return _cache;
            }

            _resuelto = true;

            var identityName = _httpContextAccessor.HttpContext?.User?.Identity?.Name;
            if (string.IsNullOrWhiteSpace(identityName))
            {
                return null;
            }

            var usuario = await _db.Usuarios
                .Include(u => u.Rol)
                .Include(u => u.JefeInmediato)
                .FirstOrDefaultAsync(u => u.NombreUsuarioDominio == identityName);

            if (usuario == null)
            {
                var rolUsuario = await _db.Roles.FirstAsync(r => r.Nombre == RoleNames.Usuario);

                // Se intenta traer nombre completo, correo y cargo reales desde Active
                // Directory para no obligar a la persona a digitarlos a mano. Si AD no
                // está disponible (modo demo, entorno de desarrollo, falla de red/
                // permisos) datosAd queda en null y se usa el valor provisional de
                // siempre; la persona corrige lo que falte en "Completar perfil".
                var datosAd = await _activeDirectoryService.ObtenerDatosAsync(identityName);

                usuario = new Usuario
                {
                    NombreUsuarioDominio = identityName,
                    NombreCompleto = datosAd?.NombreCompleto ?? ExtraerNombreParaMostrar(identityName),
                    Correo = datosAd?.Correo ?? string.Empty,
                    Cargo = datosAd?.Cargo,
                    RolId = rolUsuario.Id,
                    Activo = true,
                    FechaCreacion = DateTime.Now
                };

                _db.Usuarios.Add(usuario);
                await _db.SaveChangesAsync();

                usuario.Rol = rolUsuario;
            }

            _cache = usuario;
            return usuario;
        }

        private static string ExtraerNombreParaMostrar(string identityName)
        {
            var partes = identityName.Split('\\', StringSplitOptions.RemoveEmptyEntries);
            var sinDominio = partes.Length > 1 ? partes[1] : partes[0];
            return sinDominio.Replace('.', ' ');
        }
    }
}
