using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using MailKit.Net.Smtp;
using MailKit.Security;

namespace PermisoSalidaEquipos.Web.Services
{
    /// <summary>
    /// Envío de correo vía SMTP usando MailKit. La configuración se toma de la
    /// sección "Smtp" de appsettings.json (ver README para los valores a solicitar al
    /// equipo de infraestructura de Aligraf).
    /// </summary>
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task EnviarAsync(string destinatario, string asunto, string cuerpoHtml)
        {
            if (string.IsNullOrWhiteSpace(destinatario))
            {
                _logger.LogWarning("Se omitió el envío de correo '{Asunto}' porque el destinatario está vacío.", asunto);
                return;
            }

            var habilitado = _configuration.GetValue<bool>("Smtp:Habilitado");
            if (!habilitado)
            {
                _logger.LogInformation("Envío de correo deshabilitado (Smtp:Habilitado=false). Se omitió '{Asunto}' para {Destinatario}.", asunto, destinatario);
                return;
            }

            try
            {
                var mensaje = new MimeMessage();
                var remitenteCorreo = _configuration["Smtp:CorreoRemitente"] ?? "no-responder@alianzagrafica.com";
                var remitenteNombre = _configuration["Smtp:NombreRemitente"] ?? "Permisos de Salida de Equipos";
                mensaje.From.Add(new MailboxAddress(remitenteNombre, remitenteCorreo));
                mensaje.To.Add(MailboxAddress.Parse(destinatario));
                mensaje.Subject = asunto;
                mensaje.Body = new TextPart("html") { Text = cuerpoHtml };

                using var cliente = new SmtpClient();

                var host = _configuration["Smtp:Host"];
                var puerto = _configuration.GetValue<int>("Smtp:Puerto", 25);
                var usarSsl = _configuration.GetValue<bool>("Smtp:UsarSsl", false);

                if (string.IsNullOrWhiteSpace(host))
                {
                    _logger.LogWarning("Smtp:Host no está configurado. Se omitió el envío de '{Asunto}'.", asunto);
                    return;
                }

                var opcionesSeguridad = usarSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable;
                await cliente.ConnectAsync(host, puerto, opcionesSeguridad);

                var usuario = _configuration["Smtp:Usuario"];
                var clave = _configuration["Smtp:Clave"];
                if (!string.IsNullOrWhiteSpace(usuario))
                {
                    await cliente.AuthenticateAsync(usuario, clave);
                }

                await cliente.SendAsync(mensaje);
                await cliente.DisconnectAsync(true);
            }
            catch (Exception ex)
            {
                // Una falla de correo nunca debe interrumpir el flujo de aprobación.
                _logger.LogError(ex, "No se pudo enviar el correo '{Asunto}' a {Destinatario}.", asunto, destinatario);
            }
        }
    }
}
