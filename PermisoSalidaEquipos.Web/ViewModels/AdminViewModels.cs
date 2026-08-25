using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

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
