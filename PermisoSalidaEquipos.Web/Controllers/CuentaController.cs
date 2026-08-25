using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PermisoSalidaEquipos.Web.Data;

namespace PermisoSalidaEquipos.Web.Controllers
{
    /// <summary>
    /// Login SOLO para el sitio de demostración pública (ModoDemo=true). En el
    /// despliegue real de Aligraf este controlador no se usa: el ingreso es con
    /// Windows Authentication integrada (ver Program.cs). Aquí, en cambio, se elige
    /// uno de los usuarios de ejemplo sembrados por DbInitializer.SembrarDatosDemoAsync,
    /// simulando el inicio de sesión sin pedir contraseña.
    /// </summary>
    [AllowAnonymous]
    public class CuentaController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IConfiguration _configuration;

        public CuentaController(ApplicationDbContext db, IConfiguration configuration)
        {
            _db = db;
            _configuration = configuration;
        }

        private bool ModoDemo => _configuration.GetValue<bool>("ModoDemo");

        public async Task<IActionResult> Ingresar()
        {
            if (!ModoDemo)
            {
                return NotFound();
            }

            var usuariosDemo = await _db.Usuarios
                .Include(u => u.Rol)
                .OrderBy(u => u.RolId)
                .ThenBy(u => u.NombreCompleto)
                .ToListAsync();

            return View(usuariosDemo);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> IngresarComo(int usuarioId)
        {
            if (!ModoDemo)
            {
                return NotFound();
            }

            var usuario = await _db.Usuarios.FindAsync(usuarioId);
            if (usuario == null)
            {
                return NotFound();
            }

            var claims = new List<Claim> { new Claim(ClaimTypes.Name, usuario.NombreUsuarioDominio) };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Salir()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Ingresar));
        }
    }
}
