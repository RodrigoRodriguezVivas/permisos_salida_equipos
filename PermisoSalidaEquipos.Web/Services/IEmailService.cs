using System.Threading.Tasks;

namespace PermisoSalidaEquipos.Web.Services
{
    public interface IEmailService
    {
        /// <summary>
        /// Envía un correo. Las implementaciones deben registrar (log) los errores de
        /// envío sin lanzar excepción, para que una falla de correo nunca bloquee el
        /// flujo de aprobación de una solicitud.
        /// </summary>
        Task EnviarAsync(string destinatario, string asunto, string cuerpoHtml);
    }
}
