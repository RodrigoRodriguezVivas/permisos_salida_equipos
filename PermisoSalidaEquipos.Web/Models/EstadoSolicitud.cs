namespace PermisoSalidaEquipos.Web.Models
{
    /// <summary>
    /// Estados posibles del flujo de aprobación de un permiso de salida de equipo.
    /// Flujo normal: PendienteJefe -> PendienteDirectorTI -> Aprobada.
    /// El jefe o el Director de TI pueden rechazar en su respectiva etapa, y el
    /// solicitante puede cancelar mientras la solicitud siga pendiente.
    /// </summary>
    public enum EstadoSolicitud
    {
        PendienteJefe = 0,
        PendienteDirectorTI = 1,
        Aprobada = 2,
        RechazadaJefe = 3,
        RechazadaDirectorTI = 4,
        CanceladaPorSolicitante = 5
    }
}
