using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using PermisoSalidaEquipos.Web.Services;

namespace PermisoSalidaEquipos.Web.Authorization
{
    /// <summary>
    /// Requisito de autorización basado en el Rol de aplicación guardado en la tabla
    /// Usuarios (no en los roles/grupos de Windows). Se resuelve consultando
    /// ICurrentUserService, que a su vez cachea el Usuario actual por petición.
    /// </summary>
    public class RoleRequirement : IAuthorizationRequirement
    {
        public string RolRequerido { get; }

        public RoleRequirement(string rolRequerido)
        {
            RolRequerido = rolRequerido;
        }
    }

    public class RoleAuthorizationHandler : AuthorizationHandler<RoleRequirement>
    {
        private readonly ICurrentUserService _currentUserService;

        public RoleAuthorizationHandler(ICurrentUserService currentUserService)
        {
            _currentUserService = currentUserService;
        }

        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, RoleRequirement requirement)
        {
            var usuario = await _currentUserService.ObtenerUsuarioActualAsync();

            // El Director de TI tiene acceso implícito a todo lo que requiera el rol
            // JefeInmediato o el rol Guarda de Seguridad, además de sus propias
            // pantallas exclusivas (es el rol de más alto nivel de la aplicación).
            if (usuario?.Rol != null &&
                (usuario.Rol.Nombre == requirement.RolRequerido ||
                 (requirement.RolRequerido == RoleNames.JefeInmediato && usuario.Rol.Nombre == RoleNames.DirectorTI) ||
                 (requirement.RolRequerido == RoleNames.GuardaSeguridad && usuario.Rol.Nombre == RoleNames.DirectorTI)))
            {
                context.Succeed(requirement);
            }
        }
    }
}
