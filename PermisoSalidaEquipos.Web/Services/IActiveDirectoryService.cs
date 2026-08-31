using System.Collections.Generic;
using System.Threading.Tasks;

namespace PermisoSalidaEquipos.Web.Services
{
    /// <summary>
    /// Datos reales de un usuario tal como constan en Active Directory. Cualquier
    /// campo puede venir en null si Active Directory no lo tiene diligenciado; nunca
    /// se sobreescribe un dato existente en la aplicación con un valor vacío.
    /// </summary>
    public class DatosActiveDirectory
    {
        public string? NombreCompleto { get; set; }
        public string? Correo { get; set; }
        public string? Cargo { get; set; }
    }

    /// <summary>
    /// Una cuenta de Active Directory encontrada al buscar por nombre, para que el
    /// Director de TI la agregue a "Usuarios y roles" sin esperar a que esa persona
    /// inicie sesión por primera vez.
    /// </summary>
    public class CandidatoActiveDirectory
    {
        /// <summary>
        /// En formato DOMINIO\usuario, igual al que entrega la autenticación
        /// integrada de Windows — así el registro que se cree aquí coincide
        /// exactamente con el que se buscaría en el primer inicio de sesión real de
        /// esa persona, en vez de crear un duplicado.
        /// </summary>
        public string NombreUsuarioDominio { get; set; } = string.Empty;
        public string? NombreCompleto { get; set; }
        public string? Correo { get; set; }
        public string? Cargo { get; set; }
    }

    /// <summary>
    /// Resultado de una búsqueda en Active Directory: o bien la lista de candidatos
    /// (que puede venir vacía, si sencillamente no hubo coincidencias), o bien un
    /// <see cref="Error"/> con el detalle técnico de por qué no se pudo consultar el
    /// directorio — para poder mostrárselo al Director de TI en la pantalla de
    /// Administración y diagnosticar sin depender de revisar logs del servidor.
    /// </summary>
    public class ResultadoBusquedaActiveDirectory
    {
        /// <summary>Null si la búsqueda no se pudo realizar (ver <see cref="Error"/>).</summary>
        public List<CandidatoActiveDirectory>? Candidatos { get; set; }

        /// <summary>Detalle técnico del fallo; solo tiene valor cuando Candidatos es null.</summary>
        public string? Error { get; set; }
    }

    /// <summary>
    /// Consulta Active Directory (vía LDAP, con la identidad de Windows del usuario
    /// que ya autenticó IIS) para traer su nombre completo, correo y cargo reales,
    /// en vez de que la persona los digite a mano en "Completar perfil".
    /// </summary>
    public interface IActiveDirectoryService
    {
        /// <summary>
        /// Busca en Active Directory al usuario identificado por
        /// <paramref name="nombreUsuarioDominio"/> (formato DOMINIO\usuario o solo
        /// "usuario"). Devuelve null si no se encuentra, si la aplicación corre en
        /// modo demo, o si Active Directory no está disponible por cualquier motivo
        /// — nunca lanza una excepción que interrumpa el inicio de sesión.
        /// </summary>
        Task<DatosActiveDirectory?> ObtenerDatosAsync(string nombreUsuarioDominio);

        /// <summary>
        /// Busca cuentas habilitadas en Active Directory cuyo nombre o usuario de
        /// dominio contenga <paramref name="filtro"/> (usado en Administración >
        /// Usuarios y roles, para agregar a alguien antes de su primer ingreso).
        /// Devuelve como máximo 20 resultados.
        /// </summary>
        Task<ResultadoBusquedaActiveDirectory> BuscarUsuariosAsync(string filtro);
    }
}
