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
    }
}
