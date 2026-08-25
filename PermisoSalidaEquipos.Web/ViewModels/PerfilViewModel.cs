using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PermisoSalidaEquipos.Web.ViewModels
{
    public class PerfilViewModel
    {
        public int UsuarioId { get; set; }

        public string NombreUsuarioDominio { get; set; } = string.Empty;

        [Required(ErrorMessage = "El nombre completo es obligatorio.")]
        [Display(Name = "Nombre completo")]
        public string NombreCompleto { get; set; } = string.Empty;

        [Required(ErrorMessage = "El correo es obligatorio.")]
        [EmailAddress(ErrorMessage = "Ingresa un correo válido.")]
        [Display(Name = "Correo corporativo")]
        public string Correo { get; set; } = string.Empty;

        [Required(ErrorMessage = "La cédula es obligatoria.")]
        [Display(Name = "Cédula")]
        public string Cedula { get; set; } = string.Empty;

        [Required(ErrorMessage = "El cargo es obligatorio.")]
        [Display(Name = "Cargo")]
        public string Cargo { get; set; } = string.Empty;

        [Display(Name = "Jefe inmediato")]
        public int? JefeInmediatoId { get; set; }

        public bool EsDirectorTI { get; set; }

        public List<UsuarioOpcionViewModel> JefesDisponibles { get; set; } = new();
    }

    public class UsuarioOpcionViewModel
    {
        public int Id { get; set; }
        public string NombreCompleto { get; set; } = string.Empty;
        public string? Cargo { get; set; }
    }
}
