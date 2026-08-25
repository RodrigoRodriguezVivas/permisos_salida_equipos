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
        private Usuario? _cache;
        private bool _resuelto;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor, ApplicationDbContext db)
        {
            _httpContextAccessor = httpContextAccessor;
            _db = db;
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

                usuario = new Usuario
                {
                    NombreUsuarioDominio = identityName,
                    // Nombre para mostrar provisional: la parte después de la barra
                    // invertida del nombre de dominio. El usuario lo puede corregir en
                    // "Completar perfil".
                    NombreCompleto = ExtraerNombreParaMostrar(identityName),
                    Correo = string.Empty,
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
