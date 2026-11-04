using Microsoft.EntityFrameworkCore;
using WorkoutAgentic.Api.Data;
using WorkoutAgentic.Api.Data.Entities;
using WorkoutAgentic.Api.Features.CapturaSesion;

namespace WorkoutAgentic.Api.Features.PlanillaApi;

// Resultado de una mutación: la vista refrescada o la falla del dominio —
// nunca ambas. Una falla no persiste nada (regla madre: el estado queda tal
// como estaba).
internal sealed record ResultadoMutacion<TVista>(TVista? Vista, PlanillaFalla? Falla)
{
    internal static ResultadoMutacion<TVista> ConFalla(PlanillaFalla falla) => new(default, falla);

    internal static ResultadoMutacion<TVista> ConVista(TVista vista) => new(vista, default);
}

// Mutaciones de la cuadrícula del alumno (reglas §2: editar, eliminar y
// agregar filas ANTES de confirmar + confirmación como snapshot write-once).
// La edición de la SPA convive con el chat: la cuadrícula es editable solo
// cuando el chat no tiene la palabra (EnCurso o Lista; nunca
// EsperandoAclaracion ni Expirada — la pausa no deja entrar datos).
internal interface IPlanillaMutaciones
{
    Task<ResultadoMutacion<RespuestaSesionActual>> AgregarFilaAsync(
        int alumnoId, ComandoNuevaFila comando, CancellationToken cancellationToken);

    Task<ResultadoMutacion<RespuestaSesionActual>> EditarFilaAsync(
        int filaId, ComandoEdicionFila comando, CancellationToken cancellationToken);

    Task<ResultadoMutacion<RespuestaSesionActual>> EliminarFilaAsync(
        int filaId, CancellationToken cancellationToken);

    Task<ResultadoMutacion<RespuestaSesionActual>> EliminarTodasLasFilasAsync(
        int alumnoId, CancellationToken cancellationToken);

    Task<ResultadoMutacion<PlanillaConfirmadaVista>> ConfirmarAsync(
        int alumnoId, CancellationToken cancellationToken);
}

internal sealed class PlanillaMutaciones(CapturasDbContext db) : IPlanillaMutaciones
{
    // ─── Agregar fila ─────────────────────────────────────────────────────────

    public async Task<ResultadoMutacion<RespuestaSesionActual>> AgregarFilaAsync(
        int alumnoId, ComandoNuevaFila comando, CancellationToken cancellationToken)
    {
        var (sesion, cuenta, falla) = await SesionEditableAsync(alumnoId, cancellationToken);
        if (falla is not null || sesion is null)
        {
            return ResultadoMutacion<RespuestaSesionActual>.ConFalla(
                falla ?? PlanillaFalla.SesionInexistente());
        }

        var filasIncompletas = sesion.Registros
            .Where(fila => !ReglaCompletitudSeries.EstaCompleta(fila))
            .ToList();
        if (filasIncompletas.Count > 0)
        {
            return ResultadoMutacion<RespuestaSesionActual>.ConFalla(
                PlanillaFalla.SeriesPendientes(
                    ReglaCompletitudSeries.MensajeParaCompletar(filasIncompletas)));
        }

        var nombreEjercicio = comando.EjercicioNombre?.Trim();
        Ejercicio? ejercicio = null;
        if (comando.EjercicioId is { } ejercicioId)
        {
            ejercicio = await db.Ejercicios.FirstOrDefaultAsync(e => e.Id == ejercicioId, cancellationToken);
            if (ejercicio is null)
            {
                return ResultadoMutacion<RespuestaSesionActual>.ConFalla(PlanillaFalla.EjercicioInexistente(ejercicioId));
            }
            nombreEjercicio = ejercicio.NombreCanonico;
        }
        if (string.IsNullOrWhiteSpace(nombreEjercicio))
        {
            return ResultadoMutacion<RespuestaSesionActual>.ConFalla(
                PlanillaFalla.DatosInvalidos("Escribí el nombre del ejercicio."));
        }

        var valores = ValidarCuerpoFila(
            ejercicio, comando.NroSerie,
            comando.Reps, comando.PesoKg, comando.Unidad, comando.TiempoSeg);
        if (valores.Falla is not null)
        {
            return ResultadoMutacion<RespuestaSesionActual>.ConFalla(valores.Falla);
        }
        if (valores.Reps is null
            || (valores.PesoKg is null && valores.Unidad != Unidad.PesoCorporal))
        {
            return ResultadoMutacion<RespuestaSesionActual>.ConFalla(
                PlanillaFalla.DatosInvalidos(
                    "La fila necesita repeticiones y peso (o marcar «al cuerpo») antes de guardarse."));
        }

        var fila = new RegistroParcial
        {
            Sesion = sesion,
            Ejercicio = ejercicio,
            EjercicioCrudo = ejercicio is null ? nombreEjercicio : null,
            EjercicioCanonico = nombreEjercicio,
            NroSerie = comando.NroSerie,
            Reps = valores.Reps,
            PesoKg = valores.PesoKg,
            Unidad = valores.Unidad,
            TiempoSeg = valores.TiempoSeg,
            CreadoUtc = DateTime.UtcNow,
        };
        sesion.Registros.Add(fila);
        await db.SaveChangesAsync(cancellationToken);

        return ResultadoMutacion<RespuestaSesionActual>.ConVista(
            VistaPlanilla.Armar(alumnoId, cuenta.Nombre, sesion));
    }

