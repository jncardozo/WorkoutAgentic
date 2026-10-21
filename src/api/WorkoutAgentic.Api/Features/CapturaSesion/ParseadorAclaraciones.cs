using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace WorkoutAgentic.Api.Features.CapturaSesion;

// Parser determinístico para las RESPUESTAS del alumno a una aclaración
// (spec NLU §4): el bot ya preguntó algo puntual, así que se leen reglas
// fijas sobre el texto crudo, SIN LLM. Si nada matchea → null = la pregunta
// queda abierta (reintento); jamás un valor inventado (regla madre).
internal static class ParseadorAclaraciones
{
    // "50" / "50,5" / "50.5" / "50 kg" / "50 kilos y medio" / "50 y medio"…
    private static readonly Regex PesoRegex =
        new(@"^(?<num>\d+(?:[.,]\d+)?)(?:(?<medio>\s*y\s*medi[oa])|(?<unidad>\s*(?:k(?:g)?s?|kilos?))(?<medio2>\s*y\s*medi[oa])?)?(?<fin>\s*)$",
            RegexOptions.Compiled);

    private static readonly Regex EnteroRegex = new(@"^\d+$", RegexOptions.Compiled);

    // Respuestas sueltas tipo "y medio": el dato faltante era medio kilo
    // ("hice 12 con… kilos y medio"). [SUPUESTO ADR-0004: frases de uso común]
    private static readonly string[] MedioSolo = ["y medio", "y media", "kilo y medio", "kilos y medio", "un kilo y medio", "kg y medio", "k y medio"];

    public static decimal? ParsearPeso(string? texto)
    {
        var normalizado = Normalizar(texto);
        if (normalizado.Length == 0)
        {
            return null;
        }

        if (MedioSolo.Contains(normalizado))
        {
            return 1.5m;
        }

        var match = PesoRegex.Match(normalizado);
        if (!match.Success
            || !decimal.TryParse(
                match.Groups["num"].Value.Replace(',', '.'),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var valor))
        {
            return null;
        }

        var conMedio = match.Groups["medio"].Success || match.Groups["medio2"].Success;
        return conMedio ? valor + 0.5m : valor;
    }

    // Dígitos puros ("2", "12"): opción de una lista enumerada o una cantidad.
    // Las palabras ("dos", "tercera") no se interpretan: reintento, sin inventar.
    public static int? ParsearEntero(string? texto)
    {
        // Signo negativo explícito: jamás se acepta de forma silenciosa (el
        // normalizador convertiría "-1" en "1"); mejor pedir de nuevo.
        if (texto is not null && texto.Contains('-'))
        {
            return null;
        }

        var normalizado = Normalizar(texto);
        return EnteroRegex.IsMatch(normalizado)
            && int.TryParse(normalizado, NumberStyles.None, CultureInfo.InvariantCulture, out var valor)
            && valor > 0
            ? valor
            : null;
    }

    // "otro": el texto crudo queda como descripción libre (editable en la SPA).
    public static bool EsOtro(string? texto) => Normalizar(texto) is "otro" or "otra";

    // "seguir": reanuda la carga pausada por inactividad (business-rules §1).
    public static bool EsSeguir(string? texto) => Normalizar(texto) is "seguir";

    // Lowercase, sin diacríticos, espacios colapsados. Conserva dígitos y
    // separadores decimales (coma/punto) porque definen el peso.
    internal static string Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(texto.Length);
        var conAcento = texto.Normalize(NormalizationForm.FormD);
        foreach (var c in conAcento)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue; // sin diacríticos: "medio" == "médio"
            }

            if (c is ',' or '.')
            {
                sb.Append(c); // separador decimal
                continue;
            }

            sb.Append(char.IsWhiteSpace(c) || !char.IsAsciiLetterOrDigit(c) ? ' ' : char.ToLowerInvariant(c));
        }

        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
