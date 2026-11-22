using System.Net.Http.Json;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace WorkoutAgentic.Api.Features.PlanillaApi;

public sealed class EmailDeliveryOptions
{
    public const string SectionName = "EmailDelivery";
    public string Provider { get; set; } = "smtp";
    public string ApiKey { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "WorkoutAgentic";
}

// Shared delivery for worksheet attachments and password recovery. Selecting an
// HTTPS provider never silently falls back to SMTP or another destination.
internal sealed class EmailDeliverySender(
    IOptions<CorreoSmtpOptions> smtpOptions,
    IOptions<EmailDeliveryOptions> deliveryOptions,
    IHttpClientFactory clients,
    ILogger<EmailDeliverySender> logger)
{
    internal const string ClientName = "email-delivery";

    public bool IsConfigured => ConfigurationIssue is null;

    private string? ConfigurationIssue => deliveryOptions.Value.Provider?.Trim().ToLowerInvariant() switch
    {
        "brevo" when string.IsNullOrWhiteSpace(deliveryOptions.Value.ApiKey)
            => "Falta EmailDelivery:ApiKey.",
        "brevo" when !MailAddress.TryCreate(deliveryOptions.Value.FromAddress, out _)
            => "Falta un EmailDelivery:FromAddress válido.",
        "brevo" => null,
        "smtp" when string.IsNullOrWhiteSpace(smtpOptions.Value.Host)
            => "Falta CorreoSmtp:Host.",
        "smtp" when smtpOptions.Value.Port is < 1 or > 65535
            => "CorreoSmtp:Port no es válido.",
        "smtp" when !MailAddress.TryCreate(smtpOptions.Value.FromAddress, out _)
            => "Falta un CorreoSmtp:FromAddress válido.",
        "smtp" => null,
        _ => "EmailDelivery:Provider debe ser smtp o brevo.",
    };

    public async Task<EstadoEnvioCorreo> SendAsync(string recipient, string subject, string body,
        PlanillaArchivo? attachment, CancellationToken cancellationToken)
    {
        var config = deliveryOptions.Value;
        var provider = config.Provider?.Trim().ToLowerInvariant();
        if (ConfigurationIssue is { } issue)
        {
            logger.LogWarning("Envío de correo sin configurar: {Issue}", issue);
            return EstadoEnvioCorreo.ConfiguracionIncompleta;
        }
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            if (provider == "brevo")
            {
                var payload = new Dictionary<string, object>
                {
                    ["sender"] = new { name = config.FromName, email = config.FromAddress },
                    ["to"] = new[] { new { email = recipient } },
                    ["subject"] = subject,
                    ["textContent"] = body,
                };
                if (attachment is not null)
                    payload["attachment"] = new[] { new { name = attachment.Nombre, content = Convert.ToBase64String(attachment.Contenido) } };
                using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email");
                request.Headers.Add("api-key", config.ApiKey);
                request.Content = JsonContent.Create(payload);
                using var client = clients.CreateClient(ClientName);
                using var response = await client.SendAsync(request, timeout.Token);
                if (response.IsSuccessStatusCode) return EstadoEnvioCorreo.Enviado;
                // Provider bodies may contain personal data. Do not log them.
                logger.LogWarning("El proveedor de correo HTTPS rechazó el envío: estado {Status}.", (int)response.StatusCode);
                return EstadoEnvioCorreo.Fallo;
            }

            var smtpConfig = smtpOptions.Value;
            using var message = new MailMessage
            {
                From = new MailAddress(smtpConfig.FromAddress, smtpConfig.FromName), Subject = subject, Body = body,
            };
            message.To.Add(new MailAddress(recipient));
            if (attachment is not null)
                message.Attachments.Add(new Attachment(new MemoryStream(attachment.Contenido), attachment.Nombre, attachment.TipoContenido));
            using var smtp = new SmtpClient(smtpConfig.Host, smtpConfig.Port)
            {
                EnableSsl = smtpConfig.EnableSsl, DeliveryMethod = SmtpDeliveryMethod.Network, UseDefaultCredentials = false,
                Credentials = string.IsNullOrWhiteSpace(smtpConfig.UserName) ? null
                    : new System.Net.NetworkCredential(smtpConfig.UserName, smtpConfig.Password),
            };
            await smtp.SendMailAsync(message, timeout.Token);
            return EstadoEnvioCorreo.Enviado;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Se agotó el tiempo de espera del proveedor de correo.");
            return EstadoEnvioCorreo.Fallo;
        }
        catch (Exception exception) when (exception is SmtpException or HttpRequestException or InvalidOperationException or FormatException or ArgumentException)
        {
            // Do not expose addresses, authentication credentials or reset links.
            logger.LogWarning("No se pudo entregar el correo al proveedor.");
            return EstadoEnvioCorreo.Fallo;
        }
    }
}