    // ─── Editar fila ──────────────────────────────────────────────────────────

    public async Task<ResultadoMutacion<RespuestaSesionActual>> EditarFilaAsync(
        int filaId, ComandoEdicionFila comando, CancellationToken cancellationToken)
    {
        var (fila, fallaFila) = await FilaDeSesionEditableAsync(filaId, cancellationToken);
        if (fila is null)
        {
            return ResultadoMutacion<RespuestaSesionActual>.ConFalla(
                fallaFila ?? PlanillaFalla.FilaInexistente());
        }

        var sesion = fila.Sesion!;

        Ejercicio? ejercicio;
        if (comando.EjercicioId is { } ejercicioId && ejercicioId != fila.EjercicioId)
        {
            // Cambio de ejercicio: con tracking para que el nav quede sano al
            // guardar (el id vinculado se fija solo con la asignación nav).
            ejercicio = await db.Ejercicios
                .FirstOrDefaultAsync(e => e.Id == ejercicioId, cancellationToken);
            if (ejercicio is null)
            {
                return ResultadoMutacion<RespuestaSesionActual>.ConFalla(
                    PlanillaFalla.EjercicioInexistente(ejercicioId));
            }

            fila.Ejercicio = ejercicio;
            fila.EjercicioCanonico = ejercicio.NombreCanonico;
            fila.DescripcionPendiente = false;
        }
        else if (comando.EjercicioId is null)
        {
            // Sin ejercicio: la fila queda como descripción libre (ADR-0004
            // S7) con el texto del alumno, que es obligatorio acá.
            if (string.IsNullOrWhiteSpace(comando.EjercicioCrudo))
            {
                return ResultadoMutacion<RespuestaSesionActual>.ConFalla(PlanillaFalla.DatosInvalidos(
                    "Elegí un ejercicio del catálogo o escribí la descripción de la fila."));
            }

            ejercicio = null;
            fila.Ejercicio = null;
            fila.EjercicioCrudo = comando.EjercicioCrudo.Trim();
            fila.EjercicioCanonico = comando.EjercicioCrudo.Trim();
            fila.DescripcionPendiente = false;
        }
        else
        {
            // Mismo ejercicio que había: no se toca la identidad de la fila.
            ejercicio = fila.Ejercicio;
        }

        var valores = ValidarCuerpoFila(
            ejercicio, comando.NroSerie,
            comando.Reps, comando.PesoKg, comando.Unidad, comando.TiempoSeg);
        if (valores.Falla is not null)
        {
            return ResultadoMutacion<RespuestaSesionActual>.ConFalla(valores.Falla);
        }

        fila.NroSerie = comando.NroSerie;
        fila.Reps = valores.Reps;
        fila.PesoKg = valores.PesoKg;
        fila.Unidad = valores.Unidad;
        fila.TiempoSeg = valores.TiempoSeg;

        // El alumno intervino la fila: la marca de aclaración abandonada del
        // chat se limpia. Si el dato vuelve a faltar, se ve como null en la
        // cuadrícula y el snapshot lo congela como Incompleta.
        fila.PendienteAclaracion = false;

        await db.SaveChangesAsync(cancellationToken);
        var cuenta = sesion.Alumno!; // cargada en el include de la fila
        return ResultadoMutacion<RespuestaSesionActual>.ConVista(
            VistaPlanilla.Armar(cuenta.Id, cuenta.Nombre, sesion));
    }

