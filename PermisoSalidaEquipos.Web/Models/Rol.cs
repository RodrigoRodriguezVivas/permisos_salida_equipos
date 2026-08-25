using System.Collections.Generic;

namespace PermisoSalidaEquipos.Web.Models
{
    /// <summary>
    /// Rol de aplicación (no confundir con roles/grupos de Windows). Se siembra con
    /// tres filas fijas: Usuario, JefeInmediato y DirectorTI (ver Data/DbInitializer.cs
    /// y RoleNames para las constantes correspondientes).
    /// </summary>
    public class Rol
    {
        public int Id { get; set; }

        public string Nombre { get; set; } = string.Empty;

        public ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
    }
}
