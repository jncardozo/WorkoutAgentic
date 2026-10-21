namespace WorkoutAgentic.Api.Features.WhatsappWebhook;

// Credenciales de WhatsApp Cloud API — leídas de configuración (user-secrets en
// dev / variables de entorno en prod). NUNCA en appsettings.json ni en código
// (docs/conventions/whatsapp.md).
public sealed class WhatsappOptions
{
    public const string SectionName = "Whatsapp";

    // Clave HMAC para validar X-Hub-Signature-256 (WHATSAPP_APP_SECRET).
    public string AppSecret { get; set; } = string.Empty;

    // Token del handshake GET del webhook (WHATSAPP_VERIFY_TOKEN).
    public string VerifyToken { get; set; } = string.Empty;
}
