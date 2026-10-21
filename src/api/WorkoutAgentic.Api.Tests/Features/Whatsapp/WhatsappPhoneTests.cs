using WorkoutAgentic.Api.Features.Whatsapp;

namespace WorkoutAgentic.Api.Tests.Features.Whatsapp;

// Canonización de teléfonos (docs/conventions/whatsapp.md): solo dígitos,
// sin +, (, ), -, ni espacios.
public class WhatsappPhoneTests
{
    [Theory]
    [InlineData("+52 155 1234 5678", "5215512345678")]
    [InlineData("(+521) 55-1234-5678", "5215512345678")]
    [InlineData("5215512345678", "5215512345678")]
    [InlineData(" 52-1-55-1234-5678 ", "5215512345678")]
    [InlineData("wa-id=5215512345678", "5215512345678")]
    public void Canonizar_ConservaSoloDigitos(string entrada, string esperado) =>
        Assert.Equal(esperado, WhatsappPhone.Canonizar(entrada));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc-def")]
    public void Canonizar_SinDigitos_DevuelveVacio(string? entrada) =>
        Assert.Equal(string.Empty, WhatsappPhone.Canonizar(entrada));

    [Fact]
    public void Mask_DejaPrefijoYSufijo() =>
        Assert.Equal("52*5678", WhatsappPhone.Mask("5215512345678"));

    [Theory]
    [InlineData(null)]
    [InlineData("123")]
    [InlineData("1234")]
    public void Mask_TelefonosCortos_TotalmenteEnmascarados(string? entrada) =>
        Assert.Equal("***", WhatsappPhone.Mask(entrada));
}
