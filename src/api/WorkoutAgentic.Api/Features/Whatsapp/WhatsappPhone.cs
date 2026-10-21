using System.Text;

namespace WorkoutAgentic.Api.Features.Whatsapp;

// Teléfonos en formato internacional SIN +, (, ) ni - (docs/conventions/whatsapp.md):
// https://graph.facebook.com espera solo dígitos (ej. 5215512345678).
public static class WhatsappPhone
{
    // Conserva únicamente los dígitos (canonización defensiva del envío).
    public static string Canonizar(string? telefono)
    {
        if (string.IsNullOrWhiteSpace(telefono))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(telefono.Length);
        foreach (var c in telefono)
        {
            if (char.IsDigit(c))
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    // Los números son datos del alumno: en logs solo se llevan enmascarados.
    public static string Mask(string? telefono)
    {
        var digits = Canonizar(telefono);
        if (digits.Length <= 4)
        {
            return "***";
        }

        return digits[..2] + "*" + digits[^4..];
    }
}