    // ─── Eliminar fila ────────────────────────────────────────────────────────

    public async Task<ResultadoMutacion<RespuestaSesionActual>> EliminarFilaAsync(
        int filaId, CancellationToken cancellationToken)
    {
        var (fila, fallaFila) = await FilaDeSesionEditableAsync(filaId, cancellationToken);
        if (fila is null)
        {
            return ResultadoMutacion<RespuestaSesionActual>.ConFalla(
                fallaFila ?? PlanillaFalla.FilaInexistente());
        }

        var sesion = fila.Sesion!;
        db.RegistrosParciales.Remove(fila);
        await db.SaveChangesAsync(cancellationToken);
        var cuenta = sesion.Alumno!; // cargada en el include de la fila
        return ResultadoMutacion<RespuestaSesionActual>.ConVista(
            VistaPlanilla.Armar(cuenta.Id, cuenta.Nombre, sesion));
    }

    public async Task<ResultadoMutacion<RespuestaSesionActual>> EliminarTodasLasFilasAsync(
        int alumnoId, CancellationToken cancellationToken)
    {
        var (sesion, cuenta, falla) = await SesionEditableAsync(alumnoId, cancellationToken);
        if (falla is not null || sesion is null)
        {
            return ResultadoMutacion<RespuestaSesionActual>.ConFalla(
                falla ?? PlanillaFalla.SesionInexistente());
        }

        db.RegistrosParciales.RemoveRange(sesion.Registros);
        await db.SaveChangesAsync(cancellationToken);

        return ResultadoMutacion<RespuestaSesionActual>.ConVista(
            VistaPlanilla.Armar(alumnoId, cuenta.Nombre, sesion));
    }

    // ─── Confirmación (snapshot write-once, reglas §2 y ADR-0017) ─────────────

    public async Task<ResultadoMutacion<PlanillaConfirmadaVista>> ConfirmarAsync(
        int alumnoId, CancellationToken cancellationToken)
    {
        var alumno = await db.Alumnos
            .AsNoTracking()
            .Select(a => new { a.Id })
            .FirstOrDefaultAsync(a => a.Id == alumnoId, cancellationToken);
        if (alumno is null)
        {
            return ResultadoMutacion<PlanillaConfirmadaVista>.ConFalla(PlanillaFalla.AlumnoInexistente());
        }

        var sesion = await SesionHablanteConTrackingAsync(alumnoId, cancellationToken);
        if (sesion is null || sesion.Estado is not (EstadoSesionCaptura.EnCurso or EstadoSesionCaptura.Lista))
        {
            return ResultadoMutacion<PlanillaConfirmadaVista>.ConFalla(
                sesion is null
                    ? PlanillaFalla.SinSesionParaConfirmar()
                    : PlanillaFalla.ConfirmacionFueraDeEstado(sesion.Estado));
        }

        // Defensa determinística: en Lista el chat ya abandonó lo pendiente.
        if (sesion.AclaracionPendiente is not null)
        {
            return ResultadoMutacion<PlanillaConfirmadaVista>.ConFalla(
                PlanillaFalla.ConfirmacionConAclaracionViva());
        }

        if (sesion.Registros.Count == 0)
        {
            return ResultadoMutacion<PlanillaConfirmadaVista>.ConFalla(
                PlanillaFalla.ConfirmacionDePlanillaVacia());
        }

        var ahora = DateTime.UtcNow;
        var planilla = new PlanillaConfirmada
        {
            SesionId = sesion.Id,
            AlumnoId = alumnoId,
            ConfirmadaUtc = ahora,
        };

        var orden = 1;
        foreach (var parcial in sesion.Registros.OrderBy(r => r.Id))
        {
            planilla.Filas.Add(new PlanillaFila
            {
                Planilla = planilla,
                Orden = orden++,
                // Nombre congelado (texto, no FK): canónico, o el crudo en filas
                // sin ejercicio — el snapshot muestra lo que existió, sin inventar.
                EjercicioCanonical = parcial.EjercicioCanonico ?? parcial.Ejercicio?.NombreCanonico ?? parcial.EjercicioCrudo ?? string.Empty,
                NroSerie = parcial.NroSerie,
                Reps = parcial.Reps,
                PesoKg = parcial.PesoKg,
                Unidad = parcial.Unidad,
                TiempoSeg = parcial.TiempoSeg,
                // Marca congelada de integridad: fila sin
                // ejercicio identificado (descripción libre) o sin reps/peso.
                Incompleta = parcial.DescripcionPendiente
                    || !ReglaCompletitudSeries.EstaCompleta(parcial),
            });
        }

        sesion.Estado = EstadoSesionCaptura.Confirmada;
        db.PlanillasConfirmadas.Add(planilla);
        await db.SaveChangesAsync(cancellationToken);

        return ResultadoMutacion<PlanillaConfirmadaVista>.ConVista(new PlanillaConfirmadaVista(
            planilla.Id,
            alumnoId,
            sesion.Id,
            ahora,
            [.. planilla.Filas
                .OrderBy(f => f.Orden)
                .Select(f => new FilaConfirmadaVista(
                    f.Orden,
                    f.EjercicioCanonical,
                    f.NroSerie,
                    f.Reps,
                    f.PesoKg,
                    f.Unidad?.ToString(),
                    f.TiempoSeg,
                    f.Incompleta))]));
    }

