namespace WorkoutAgentic.Api.Features.Whatsapp;

// Credenciales del canal WhatsApp Cloud API (webhook entrante + envío saliente).
// Leídas de configuración (user-secrets en dev / variables de entorno en prod).
// NUNCA en appsettings.json ni en código (docs/conventions/whatsapp.md).
public sealed class WhatsappOptions
{
    public const string SectionName = "Whatsapp";

    // Clave HMAC para validar X-Hub-Signature-256 (WHATSAPP_APP_SECRET).
    public string AppSecret { get; set; } = string.Empty;

    // Token del handshake GET del webhook (WHATSAPP_VERIFY_TOKEN).
    public string VerifyToken { get; set; } = string.Empty;

    // Bearer para llamar a Graph API (WHATSAPP_ACCESS_TOKEN).
    public string AccessToken { get; set; } = string.Empty;

    // Número emisor en la llamada a Graph API (WHATSAPP_PHONE_NUMBER_ID).
    public string PhoneNumberId { get; set; } = string.Empty;

    // Versión de la Graph API (WHATSAPP_API_VERSION). Default según el
    // ejemplo de la convención: "v23.0".
    public string ApiVersion { get; set; } = "v23.0";
}
