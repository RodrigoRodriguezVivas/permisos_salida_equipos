using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Filters;
using PermisoSalidaEquipos.Web.Services;

namespace PermisoSalidaEquipos.Web.Authorization
{
    /// <summary>
    /// Filtro global: si el usuario autenticado todavía no completó su perfil
    /// (cédula, cargo y jefe inmediato), lo redirige a Perfil/Completar antes de
    /// dejarlo usar cualquier otra pantalla. Se excluye el propio controlador Perfil
    /// para no generar un bucle de redirección.
    /// </summary>
    public class PerfilCompletoFilter : IAsyncActionFilter
    {
        private readonly ICurrentUserService _currentUserService;

        public PerfilCompletoFilter(ICurrentUserService currentUserService)
        {
            _currentUserService = currentUserService;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var nombreControlador = context.RouteData.Values["controller"]?.ToString();

            if (string.Equals(nombreControlador, "Perfil", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(nombreControlador, "Error", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(nombreControlador, "Cuenta", StringComparison.OrdinalIgnoreCase))
            {
                await next();
                return;
            }

            var usuario = await _currentUserService.ObtenerUsuarioActualAsync();
            if (usuario != null && !usuario.PerfilCompleto(RoleNames.DirectorTI))
            {
                context.Result = new Microsoft.AspNetCore.Mvc.RedirectToActionResult("Completar", "Perfil", null);
                return;
            }

            await next();
        }
    }
}
