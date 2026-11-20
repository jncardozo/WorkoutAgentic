using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using WorkoutAgentic.Api.Features.PlanillaApi;

namespace WorkoutAgentic.Api.Features.Auth;

internal interface IAccountEmailSender
{
    Task SendResetAsync(string email, string url, CancellationToken cancellationToken);
}

internal sealed class AccountEmailSender(IOptions<CorreoSmtpOptions> options, ILogger<AccountEmailSender> logger) : IAccountEmailSender
{
    public async Task SendResetAsync(string email, string url, CancellationToken cancellationToken)
    {
        var config = options.Value;
        if (string.IsNullOrWhiteSpace(config.Host) || string.IsNullOrWhiteSpace(config.FromAddress))
        {
            logger.LogWarning("Recuperación de contraseña no disponible: configurar SMTP.");
            return;
        }
        try
        {
            using var message = new MailMessage(config.FromAddress, email, "Restablecé tu contraseña de WorkoutAgentic",
                $"Abrí este enlace para elegir una nueva contraseña (vence en una hora):\n\n{url}\n\nSi no lo solicitaste, ignorá este mensaje.");
            using var smtp = new SmtpClient(config.Host, config.Port)
            {
                EnableSsl = config.EnableSsl, UseDefaultCredentials = false,
                Credentials = string.IsNullOrWhiteSpace(config.UserName) ? null : new NetworkCredential(config.UserName, config.Password),
            };
            await smtp.SendMailAsync(message, cancellationToken);
        }
        catch (Exception exception) when (exception is SmtpException or InvalidOperationException or FormatException or ArgumentException)
        {
            // Never log a message/link/token or SMTP credentials.
            logger.LogWarning("No se pudo entregar el correo de recuperación.");
        }
    }
}
