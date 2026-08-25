using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using PermisoSalidaEquipos.Web.Models;

namespace PermisoSalidaEquipos.Web.ViewModels
{
    public class CrearSolicitudViewModel
    {
        [Required(ErrorMessage = "Selecciona el tipo de equipo.")]
        [Display(Name = "Tipo de equipo")]
        public string TipoEquipo { get; set; } = string.Empty;

        [Required(ErrorMessage = "La marca es obligatoria.")]
        public string Marca { get; set; } = string.Empty;

        [Required(ErrorMessage = "El modelo es obligatorio.")]
        public string Modelo { get; set; } = string.Empty;

        [Required(ErrorMessage = "El número de serie/placa es obligatorio.")]
        [Display(Name = "Número de serie o placa de inventario")]
        public string NumeroSerie { get; set; } = string.Empty;

        [Display(Name = "Accesorios que salen con el equipo")]
        public string? Accesorios { get; set; }

        [Required(ErrorMessage = "Selecciona el motivo de la salida.")]
        [Display(Name = "Motivo de la salida")]
        public string Motivo { get; set; } = string.Empty;

        [Display(Name = "Detalle del motivo")]
        public string? MotivoDetalle { get; set; }

        [Required(ErrorMessage = "La fecha de salida es obligatoria.")]
        [Display(Name = "Fecha y hora de salida")]
        [DataType(DataType.DateTime)]
        public DateTime FechaSalida { get; set; } = DateTime.Now;

        [Display(Name = "Fecha estimada de retorno")]
        [DataType(DataType.Date)]
        public DateTime? FechaRetornoEstimada { get; set; }

        [Display(Name = "Observaciones")]
        public string? Observaciones { get; set; }

        public static readonly string[] TiposEquipo =
        {
            "Portátil", "Equipo de escritorio", "Monitor", "Impresora", "Proyector", "Tablet", "Otro"
        };

        public static readonly string[] Motivos =
        {
            "Reparación externa", "Préstamo / trabajo remoto", "Mantenimiento preventivo", "Evento externo", "Otro"
        };
    }

    public class DecisionSolicitudViewModel
    {
        public int SolicitudId { get; set; }

        [Display(Name = "Comentario")]
        public string? Comentario { get; set; }

        public bool Aprobar { get; set; }
    }

    /// <summary>Confirmación del Guarda de Seguridad de que el equipo salió de la empresa.</summary>
    public class ConfirmarSalidaViewModel
    {
        public int SolicitudId { get; set; }

        [Display(Name = "Observaciones")]
        public string? Comentario { get; set; }
    }

    public class SolicitudListItemViewModel
    {
        public int Id { get; set; }
        public string SolicitanteNombre { get; set; } = string.Empty;
        public string TipoEquipo { get; set; } = string.Empty;
        public string Marca { get; set; } = string.Empty;
        public string Modelo { get; set; } = string.Empty;
        public DateTime FechaSalida { get; set; }
        public DateTime FechaCreacion { get; set; }
        public EstadoSolicitud Estado { get; set; }
    }

    public class SolicitudDetalleViewModel
    {
        public Solicitud Solicitud { get; set; } = null!;
        public bool PuedeDecidirComoJefe { get; set; }
        public bool PuedeDecidirComoDirector { get; set; }
        public bool PuedeConfirmarSalida { get; set; }
        public bool PuedeCancelar { get; set; }
    }

    public static class EstadoSolicitudTexto
    {
        public static string Descripcion(EstadoSolicitud estado) => estado switch
        {
            EstadoSolicitud.PendienteJefe => "Pendiente de aprobación del jefe inmediato",
            EstadoSolicitud.PendienteDirectorTI => "Pendiente de aprobación del Director de TI",
            EstadoSolicitud.Aprobada => "Aprobada",
            EstadoSolicitud.RechazadaJefe => "Rechazada por el jefe inmediato",
            EstadoSolicitud.RechazadaDirectorTI => "Rechazada por el Director de TI",
            EstadoSolicitud.CanceladaPorSolicitante => "Cancelada por el solicitante",
            EstadoSolicitud.SalioDeLaEmpresa => "Equipo entregado, salió de la empresa",
            _ => estado.ToString()
        };

        public static string ClaseBadge(EstadoSolicitud estado) => estado switch
        {
            EstadoSolicitud.PendienteJefe => "text-bg-warning",
            EstadoSolicitud.PendienteDirectorTI => "text-bg-warning",
            EstadoSolicitud.Aprobada => "text-bg-success",
            EstadoSolicitud.RechazadaJefe => "text-bg-danger",
            EstadoSolicitud.RechazadaDirectorTI => "text-bg-danger",
            EstadoSolicitud.CanceladaPorSolicitante => "text-bg-secondary",
            EstadoSolicitud.SalioDeLaEmpresa => "text-bg-primary",
            _ => "text-bg-secondary"
        };
    }
}
