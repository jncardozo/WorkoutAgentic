namespace WorkoutAgentic.Api.Features.Nlu;

// Analizador del texto reconocido o escrito en el navegador. El backend no
// recibe ni procesa audio: si falla una etapa no devuelve datos inventados.
public interface IMensajesNlu
{
    Task<AnalisisCaptura?> AnalizarTextoAsync(
        string texto,
        CancellationToken cancellationToken);
}