    // ─── Carga y validaciones compartidas ─────────────────────────────────────

    private async Task<(SesionCaptura? Sesion, AlumnoVista Cuenta, PlanillaFalla? Falla)> SesionEditableAsync(
        int alumnoId, CancellationToken cancellationToken)
    {
        var alumno = await db.Alumnos
            .AsNoTracking()
            .Where(a => a.Id == alumnoId)
            .Select(a => new AlumnoVista(a.Id, a.Nombre))
            .FirstOrDefaultAsync(cancellationToken);
        if (alumno is null)
        {
            return (null, CuentaVacia(), PlanillaFalla.AlumnoInexistente());
        }

        var sesion = await SesionHablanteConTrackingAsync(alumnoId, cancellationToken);
        if (sesion is null)
        {
            return (null, alumno, PlanillaFalla.SesionInexistente());
        }

        var falla = FallaDeEstado(sesion.Estado);
        return (falla is null ? sesion : null, alumno, falla);
    }

    // Cuenta encabezada vacía para la deconstrucción del borde de fallo.
    private static AlumnoVista CuentaVacia() => new(0, string.Empty);

    private async Task<(RegistroParcial? Fila, PlanillaFalla? Falla)> FilaDeSesionEditableAsync(
        int filaId, CancellationToken cancellationToken)
    {
        var fila = await db.RegistrosParciales
            .Include(r => r.Ejercicio)
            .Include(r => r.Sesion).ThenInclude(s => s!.Alumno)
            .Include(r => r.Sesion).ThenInclude(s => s!.EjercicioActivo)
            .Include(r => r.Sesion).ThenInclude(s => s!.AclaracionPendiente)
            .Include(r => r.Sesion).ThenInclude(s => s!.Registros).ThenInclude(rr => rr.Ejercicio)
            .FirstOrDefaultAsync(r => r.Id == filaId, cancellationToken);
        if (fila is null)
        {
            return (null, null);
        }

        // La fila solo es editable si su sesión (que con un solo diálogo activo
        // por alumno es la hablante, ADR-0004 S1) lo permite hoy.
        var falla = FallaDeEstado(fila.Sesion!.Estado);
        return falla is null ? (fila, null) : (null, falla);
    }

