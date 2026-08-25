namespace PermisoSalidaEquipos.Web.Models
{
    /// <summary>
    /// Estados posibles del flujo de aprobación de un permiso de salida de equipo.
    /// Flujo normal: PendienteJefe -> PendienteDirectorTI -> Aprobada -> SalioDeLaEmpresa.
    /// El jefe o el Director de TI pueden rechazar en su respectiva etapa, y el
    /// solicitante puede cancelar mientras la solicitud siga pendiente. Una vez
    /// Aprobada, el Guarda de Seguridad la revisa en portería y, cuando el equipo
    /// realmente sale de las instalaciones, la pasa a SalioDeLaEmpresa.
    /// </summary>
    public enum EstadoSolicitud
    {
        PendienteJefe = 0,
        PendienteDirectorTI = 1,
        Aprobada = 2,
        RechazadaJefe = 3,
        RechazadaDirectorTI = 4,
        CanceladaPorSolicitante = 5,
        SalioDeLaEmpresa = 6
    }
}
