using System;
using System.Collections.Generic;

namespace PermisoSalidaEquipos.Web.Models
{
    /// <summary>
    /// Permiso de salida de un equipo de cómputo. Guarda una copia (snapshot) de la
    /// cédula, el cargo y el jefe inmediato del solicitante en el momento de crear la
    /// solicitud, de manera que el historial no cambie si esos datos se actualizan
    /// después en el perfil del usuario.
    /// </summary>
    public class Solicitud
    {
        public int Id { get; set; }

        public int SolicitanteId { get; set; }
        public Usuario? Solicitante { get; set; }

        public string CedulaSolicitante { get; set; } = string.Empty;
        public string CargoSolicitante { get; set; } = string.Empty;

        // ----- Datos del equipo -----
        public string TipoEquipo { get; set; } = string.Empty;
        public string Marca { get; set; } = string.Empty;
        public string Modelo { get; set; } = string.Empty;
        public string NumeroSerie { get; set; } = string.Empty;
        public string? Accesorios { get; set; }

        // ----- Motivo y fechas -----
        public string Motivo { get; set; } = string.Empty;
        public string? MotivoDetalle { get; set; }
        public DateTime FechaSalida { get; set; }
        public DateTime? FechaRetornoEstimada { get; set; }
        public string? Observaciones { get; set; }

        // ----- Flujo de aprobación -----
        public EstadoSolicitud Estado { get; set; } = EstadoSolicitud.PendienteJefe;

        public DateTime FechaCreacion { get; set; }

        /// <summary>Jefe inmediato del solicitante al momento de crear la solicitud.</summary>
        public int JefeInmediatoId { get; set; }
        public Usuario? JefeInmediatoAsignado { get; set; }
        public DateTime? FechaDecisionJefe { get; set; }
        public string? ComentarioJefe { get; set; }

        /// <summary>Usuario con rol Director de TI que tomó la decisión final.</summary>
        public int? DirectorTIRevisorId { get; set; }
        public Usuario? DirectorTIRevisor { get; set; }
        public DateTime? FechaDecisionDirectorTI { get; set; }
        public string? ComentarioDirectorTI { get; set; }

        public ICollection<HistorialSolicitud> Historial { get; set; } = new List<HistorialSolicitud>();
    }
}
