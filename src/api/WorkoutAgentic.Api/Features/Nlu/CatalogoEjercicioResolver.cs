using System.Text;

namespace WorkoutAgentic.Api.Features.Nlu;

// CatalogResolver determinístico y offline (ADR-0002 S2; reglas puntuales en
// ADR-0003): el LLM entrega texto crudo; acá se decide el EjercicioId contra
// NombreCanonico + Apodos, con umbral y reglas fijas. NUNCA reversea el flujo:
// si no matchea → sugerencias/ambigüedad → aclaración.
internal static class CatalogoEjercicioResolver
{
    // [SUPUESTO ADR-0003]: umbral fijo; se calibran con fixtures reales.
    public const double UmbralMatch = 0.85;

    private const double ScoreExacto = 1.0;
    private const double ScoreContencion = 0.95;
    private const double ScoreGrupo = 0.9;
    private const int MaximoSugerencias = 3;

    // Sugerencias solo para cerca-de-match; palabras lejanas no sugieren.
    private const double UmbralSugerencias = 0.55;

    private static readonly HashSet<string> Stopwords =
        ["el", "la", "los", "las", "de", "del", "al", "en", "con", "a", "y", "o", "un", "una", "unos", "unas", "mi", "mis", "su", "sus", "que", "se", "lo", "para", "por"];

    public static ResultadoResolucionEjercicio Resolver(
        string textoCrudo,
        IReadOnlyList<CatalogoEjercicio> catalogo)
    {
        var query = Normalizar(textoCrudo);
        if (query.Length == 0)
        {
            return ResultadoResolucionEjercicio.SinMatch([]);
        }

        var queryTokens = Tokens(query);

        // Score por ejercicio: el mejor sobre nombre canónico y sus apodos.
        var scored = new (CatalogoEjercicio Ejercicio, double Score)[catalogo.Count];
        for (var i = 0; i < catalogo.Count; i++)
        {
            var item = catalogo[i];
            var mejor = 0.0;
            foreach (var frase in Frases(item))
            {
                var score = Score(query, Normalizar(frase));
                if (score > mejor)
                {
                    mejor = score;
                }
            }

            scored[i] = (item, mejor);
        }

        // 1) ¿Nombró un GRUPO muscular? ("el pecho", "la pierna"): los tokens
        //    del query (sin stopwords) son iguales a los del grupo → la spec
        //    pide aclarar cuál del grupo (NLU §2). ANTES del léxico: si fuera
        //    después, "pecho" matchearía "press de pecho" por contención.
        var delGrupo = scored
            .Where(s => new HashSet<string>(Tokens(Normalizar(s.Ejercicio.GrupoMuscular)))
                .SetEquals(queryTokens))
            .ToList();

        if (delGrupo.Count == 1)
        {
            var soloGrupo = delGrupo[0];
            return ResultadoResolucionEjercicio.Matcheado(
                soloGrupo.Ejercicio.Id, soloGrupo.Ejercicio.NombreCanonico, ScoreGrupo);
        }

        if (delGrupo.Count >= 2)
        {
            var sugerenciasGrupo = delGrupo
                .Take(MaximoSugerencias)
                .Select(s => new SugerenciaEjercicio(s.Ejercicio.Id, s.Ejercicio.NombreCanonico, ScoreGrupo))
                .ToList();
            return ResultadoResolucionEjercicio.Ambiguo(sugerenciasGrupo);
        }

        // 2) match léxico (exacto / contención / levenshtein ≥ umbral).
        var lexicos = scored.Where(s => s.Score >= UmbralMatch).ToList();
        if (lexicos.Count == 1)
        {
            var solo = lexicos[0];
            return ResultadoResolucionEjercicio.Matcheado(
                solo.Ejercicio.Id, solo.Ejercicio.NombreCanonico, solo.Score);
        }

        if (lexicos.Count >= 2)
        {
            var sugerencias = lexicos
                .OrderByDescending(s => s.Score)
                .Take(MaximoSugerencias)
                .Select(s => new SugerenciaEjercicio(s.Ejercicio.Id, s.Ejercicio.NombreCanonico, s.Score))
                .ToList();
            return ResultadoResolucionEjercicio.Ambiguo(sugerencias);
        }

        // 3) nada: sugerencias por cercanía — solo cercanos de verdad (los N
        //    más cerca, spec §4); un score chico no es una sugerencia útil.
        var cercanos = scored
            .Where(s => s.Score > UmbralSugerencias)
            .OrderByDescending(s => s.Score)
            .Take(MaximoSugerencias)
            .Select(s => new SugerenciaEjercicio(s.Ejercicio.Id, s.Ejercicio.NombreCanonico, s.Score))
            .ToList();

        return ResultadoResolucionEjercicio.SinMatch(cercanos);
    }

    private static double Score(string query, string frase)
    {
        if (query.Length == 0 || frase.Length == 0)
        {
            return 0;
        }

        if (query == frase)
        {
            return ScoreExacto;
        }

        if (query.Length >= 4 && frase.Length >= 4 && (frase.Contains(query) || query.Contains(frase)))
        {
            return ScoreContencion;
        }

        return LevenshteinRatio(query, frase);
    }

    private static IEnumerable<string> Frases(CatalogoEjercicio ejercicio)
    {
        yield return ejercicio.NombreCanonico;
        foreach (var apodo in ejercicio.Apodos)
        {
            yield return apodo;
        }
    }

    private static string Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(texto.Length);
        var conAcento = texto.Normalize(System.Text.NormalizationForm.FormD);
        foreach (var c in conAcento)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                continue; // sin diacríticos: "sillón" == "sillon"
            }

            sb.Append(char.ToLowerInvariant(char.IsWhiteSpace(c) || !char.IsAsciiLetterOrDigit(c) ? ' ' : c));
        }

        // Colapsa espacios consecutivos y recorta.
        var partes = sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', partes);
    }

    private static List<string> Tokens(string normalizado) =>
        normalizado
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => !Stopwords.Contains(t))
            .ToList();

    private static double LevenshteinRatio(string a, string b)
    {
        if (a.Length == 0 && b.Length == 0)
        {
            return 1;
        }

        var dist = Levenshtein(a, b);
        return 1.0 - dist / (double)Math.Max(a.Length, b.Length);
    }

    private static int Levenshtein(string a, string b)
    {
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;

        var filaAnterior = new int[b.Length + 1];
        var filaActual = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
        {
            filaAnterior[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            filaActual[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var costo = a[i - 1] == b[j - 1] ? 0 : 1;
                filaActual[j] = Math.Min(
                    Math.Min(filaActual[j - 1] + 1, filaAnterior[j] + 1),
                    filaAnterior[j - 1] + costo);
            }

            (filaAnterior, filaActual) = (filaActual, filaAnterior);
        }

        return filaAnterior[b.Length];
    }
}
