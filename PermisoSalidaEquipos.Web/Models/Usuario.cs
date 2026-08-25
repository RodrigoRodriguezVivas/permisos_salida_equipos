using System;
using System.Collections.Generic;
using PermisoSalidaEquipos.Web.Authorization;

namespace PermisoSalidaEquipos.Web.Models
{
    /// <summary>
    /// Representa a un empleado de Alianza Gráfica que ha iniciado sesión en la
    /// aplicación con su cuenta de dominio. El primer inicio de sesión crea
    /// automáticamente el registro (ver CurrentUserService); el usuario debe luego
    /// completar cédula, cargo y jefe inmediato antes de poder radicar solicitudes.
    /// </summary>
    public class Usuario
    {
        public int Id { get; set; }

        /// <summary>
        /// Identidad de Windows tal como la entrega la autenticación integrada de IIS,
        /// normalmente en formato DOMINIO\usuario. Es la llave de correlación entre el
        /// inicio de sesión de Windows y el registro de la aplicación.
        /// </summary>
        public string NombreUsuarioDominio { get; set; } = string.Empty;

        public string NombreCompleto { get; set; } = string.Empty;

        public string Correo { get; set; } = string.Empty;

        public string? Cedula { get; set; }

        public string? Cargo { get; set; }

        public int RolId { get; set; }

        public Rol? Rol { get; set; }

        /// <summary>
        /// Jefe inmediato asignado por el Director de TI (o quien administre la
        /// aplicación) desde el módulo de administración. Es quien recibe las
        /// solicitudes de este usuario en primera instancia.
        /// </summary>
        public int? JefeInmediatoId { get; set; }

        public Usuario? JefeInmediato { get; set; }

        public ICollection<Usuario> Subordinados { get; set; } = new List<Usuario>();

        public bool Activo { get; set; } = true;

        public DateTime FechaCreacion { get; set; }

        public ICollection<Solicitud> SolicitudesCreadas { get; set; } = new List<Solicitud>();

        /// <summary>
        /// El perfil se considera completo cuando tiene cédula, cargo y (salvo que el
        /// rol esté exento, como Director de TI o Guarda de Seguridad) un jefe
        /// inmediato asignado.
        /// </summary>
        public bool PerfilCompleto()
        {
            var datosBasicos = !string.IsNullOrWhiteSpace(Cedula) && !string.IsNullOrWhiteSpace(Cargo);
            if (!datosBasicos)
            {
                return false;
            }

            if (RoleNames.ExentoDeJefeInmediato(Rol?.Nombre))
            {
                return true;
            }

            return JefeInmediatoId.HasValue;
        }
    }
}
