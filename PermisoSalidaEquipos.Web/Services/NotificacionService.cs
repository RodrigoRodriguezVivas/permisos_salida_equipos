using System.Threading.Tasks;
using PermisoSalidaEquipos.Web.Models;

namespace PermisoSalidaEquipos.Web.Services
{
    /// <summary>
    /// Compone y envía los correos de cada punto del flujo de aprobación. Mantiene el
    /// contenido de los mensajes fuera de los controladores.
    /// </summary>
    public class NotificacionService
    {
        private readonly IEmailService _emailService;
        private readonly string _urlBase;

        public NotificacionService(IEmailService emailService, Microsoft.Extensions.Configuration.IConfiguration configuration)
        {
            _emailService = emailService;
            _urlBase = configuration["Smtp:UrlBaseAplicacion"]?.TrimEnd('/') ?? string.Empty;
        }

        private string EnlaceSolicitud(int solicitudId) =>
            string.IsNullOrEmpty(_urlBase) ? $"/Solicitudes/Detalle/{solicitudId}" : $"{_urlBase}/Solicitudes/Detalle/{solicitudId}";

        public Task NotificarNuevaSolicitudAsync(Solicitud solicitud, Usuario jefeInmediato)
        {
            var asunto = $"Nuevo permiso de salida de equipo por aprobar (#{solicitud.Id})";
            var cuerpo = $@"
                <p>Hola {jefeInmediato.NombreCompleto},</p>
                <p><strong>{solicitud.Solicitante?.NombreCompleto}</strong> ha radicado un permiso de salida de equipo de cómputo
                ({solicitud.TipoEquipo} {solicitud.Marca} {solicitud.Modelo}) que requiere tu aprobación como jefe inmediato.</p>
                <p><a href='{EnlaceSolicitud(solicitud.Id)}'>Ver y responder la solicitud</a></p>";
            return _emailService.EnviarAsync(jefeInmediato.Correo, asunto, cuerpo);
        }

        public Task NotificarPendienteDirectorTIAsync(Solicitud solicitud, Usuario directorTI)
        {
            var asunto = $"Permiso de salida de equipo pendiente de tu aprobación final (#{solicitud.Id})";
            var cuerpo = $@"
                <p>Hola {directorTI.NombreCompleto},</p>
                <p>La solicitud #{solicitud.Id} de <strong>{solicitud.Solicitante?.NombreCompleto}</strong>
                ({solicitud.TipoEquipo} {solicitud.Marca} {solicitud.Modelo}, serie {solicitud.NumeroSerie})
                fue aprobada por su jefe inmediato y está pendiente de tu aprobación final.</p>
                <p><a href='{EnlaceSolicitud(solicitud.Id)}'>Ver y responder la solicitud</a></p>";
            return _emailService.EnviarAsync(directorTI.Correo, asunto, cuerpo);
        }

        public Task NotificarDecisionAlSolicitanteAsync(Solicitud solicitud, string estadoTexto, string? comentario)
        {
            var asunto = $"Tu permiso de salida de equipo #{solicitud.Id}: {estadoTexto}";
            var comentarioHtml = string.IsNullOrWhiteSpace(comentario) ? string.Empty : $"<p><strong>Comentario:</strong> {comentario}</p>";
            var cuerpo = $@"
                <p>Hola {solicitud.Solicitante?.NombreCompleto},</p>
                <p>Tu solicitud #{solicitud.Id} ({solicitud.TipoEquipo} {solicitud.Marca} {solicitud.Modelo}) cambió de estado a:
                <strong>{estadoTexto}</strong>.</p>
                {comentarioHtml}
                <p><a href='{EnlaceSolicitud(solicitud.Id)}'>Ver el detalle</a></p>";
            return _emailService.EnviarAsync(solicitud.Solicitante!.Correo, asunto, cuerpo);
        }
    }
}
