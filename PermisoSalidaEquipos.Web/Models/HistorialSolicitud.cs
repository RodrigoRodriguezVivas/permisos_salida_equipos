using System;

namespace PermisoSalidaEquipos.Web.Models
{
    /// <summary>
    /// Registro de auditoría: una fila por cada cambio de estado de una solicitud
    /// (creación, aprobación/rechazo del jefe, aprobación/rechazo del Director de TI,
    /// cancelación). Se muestra como línea de tiempo en el detalle de la solicitud.
    /// </summary>
    public class HistorialSolicitud
    {
        public int Id { get; set; }

        public int SolicitudId { get; set; }
        public Solicitud? Solicitud { get; set; }

        public EstadoSolicitud Estado { get; set; }

        public int UsuarioId { get; set; }
        public Usuario? Usuario { get; set; }

        public DateTime Fecha { get; set; }

        public string? Comentario { get; set; }
    }
}
