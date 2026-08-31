using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Linq;
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

        private const int MaximoResultadosBusqueda = 20;

        public Task<List<CandidatoActiveDirectory>?> BuscarUsuariosAsync(string filtro)
        {
            if (_configuration.GetValue<bool>("ModoDemo"))
            {
                // Sitio de demostración pública: no hay Active Directory real que consultar.
                return Task.FromResult<List<CandidatoActiveDirectory>?>(null);
            }

            filtro = (filtro ?? string.Empty).Trim();
            if (filtro.Length < 2)
            {
                // Evita consultas demasiado amplias (o costosas) sobre todo el directorio;
                // la pantalla le pide a la persona que escriba al menos dos caracteres.
                return Task.FromResult<List<CandidatoActiveDirectory>?>(new List<CandidatoActiveDirectory>());
            }

            try
            {
                var dominio = _configuration["ActiveDirectory:Dominio"];
                var usuarioServicio = _configuration["ActiveDirectory:Usuario"];
                var claveServicio = _configuration["ActiveDirectory:Clave"];
                var prefijoDominio = ResolverPrefijoDominio();

                using var contexto = CrearContexto(dominio, usuarioServicio, claveServicio);

                var encontrados = new Dictionary<string, CandidatoActiveDirectory>(StringComparer.OrdinalIgnoreCase);

                // Dos búsquedas por separado (nombre para mostrar y usuario de dominio):
                // Query By Example de UserPrincipal no hace "contiene" sobre varios
                // campos a la vez, así que se combinan los resultados de ambas,
                // evitando duplicados por SamAccountName.
                BuscarPorCampo(contexto, prefijoDominio, encontrados, filtro, porNombre: true);
                if (encontrados.Count < MaximoResultadosBusqueda)
                {
                    BuscarPorCampo(contexto, prefijoDominio, encontrados, filtro, porNombre: false);
                }

                var resultado = encontrados.Values
                    .OrderBy(c => c.NombreCompleto ?? c.NombreUsuarioDominio)
                    .Take(MaximoResultadosBusqueda)
                    .ToList();

                return Task.FromResult<List<CandidatoActiveDirectory>?>(resultado);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo buscar en Active Directory con el filtro '{Filtro}'.", filtro);
                return Task.FromResult<List<CandidatoActiveDirectory>?>(null);
            }
        }

        private void BuscarPorCampo(PrincipalContext contexto, string prefijoDominio, Dictionary<string, CandidatoActiveDirectory> encontrados, string filtro, bool porNombre)
        {
            using var qbeUsuario = new UserPrincipal(contexto) { Enabled = true };
            if (porNombre)
            {
                qbeUsuario.DisplayName = $"*{filtro}*";
            }
            else
            {
                qbeUsuario.SamAccountName = $"*{filtro}*";
            }

            using var buscador = new PrincipalSearcher(qbeUsuario);
            using var resultados = buscador.FindAll();

            foreach (var principal in resultados)
            {
                using (principal)
                {
                    if (principal is not UserPrincipal usuarioAd || string.IsNullOrWhiteSpace(usuarioAd.SamAccountName))
                    {
                        continue;
                    }

                    if (encontrados.Count >= MaximoResultadosBusqueda)
                    {
                        break;
                    }

                    var nombreUsuarioDominio = $"{prefijoDominio}\\{usuarioAd.SamAccountName}";
                    if (encontrados.ContainsKey(nombreUsuarioDominio))
                    {
                        continue;
                    }

                    encontrados[nombreUsuarioDominio] = new CandidatoActiveDirectory
                    {
                        NombreUsuarioDominio = nombreUsuarioDominio,
                        NombreCompleto = CadenaONulo(usuarioAd.DisplayName),
                        Correo = CadenaONulo(usuarioAd.EmailAddress),
                        Cargo = CadenaONulo(LeerCargo(usuarioAd))
                    };
                }
            }
        }

        /// <summary>
        /// El nombre de usuario de dominio se guarda en la aplicación en formato
        /// DOMINIO\usuario (el mismo que entrega la autenticación integrada de
        /// Windows), pero Active Directory identifica el dominio por su nombre DNS,
        /// no por el nombre NetBIOS que aparece antes de la barra invertida. Por eso
        /// hace falta resolverlo aparte para que el registro creado aquí coincida
        /// exactamente con el que se buscaría en el primer inicio de sesión real de
        /// esa persona (si no coinciden, se crearía un segundo registro duplicado en
        /// vez de reconocerla). Orden de resolución:
        ///   1. ActiveDirectory:PrefijoDominioNetBIOS en appsettings, si se diligenció.
        ///   2. El dominio de Windows del propio servidor (Environment.UserDomainName),
        ///      que es el valor correcto en el caso normal: IIS corriendo en un
        ///      servidor unido al dominio de Aligraf.
        /// </summary>
        private string ResolverPrefijoDominio()
        {
            var configurado = _configuration["ActiveDirectory:PrefijoDominioNetBIOS"];
            if (!string.IsNullOrWhiteSpace(configurado))
            {
                return configurado.Trim();
            }

            return Environment.UserDomainName;
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
