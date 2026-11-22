using WorkoutAgentic.Api.Features.PlanillaApi;

namespace WorkoutAgentic.Api.Features.Auth;

internal interface IAccountEmailSender
{
    Task SendResetAsync(string email, string url, CancellationToken cancellationToken);
}

internal sealed class AccountEmailSender(EmailDeliverySender sender, ILogger<AccountEmailSender> logger) : IAccountEmailSender
{
    public async Task SendResetAsync(string email, string url, CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(email, "Restablecé tu contraseña de WorkoutAgentic",
            $"Abrí este enlace para elegir una nueva contraseña (vence en una hora):\n\n{url}\n\nSi no lo solicitaste, ignorá este mensaje.",
            attachment: null, cancellationToken);
        if (result != EstadoEnvioCorreo.Enviado)
            logger.LogWarning("No se pudo entregar el correo de recuperación.");
    }
}
