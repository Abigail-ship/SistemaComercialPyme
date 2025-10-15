using Microsoft.Extensions.Options;
using SistemaComercialPyme.Models;
using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace SistemaComercialPyme.Services
{
    public class EmailService
    {
        private readonly SmtpClient _smtp;
        private readonly EmailSettings _settings;

        public EmailService(IOptions<EmailSettings> settings)
        {
            _settings = settings.Value;
            _smtp = new SmtpClient(_settings.Host)
            {
                Port = _settings.Port,
                Credentials = new NetworkCredential(_settings.From, _settings.Password),
                EnableSsl = _settings.EnableSSL
            };
        }

        public async Task SendAsync(string to, string subject, string body)
        {
            try
            {
                using var message = new MailMessage(_settings.From!, to, subject, body);
                message.IsBodyHtml = true;
                await _smtp.SendMailAsync(message);
            }
            catch (SmtpException ex)
            {
                Console.WriteLine($"⚠️ Error enviando correo a {to}: {ex.Message}");
                // Para no corromper el webhook de Stripe
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Error inesperado enviando correo: {ex.Message}");
            }
        }
        public async Task EnviarCorreoAsync(string destinatario, string asunto, string cuerpoHtml)
        {
            var mensaje = new MailMessage(_settings.From!, destinatario, asunto, cuerpoHtml);
            mensaje.IsBodyHtml = true;

            await _smtp.SendMailAsync(mensaje);
        }
    }
}
