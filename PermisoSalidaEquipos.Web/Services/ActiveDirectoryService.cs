using System;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace PermisoSalidaEquipos.Web.Services
{
    /// <summary>
    /// Implementación real de <see cref="IActiveDirectoryService"/> usando
    /// System.DirectoryServices.AccountManagement (LDAP) contra el Active Directory
    /// de Aligraf. Solo tiene efecto en el despliegue real (ModoDemo=false): en modo
    /// demo, o si la consulta falla por cualquier motivo (LDAP no disponible,
    /// credenciales insuficientes, el equipo donde corre esto no está unido al
    /// dominio, etc.), devuelve null sin lanzar excepción, de modo que el inicio de
    /// sesión y "Completar perfil" siguen funcionando exactamente igual que antes
    /// (la persona diligencia sus datos a mano).
    ///
    /// Configuración (sección "ActiveDirectory" de appsettings.json):
    ///   - Dominio: opcional. Si se deja vacío, se usa el dominio del equipo donde
    ///     corre IIS (caso normal cuando el servidor está unido al dominio de
    ///     Aligraf).
    ///   - Usuario / Clave: opcionales. Si se dejan vacíos, la consulta se hace con
    ///     la identidad del Application Pool de IIS (caso normal cuando esa cuenta ya
    ///     tiene permiso de lectura sobre el directorio, que es lo más común).
    ///     Solo hace falta diligenciarlos si el equipo de infraestructura de Aligraf
    ///     indica que se necesita una cuenta de servicio dedicada para consultar AD.
    /// </summary>
    public class ActiveDirectoryService : IActiveDirectoryService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<ActiveDirectoryService> _logger;

        public ActiveDirectoryService(IConfiguration configuration, ILogger<ActiveDirectoryService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public Task<DatosActiveDirectory?> ObtenerDatosAsync(string nombreUsuarioDominio)
        {
            if (_configuration.GetValue<bool>("ModoDemo"))
            {
                // Sitio de demostración pública: no hay Active Directory real que consultar.
                return Task.FromResult<DatosActiveDirectory?>(null);
            }

            if (string.IsNullOrWhiteSpace(nombreUsuarioDominio))
            {
                return Task.FromResult<DatosActiveDirectory?>(null);
            }

            try
            {
                var samAccountName = ExtraerSamAccountName(nombreUsuarioDominio);
                var dominio = _configuration["ActiveDirectory:Dominio"];
                var usuarioServicio = _configuration["ActiveDirectory:Usuario"];
                var claveServicio = _configuration["ActiveDirectory:Clave"];

                using var contexto = CrearContexto(dominio, usuarioServicio, claveServicio);
                using var usuarioAd = UserPrincipal.FindByIdentity(contexto, IdentityType.SamAccountName, samAccountName);

                if (usuarioAd == null)
                {
                    _logger.LogWarning("No se encontró en Active Directory al usuario '{Usuario}'.", nombreUsuarioDominio);
                    return Task.FromResult<DatosActiveDirectory?>(null);
                }

                var datos = new DatosActiveDirectory
                {
                    NombreCompleto = CadenaONulo(usuarioAd.DisplayName),
                    Correo = CadenaONulo(usuarioAd.EmailAddress),
                    Cargo = CadenaONulo(LeerCargo(usuarioAd))
                };

                return Task.FromResult<DatosActiveDirectory?>(datos);
            }
            catch (Exception ex)
            {
                // Nunca debe interrumpir el inicio de sesión ni "Completar perfil": si
                // Active Directory no está disponible (por ejemplo en desarrollo, o una
                // falla temporal de red/permisos), la persona simplemente diligencia
                // sus datos a mano, como antes de tener esta integración.
                _logger.LogWarning(ex, "No se pudo consultar Active Directory para '{Usuario}'.", nombreUsuarioDominio);
                return Task.FromResult<DatosActiveDirectory?>(null);
            }
        }

        private static PrincipalContext CrearContexto(string? dominio, string? usuarioServicio, string? claveServicio)
        {
            if (!string.IsNullOrWhiteSpace(usuarioServicio))
            {
                return new PrincipalContext(ContextType.Domain, string.IsNullOrWhiteSpace(dominio) ? null : dominio, usuarioServicio, claveServicio);
            }

            return string.IsNullOrWhiteSpace(dominio)
                ? new PrincipalContext(ContextType.Domain)
                : new PrincipalContext(ContextType.Domain, dominio);
        }

        /// <summary>
        /// El cargo (puesto) no está expuesto directamente por UserPrincipal: hay que
        /// leerlo del atributo LDAP "title" a través del DirectoryEntry subyacente.
        /// </summary>
        private string? LeerCargo(UserPrincipal usuarioAd)
        {
            try
            {
                if (usuarioAd.GetUnderlyingObject() is DirectoryEntry entry && entry.Properties.Contains("title"))
                {
                    return entry.Properties["title"].Value?.ToString();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo leer el atributo 'title' de Active Directory para '{Usuario}'.", usuarioAd.SamAccountName);
            }

            return null;
        }

        private static string? CadenaONulo(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

        private static string ExtraerSamAccountName(string nombreUsuarioDominio)
        {
            var partes = nombreUsuarioDominio.Split('\\', StringSplitOptions.RemoveEmptyEntries);
            return partes.Length > 1 ? partes[1] : partes[0];
        }
    }
}
