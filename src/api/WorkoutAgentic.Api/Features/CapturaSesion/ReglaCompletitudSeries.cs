using WorkoutAgentic.Api.Data.Entities;
using WorkoutAgentic.Api.Data;

namespace WorkoutAgentic.Api.Features.CapturaSesion;

/// <summary>Regla de dominio: cada serie requiere repeticiones y una carga.</summary>
internal static class ReglaCompletitudSeries
{
    public static bool EstaCompleta(RegistroParcial fila) =>
        fila.Reps is not null && (fila.PesoKg is not null || fila.Unidad == Unidad.PesoCorporal);

    public static IReadOnlyList<string> CamposFaltantes(RegistroParcial fila)
    {
        var campos = new List<string>(2);
        if (fila.Reps is null)
        {
            campos.Add("repeticiones");
        }

        if (fila.PesoKg is null && fila.Unidad != Unidad.PesoCorporal)
        {
            campos.Add("peso (o «al cuerpo»)");
        }

        return campos;
    }

    public static string MensajeParaCompletar(IEnumerable<RegistroParcial> filasIncompletas)
    {
        var pendientes = filasIncompletas
            .OrderBy(fila => fila.CreadoUtc)
            .ThenBy(fila => fila.NroSerie)
            .Select(fila =>
            {
                var ejercicio = fila.EjercicioCanonico ?? fila.EjercicioCrudo ?? "ejercicio sin nombre";
                return $"{ejercicio}, serie {fila.NroSerie}: {string.Join(" y ", CamposFaltantes(fila))}";
            });

        return $"Antes de cargar otro ejercicio, completá repeticiones y peso de todas las series. Faltan {string.Join("; ", pendientes)}. Podés dictar, por ejemplo: «primera serie 12 repeticiones con 20 kilos».";
    }
}
