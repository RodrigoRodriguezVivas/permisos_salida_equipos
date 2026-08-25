using System.Threading.Tasks;
using PermisoSalidaEquipos.Web.Models;

namespace PermisoSalidaEquipos.Web.Services
{
    /// <summary>
    /// Resuelve el Usuario de la aplicación correspondiente a la identidad de Windows
    /// de la petición actual, creando el registro automáticamente en el primer
    /// inicio de sesión.
    /// </summary>
    public interface ICurrentUserService
    {
        /// <summary>
        /// Obtiene (o crea, si es el primer ingreso) el Usuario correspondiente a la
        /// identidad de Windows autenticada por IIS. Devuelve null si no hay una
        /// identidad autenticada en la petición actual.
        /// </summary>
        Task<Usuario?> ObtenerUsuarioActualAsync();
    }
}
