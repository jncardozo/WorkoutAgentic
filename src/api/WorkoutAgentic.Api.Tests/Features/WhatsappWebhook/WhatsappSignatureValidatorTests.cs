using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using WorkoutAgentic.Api.Features.WhatsappWebhook;

namespace WorkoutAgentic.Api.Tests.Features.WhatsappWebhook;

public class WhatsappSignatureValidatorTests
{
    private const string AppSecret = "test-app-secret";

    private readonly WhatsappSignatureValidator _validator =
        new(Options.Create(new WhatsappOptions { AppSecret = AppSecret }));

    [Fact]
    public void IsValid_WithExpectedSignature_ReturnsTrue()
    {
        var body = Encoding.UTF8.GetBytes("payload-1");

        Assert.True(_validator.IsValid(body, Signature(body)));
    }

    [Fact]
    public void IsValid_WithDigestOfOtherBody_ReturnsFalse()
    {
        var body = Encoding.UTF8.GetBytes("payload-1");
        var other = Encoding.UTF8.GetBytes("payload-distinto");

        Assert.False(_validator.IsValid(body, Signature(other)));
    }

    [Fact]
    public void IsValid_WithSignatureFromWrongSecret_ReturnsFalse()
    {
        var body = Encoding.UTF8.GetBytes("payload-1");
        var forged = Signature(body, secret: "otro-secret");

        Assert.False(_validator.IsValid(body, forged));
    }

    [Fact]
    public void IsValid_WithoutHeader_ReturnsFalse()
    {
        var body = Encoding.UTF8.GetBytes("payload-1");

        Assert.False(_validator.IsValid(body, signatureHeader: null));
        Assert.False(_validator.IsValid(body, string.Empty));
    }

    [Fact]
    public void IsValid_WithUnexpectedPrefix_ReturnsFalse()
    {
        var body = Encoding.UTF8.GetBytes("payload-1");
        var header = "md5=" + Signature(body)["sha256=".Length..];

        Assert.False(_validator.IsValid(body, header));
    }

    [Fact]
    public void IsValid_WithMalformedHex_ReturnsFalse()
    {
        var body = Encoding.UTF8.GetBytes("payload-1");

        Assert.False(_validator.IsValid(body, "sha256=zz-no-es-hex"));
        Assert.False(_validator.IsValid(body, "sha256=abc")); // largo impar
    }

    [Fact]
    public void IsValid_WithoutConfiguredSecret_FailsClosed()
    {
        // Sin AppSecret no hay forma de validar: se descarta el mensaje.
        var validator = new WhatsappSignatureValidator(Options.Create(new WhatsappOptions()));
        var body = Encoding.UTF8.GetBytes("payload-1");

        Assert.False(validator.IsValid(body, Signature(body)));
    }

    // Meta envía "sha256=<hex>`" en minúsculas; replicamos ese formato exacto.
    private static string Signature(byte[] body, string secret = AppSecret) =>
        "sha256=" + Convert
            .ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body))
            .ToLowerInvariant();
}
