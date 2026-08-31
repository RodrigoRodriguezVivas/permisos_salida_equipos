using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using PermisoSalidaEquipos.Web.Services;

namespace PermisoSalidaEquipos.Web.ViewModels
{
    public class UsuarioAdminListItemViewModel
    {
        public int Id { get; set; }
        public string NombreUsuarioDominio { get; set; } = string.Empty;
        public string NombreCompleto { get; set; } = string.Empty;
        public string? Cargo { get; set; }
        public string RolNombre { get; set; } = string.Empty;
        public string? JefeInmediatoNombre { get; set; }
        public bool Activo { get; set; }
    }

    /// <summary>
    /// Modelo de la pantalla "Usuarios y roles": la lista de quienes ya usaron la
    /// aplicación, más — cuando se escribió una búsqueda — las cuentas de Active
    /// Directory que coinciden y todavía no están en el sistema, para poder
    /// agregarlas y asignarles rol sin esperar a que esa persona inicie sesión.
    /// </summary>
    public class UsuariosAdminViewModel
    {
        public List<UsuarioAdminListItemViewModel> Usuarios { get; set; } = new();

        [Display(Name = "Buscar en Active Directory")]
        public string? Busqueda { get; set; }

        /// <summary>
        /// Candidatos de AD que coinciden con la búsqueda y aún no están en
        /// "Usuarios". Null si no se ha buscado todavía; también viene null (junto
        /// con <see cref="ActiveDirectoryDisponible"/> en false) si la búsqueda no
        /// se pudo hacer porque Active Directory no está disponible.
        /// </summary>
        public List<CandidatoActiveDirectory>? ResultadosAD { get; set; }

        /// <summary>
        /// false cuando se intentó buscar pero Active Directory no respondió (modo
        /// demo, sin conexión al dominio, etc.), para mostrar un mensaje distinto de
        /// "no hay resultados".
        /// </summary>
        public bool ActiveDirectoryDisponible { get; set; } = true;
    }

    public class EditarUsuarioAdminViewModel
    {
        public int Id { get; set; }
        public string NombreUsuarioDominio { get; set; } = string.Empty;
        public string NombreCompleto { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Rol")]
        public int RolId { get; set; }

        [Display(Name = "Jefe inmediato")]
        public int? JefeInmediatoId { get; set; }

        [Display(Name = "Usuario activo")]
        public bool Activo { get; set; }

        public List<UsuarioOpcionViewModel> RolesDisponibles { get; set; } = new();
        public List<UsuarioOpcionViewModel> JefesDisponibles { get; set; } = new();
    }

    public class ReporteFiltroViewModel
    {
        [Display(Name = "Desde")]
        [DataType(DataType.Date)]
        public System.DateTime? Desde { get; set; }

        [Display(Name = "Hasta")]
        [DataType(DataType.Date)]
        public System.DateTime? Hasta { get; set; }

        [Display(Name = "Estado")]
        public string? Estado { get; set; }

        [Display(Name = "Solicitante")]
        public string? Solicitante { get; set; }

        public List<SolicitudListItemViewModel> Resultados { get; set; } = new();
    }
}