    private static PlanillaFalla? FallaDeEstado(EstadoSesionCaptura estado) => estado switch
    {
        EstadoSesionCaptura.EsperandoAclaracion => PlanillaFalla.SesionSiendoAclarada(),
        EstadoSesionCaptura.Expirada => PlanillaFalla.SesionExpirada(),
        // La hablante nunca es Confirmada; si se llega, el diálogo está cerrado.
        EstadoSesionCaptura.Confirmada => PlanillaFalla.SesionInexistente(),
        _ => null, // EnCurso y Lista: cuadrícula editable.
    };

    // Sesión hablante CON tracking (van a mutarse sus filas o su estado).
    private Task<SesionCaptura?> SesionHablanteConTrackingAsync(int alumnoId, CancellationToken cancellationToken) =>
        db.SesionesCaptura
            .Include(s => s.EjercicioActivo)
            .Include(s => s.AclaracionPendiente)
            .Include(s => s.Registros).ThenInclude(r => r.Ejercicio)
            .Where(s => s.AlumnoId == alumnoId && s.Estado != EstadoSesionCaptura.Confirmada)
            .OrderByDescending(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);

    // Valida el cuerpo de la fila y aplica las MISMAS normalizaciones del chat
    // (ADR-0004 S6: planilla 100 % en kg; unidad ausente con peso → kg). Si
    // Falla != null, NADA se persiste.
    private static (int? Reps, decimal? PesoKg, Unidad? Unidad, int? TiempoSeg, PlanillaFalla? Falla) ValidarCuerpoFila(
        Ejercicio? ejercicio,
        int nroSerie,
        int? reps,
        decimal? pesoKg,
        string? unidadTexto,
        int? tiempoSeg)
    {
        var detalles = new List<string>();

        var pesoFin = pesoKg;
        var unidadFin = default(Unidad?);

        if (unidadTexto is { Length: > 0 })
        {
            if (Enum.TryParse<Unidad>(unidadTexto, ignoreCase: true, out var unidad))
            {
                (pesoFin, unidadFin) = SesionCapturaServicio.NormalizarValores(pesoKg, unidad);
            }
            else
            {
                detalles.Add($"Unidad inválida: \"{unidadTexto}\". Valores: Kg, Lb, PesoCorporal o Tiempo.");
            }
        }
        else
        {
            // Unidad ausente y peso presente → kg (mismo criterio del chat).
            (pesoFin, unidadFin) = SesionCapturaServicio.NormalizarValores(pesoKg, null);
        }

        // Igual que el chat (ADR-0004): fila por tiempo sin unidad explícita
        // queda etiquetada como Unidad.Tiempo (etiqueta de tipo, no dato).
        if (tiempoSeg is not null && unidadFin is null)
        {
            unidadFin = Unidad.Tiempo;
        }

        if (nroSerie < 1)
        {
            detalles.Add("El número de serie va de 1 en adelante.");
        }

        if (reps is < 1)
        {
            detalles.Add("Las repeticiones van de 1 en adelante.");
        }

        if (pesoFin is < 0)
        {
            detalles.Add("El peso no puede ser negativo.");
        }

        if (tiempoSeg is < 1)
        {
            detalles.Add("El tiempo va de 1 segundo en adelante.");
        }

        if (ejercicio is not null)
        {
            if (ejercicio.UsaTiempo && pesoFin is not null)
            {
                detalles.Add("El ejercicio es por tiempo: no lleva peso.");
            }

            if (!ejercicio.UsaTiempo && tiempoSeg is not null)
            {
                detalles.Add("El ejercicio no es por tiempo: no lleva tiempo en segundos.");
            }

            if (!ejercicio.UsaPeso && pesoFin is not null)
            {
                detalles.Add("El ejercicio no lleva peso externo.");
            }
        }

        if (detalles.Count > 0)
        {
            return (reps, pesoFin, unidadFin, tiempoSeg, PlanillaFalla.DatosInvalidos(string.Join(' ', detalles)));
        }

        return (reps, pesoFin, unidadFin, tiempoSeg, null);
    }

    private static string FraseFaltante(string campo) => campo switch
    {
        "peso" => "La fila necesita el peso (o \"al cuerpo\" si es peso corporal).",
        "reps" => "La fila necesita las repeticiones.",
        _ => $"La fila necesita {campo}.",
    };
}
