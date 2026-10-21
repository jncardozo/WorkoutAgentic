using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace WorkoutAgentic.Api.Features.WhatsappWebhook;

// Firma X-Hub-Signature-256 (docs/conventions/whatsapp.md): HMAC-SHA256 del
// body crudo (bytes, sin re-serializar) con WHATSAPP_APP_SECRET y comparación
// timing-safe. Sin app secret configurado o sin header válido → inválido.
internal sealed class WhatsappSignatureValidator(IOptions<WhatsappOptions> options)
{
    private readonly byte[] _appSecretBytes = Encoding.UTF8.GetBytes(options.Value.AppSecret);

    public bool IsValid(byte[] rawBody, string? signatureHeader)
    {
        // Fail-closed: sin secret no hay nada contra qué firmar.
        if (_appSecretBytes.Length == 0)
        {
            return false;
        }

        // Header esperado: "sha256=<hex>".
        if (signatureHeader is not { Length: > 0 } header ||
            !header.StartsWith("sha256=", StringComparison.Ordinal))
        {
            return false;
        }

        byte[] headerDigest;
        try
        {
            // FromHexString tolera mayúsculas/minúsculas; lanza si el largo es
            // impar o hay caracteres no hexadecimales.
            headerDigest = Convert.FromHexString(header["sha256=".Length..]);
        }
        catch (FormatException)
        {
            return false;
        }

        var digest = HMACSHA256.HashData(_appSecretBytes, rawBody);
        return CryptographicOperations.FixedTimeEquals(digest, headerDigest);
    }
}
