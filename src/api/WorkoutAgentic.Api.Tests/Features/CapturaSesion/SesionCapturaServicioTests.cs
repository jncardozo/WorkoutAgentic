using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WorkoutAgentic.Api.Data;
using WorkoutAgentic.Api.Data.Entities;
using WorkoutAgentic.Api.Features.CapturaSesion;
using WorkoutAgentic.Api.Features.Nlu;

namespace WorkoutAgentic.Api.Tests.Features.CapturaSesion;

// Máquina de estados del diálogo de captura sobre SQLite temporal con el
// esquema REAL por migraciones y el catálogo sembrado. Los análisis son
// sintéticos (el slice NLU ya los cubre): no se llama a proveedores externos.
// Regla del dictado vigente (ADR-0013): EJERCICIO + SERIES ⇒ filas con solo
// ejercicio y número de serie; lo que el alumno diga encima de eso se ignora;
// fuera de la regla → guía única, sin tocar el estado ni el ejercicio activo.
public class SesionCapturaServicioTests : IDisposable
{

    private static readonly DateTime Ahora = new(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc);

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"wa-sesion-{Guid.NewGuid():N}.db");
    private readonly CapturasDbContext _db;
    private readonly SesionCapturaServicio _servicio;
    private readonly int _alumnoId;

    public SesionCapturaServicioTests()
    {
        _db = new CapturasDbContext(
            new DbContextOptionsBuilder<CapturasDbContext>()
                .UseSqlite($"Data Source={_dbPath};Cache=Shared")
                .Options);

        _db.Database.Migrate();
        var alumno = new Alumno { Nombre = "Alumno de prueba", Estado = "ACTIVO" };
        _db.Alumnos.Add(alumno);
        _db.Ejercicios.AddRange(CatalogoEjercicioSeed.Crear());
        _db.SaveChanges();
        _alumnoId = alumno.Id;

        _servicio = new SesionCapturaServicio(
            _db,
            NullLogger<SesionCapturaServicio>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        BorrarBase();
    }

    // ─── Flujo completo: lote base → continuación → cierre ──────────────────────

    [Fact]
    public async Task PrimerContacto_LoteBase_CreaFilasConSoloEjercicioYSeries()
    {
        var respuesta = await AplicarAltaBase();

        Assert.Equal(1, await _db.Alumnos.CountAsync());
        var sesion = await _db.SesionesCaptura.SingleAsync();
        Assert.Equal(EstadoSesionCaptura.EnCurso, sesion.Estado);
        Assert.Equal(1, sesion.EjercicioActivoId);
        Assert.Equal(5, sesion.OrdenSerieSiguiente);
        Assert.Equal(respuesta.SesionId, sesion.Id);

        var filas = await _db.RegistrosParciales.OrderBy(f => f.NroSerie).ToListAsync();
        Assert.Equal(4, filas.Count);
        Assert.Equal(new[] { 1, 2, 3, 4 }, filas.Select(f => f.NroSerie));
        Assert.All(filas, f =>
        {
            Assert.Equal(1, f.EjercicioId);
            Assert.Equal("sillón", f.EjercicioCrudo);
            Assert.Null(f.Reps);
            Assert.Null(f.PesoKg);
            Assert.Null(f.Unidad);
            Assert.Null(f.TiempoSeg);
            Assert.False(f.PendienteAclaracion);
            Assert.False(f.DescripcionPendiente);
        });
        Assert.Equal(0, await _db.AclaracionesPendientes.CountAsync());
        Assert.Contains("Anotadas 4 series de Sillón de cuadríceps (1 a 4)", respuesta.MensajeAlumno);
        Assert.Contains("pasame el dato de cada serie", respuesta.MensajeAlumno);
    }

    [Fact]
    public async Task LoteRepetido_ContinuaLaNumeracionDelEjercicio()
    {
        await AplicarAltaBase();
        Refrescar();

        var respuesta = await Aplicar(AnalisisCargaEjercicio(
            "otras 3 del sillón", "sillón", Match(1, "Sillón de cuadríceps"), cantidad: 3));

        var filas = await _db.RegistrosParciales.OrderBy(f => f.NroSerie).ToListAsync();
        Assert.Equal(7, filas.Count);
        Assert.Equal(new[] { 5, 6, 7 }, filas.Skip(4).Select(f => f.NroSerie).ToArray());
        Assert.All(filas, f => Assert.Null(f.PesoKg));
        Assert.Equal(8, (await _db.SesionesCaptura.SingleAsync()).OrdenSerieSiguiente);
        Assert.Contains("Anotadas 3 series de Sillón de cuadríceps (5 a 7)", respuesta.MensajeAlumno);
    }

    [Fact]
    public async Task SeriePuntual_CreaUnaFilaConEseOrdinal()
    {
        var respuesta = await Aplicar(AnalisisCargaEjercicio(
            "sillón, tercera serie", "sillón", Match(1, "Sillón de cuadríceps"), serie: 3));

        var fila = await _db.RegistrosParciales.SingleAsync();
        Assert.Equal(3, fila.NroSerie);
        Assert.Equal(1, fila.EjercicioId);
        Assert.Equal("sillón", fila.EjercicioCrudo);
        Assert.Null(fila.Reps);
        Assert.Null(fila.PesoKg);
        Assert.Equal(4, (await _db.SesionesCaptura.SingleAsync()).OrdenSerieSiguiente);
        Assert.Contains("Serie 3 de Sillón de cuadríceps anotada", respuesta.MensajeAlumno);
    }

    // ─── Dictado fuera de la regla: guía única, sin tocar nada ──────────────────

    [Fact]
    public async Task SeriesSinEjercicio_FueraDeRegla_NoCreaNadaYEnseniaLaRegla()
    {
        var respuesta = await Aplicar(AnalisisCargaSerie(
            "3x12 con 60", ejercicio: null, reps: 12, pesoKg: 40m, unidad: Unidad.Kg, cantidad: 3));

        Assert.Empty(await _db.RegistrosParciales.ToListAsync());
        Assert.Equal(0, await _db.AclaracionesPendientes.CountAsync());
        Assert.Null((await _db.SesionesCaptura.SingleAsync()).EjercicioActivoId);
        Assert.Equal(TextosBot.FueraDeRegla, respuesta.MensajeAlumno);
    }

    [Fact]
    public async Task EjercicioSinSeries_FueraDeRegla_NoCreaNadaYEnseniaLaRegla()
    {
        var respuesta = await Aplicar(AnalisisCargaEjercicio(
            "press de banca", "press", Match(3, "Press de banca")));

        Assert.Empty(await _db.RegistrosParciales.ToListAsync());
        Assert.Equal(0, await _db.AclaracionesPendientes.CountAsync());
        Assert.Null((await _db.SesionesCaptura.SingleAsync()).EjercicioActivoId);
        Assert.Equal(TextosBot.FueraDeRegla, respuesta.MensajeAlumno);
    }

    [Fact]
    public async Task DatosExtraDelDictado_SeIgnoran_NoSeGuardan()
    {
        var respuesta = await Aplicar(AnalisisCargaEjercicio(
            "sillón 4 series y 12 repeticiones con 45 kg",
            "sillón", Match(1, "Sillón de cuadríceps"),
            reps: 12, pesoKg: 45m, unidad: Unidad.Kg, tiempoSeg: 60, cantidad: 4));

        var filas = await _db.RegistrosParciales.ToListAsync();
        Assert.Equal(4, filas.Count);
        Assert.All(filas, f =>
        {
            Assert.Null(f.Reps);
            Assert.Null(f.PesoKg);
            Assert.Null(f.Unidad);
            Assert.Null(f.TiempoSeg);
            Assert.False(f.PendienteAclaracion);
        });
        Assert.Equal(0, await _db.AclaracionesPendientes.CountAsync());
        Assert.DoesNotContain("45", respuesta.MensajeAlumno);
        Assert.Contains("pasame el dato de cada serie", respuesta.MensajeAlumno);
    }

    [Fact]
    public async Task CantidadAbsurda_FueraDeRegla_NoCreaFilas()
    {
        var respuesta = await Aplicar(AnalisisCargaEjercicio(
            "press de banca 999 series", "press", Match(3, "Press de banca"),
            cantidad: SesionCapturaServicio.MaxSeriesLote + 1));

        Assert.Empty(await _db.RegistrosParciales.ToListAsync());
        Assert.Equal(TextosBot.FueraDeRegla, respuesta.MensajeAlumno);
    }

    // ─── Regla 2: datos de serie sin ejercicio nombrado (ADR-0014) ───────────────

    [Fact]
    public async Task DatosDeSerie_EnRegla_LlenanLaFilaDelActivo()
    {
        await AplicarAltaBase();
        Refrescar();

        var respuesta = await Aplicar(AnalisisCargaSerie(
            "primera serie 12 repeticiones con 20 kilos", ejercicio: null,
            reps: 12, pesoKg: 20m, serie: 1));

        var fila = await _db.RegistrosParciales.OrderBy(f => f.Id).FirstAsync();
        Assert.Equal(12, fila.Reps);
        Assert.Equal(20m, fila.PesoKg);
        Assert.Equal(Unidad.Kg, fila.Unidad);
        Assert.Null(fila.TiempoSeg);
        Assert.False(fila.PendienteAclaracion);

        var sesion = await _db.SesionesCaptura.SingleAsync();
        Assert.Equal(EstadoSesionCaptura.EnCurso, sesion.Estado);
        Assert.Equal(5, sesion.OrdenSerieSiguiente); // el dato no mueve la numeración
        Assert.Equal(4, await _db.RegistrosParciales.CountAsync()); // nada de filas nuevas
        Assert.Contains("Serie 1 de Sillón de cuadríceps anotada: 12 reps con 20 kg", respuesta.MensajeAlumno);
    }

    [Fact]
    public async Task DatosDeSerie_SoloPesoOSoloReps_EscribenSoloLoDichado()
    {
        await AplicarAltaBase();
        Refrescar();

        await Aplicar(AnalisisCargaSerie("segunda serie con 30", ejercicio: null, pesoKg: 30m, serie: 2));
        Refrescar();
        var respuestaSoloReps = await Aplicar(AnalisisCargaSerie(
            "tercera serie 15", ejercicio: null, reps: 15, serie: 3));

        var filas = await _db.RegistrosParciales.OrderBy(f => f.Id).ToListAsync();
        Assert.Multiple(
            () => Assert.Equal(30m, filas[1].PesoKg),
            () => Assert.Null(filas[1].Reps),
            () => Assert.Equal(Unidad.Kg, filas[1].Unidad), // "50" suelto → kg (spec §2)
            () => Assert.Equal(15, filas[2].Reps),
            () => Assert.Null(filas[2].PesoKg),
            () => Assert.Null(filas[2].Unidad));
        Assert.Contains("Serie 3 de Sillón de cuadríceps anotada: 15 reps", respuestaSoloReps.MensajeAlumno);
    }

    [Fact]
    public async Task DatosDeSerie_Repetido_GanaElUltimoDictado()
    {
        await AplicarAltaBase();
        Refrescar();

        await Aplicar(AnalisisCargaSerie("primera serie 12 con 20", ejercicio: null, reps: 12, pesoKg: 20m, serie: 1));
        Refrescar();
        var respuesta = await Aplicar(AnalisisCargaSerie(
            "primera serie 10 repeticiones con 25 kilos", ejercicio: null, reps: 10, pesoKg: 25m, serie: 1));

        var fila = await _db.RegistrosParciales.OrderBy(f => f.Id).FirstAsync();
        Assert.Equal(10, fila.Reps);
        Assert.Equal(25m, fila.PesoKg);
        Assert.Equal(4, await _db.RegistrosParciales.CountAsync()); // sigue habiendo 4 filas
        Assert.Contains("10 reps con 25 kg", respuesta.MensajeAlumno);
    }

    [Fact]
    public async Task DatosDeSerie_SerieInexistente_AvisaSinCrearNada()
    {
        await AplicarAltaBase();
        Refrescar();

        var respuesta = await Aplicar(AnalisisCargaSerie(
            "sexta serie 10 con 40", ejercicio: null, reps: 10, pesoKg: 40m, serie: 6));

        Assert.Equal(4, await _db.RegistrosParciales.CountAsync()); // no se inventa la fila
        Assert.All(await _db.RegistrosParciales.ToListAsync(), f => Assert.Null(f.Reps));
        Assert.Contains("No veo la serie 6 de Sillón de cuadríceps", respuesta.MensajeAlumno);
    }

    [Fact]
    public async Task DatosDeSerie_SinEjercicioActivo_GuiaSinFilas()
    {
        // Sin lote previo: el dato no puede apoyarse en nada (no se adivina).
        var respuesta = await Aplicar(AnalisisCargaSerie(
            "primera serie 12 con 20", ejercicio: null, reps: 12, pesoKg: 20m, serie: 1));

        Assert.Empty(await _db.RegistrosParciales.ToListAsync());
        Assert.Equal(TextosBot.FueraDeRegla, respuesta.MensajeAlumno);
    }

    [Fact]
    public async Task DatosDeSerie_SinOrdinalOSinDato_FueraDeRegla()
    {
        await AplicarAltaBase();
        Refrescar();

        var sinOrdinal = await Aplicar(AnalisisCargaSerie(
            "12 repeticiones con 20", ejercicio: null, reps: 12, pesoKg: 20m));
        var sinDato = await Aplicar(AnalisisCargaSerie("quinta serie", ejercicio: null, serie: 5));

        Assert.All(await _db.RegistrosParciales.ToListAsync(), f => Assert.Null(f.Reps));
        Assert.Equal(TextosBot.FueraDeRegla, sinOrdinal.MensajeAlumno);
        Assert.Equal(TextosBot.FueraDeRegla, sinDato.MensajeAlumno);
    }

    [Fact]
    public async Task DatosDeSerie_EnLibras_SeConviertenAKg()
    {
        await AplicarAltaBase();
        Refrescar();

        var respuesta = await Aplicar(AnalisisCargaSerie(
            "segunda serie 10 con 40 en libras", ejercicio: null,
            reps: 10, pesoKg: 40m, unidad: Unidad.Lb, serie: 2));

        var fila = await _db.RegistrosParciales.OrderBy(f => f.Id).Skip(1).FirstAsync();
        Assert.Equal(10, fila.Reps);
        Assert.Equal(18.14m, fila.PesoKg); // 40 lb × 0,45359237 = 18,1436948 → 18,14
        Assert.Equal(Unidad.Kg, fila.Unidad);
        Assert.Contains("10 reps con 18.14 kg", respuesta.MensajeAlumno);
    }

    [Fact]
    public async Task DatosDeSerie_AlCuerpo_FijaPesoCorporalSinPeso()
    {
        await AplicarAltaBase();
        Refrescar();

        var respuesta = await Aplicar(AnalisisCargaSerie(
            "tercera serie 10 al cuerpo", ejercicio: null, reps: 10, unidad: Unidad.PesoCorporal, serie: 3));

        var fila = await _db.RegistrosParciales.OrderBy(f => f.Id).Skip(2).FirstAsync();
        Assert.Equal(10, fila.Reps);
        Assert.Null(fila.PesoKg);
        Assert.Equal(Unidad.PesoCorporal, fila.Unidad);
        Assert.Contains("10 reps al cuerpo", respuesta.MensajeAlumno);
    }

    [Fact]
    public async Task DatosDeSerie_MientrasHayAclaracionViva_SeAplicaAlLoteVivoYSigueLaPregunta()
    {
        await AbrirPendienteDeEjercicio(); // lote vivo: 4 filas de "el pecho" pendientes
        Refrescar();

        var respuesta = await Aplicar(AnalisisCargaSerie(
            "primera serie 12 con 20", ejercicio: null, reps: 12, pesoKg: 20m, serie: 1));

        var filas = await _db.RegistrosParciales.OrderBy(f => f.Id).ToListAsync();
        Assert.Equal(8, filas.Count);

        // El dato entra en la serie 1 del LOTE VIVO (el lote del pecho), no en
        // el ejercicio activo heredado del alta base.
        Assert.Equal(12, filas[4].Reps);
        Assert.Equal(20m, filas[4].PesoKg);
        Assert.Null(filas[4].EjercicioId); // el ejercicio aún no se resolvó
        Assert.False(filas[4].PendienteAclaracion); // con dato deja de esperar al bot
        for (var i = 0; i < 4; i++)
        {
            Assert.Null(filas[i].Reps); // las series del lote viejo no se tocan
        }

        // La pregunta del ejercicio sigue viva, sin reintentos quemados.
        var aclaracion = await _db.AclaracionesPendientes.SingleAsync();
        Assert.Equal(0, aclaracion.Reintento);
        Assert.Equal(EstadoSesionCaptura.EsperandoAclaracion, (await _db.SesionesCaptura.SingleAsync()).Estado);
        Assert.Contains("Serie 1", respuesta.MensajeAlumno);
        Assert.Contains("me sigue faltando el ejercicio", respuesta.MensajeAlumno);
    }

    [Fact]
    public async Task DatosDeSerie_TextoLibre_SeAplicaAlLoteDelCrudo()
    {
        await Aplicar(AnalisisCargaEjercicio(
            "movimiento raro 3 series", "movimiento raro", SinMatch(), cantidad: 3), Ahora.AddMinutes(1));
        await Aplicar(AnalisisLibre("otro"));
        Refrescar();

        var respuesta = await Aplicar(AnalisisCargaSerie(
            "segunda serie 10 con 40", ejercicio: null, reps: 10, pesoKg: 40m, serie: 2));

        var filas = await _db.RegistrosParciales.OrderBy(f => f.Id).ToListAsync();
        Assert.Equal(10, filas[1].Reps);
        Assert.Equal(40m, filas[1].PesoKg);
        Assert.Null(filas[1].EjercicioId); // lote de texto libre (resolver = otro)
        Assert.Equal("movimiento raro", filas[1].EjercicioCrudo);
        Assert.False(filas[1].PendienteAclaracion);
        // El «Estás cargando» del proveedor del dato: el nombre del lote vivo.
        Assert.Contains("movimiento raro", respuesta.MensajeAlumno);
    }

    [Fact]
    public async Task DatosDeSerie_DespuesDeOtroLote_TargeteanLaUltimaFilaCreada()
    {
        await AplicarAltaBase();

        // Segundo lote RESUELTO directo (Match) de otro ejercicio del catálogo.
        await Aplicar(AnalisisCargaEjercicio(
            "press de pecho 3 series", "press de pecho",
            Match(3, "Press de pecho en máquina"), cantidad: 3), Ahora.AddMinutes(1));
        Refrescar();

        var respuesta = await Aplicar(AnalisisCargaSerie(
            "segunda serie 5 con 10", ejercicio: null, reps: 5, pesoKg: 10m, serie: 2));

        var filas = await _db.RegistrosParciales.OrderBy(f => f.Id).ToListAsync();
        var press = filas.Skip(4).ToList();
        Assert.Equal(5, press[1].Reps);
        Assert.Equal(10m, press[1].PesoKg);
        Assert.Equal(3, press[1].EjercicioId); // Press de pecho en máquina

        Assert.Null(filas[1].Reps); // la serie 2 del lote VIEJO (sillón) queda intacta
        Assert.Null(filas[1].PesoKg);
        Assert.Contains("Press de pecho en máquina", respuesta.MensajeAlumno);
    }

    [Fact]
    public async Task AnalisisFallido_ConPreguntaViva_NoCuentaReintentoNiAbandona()
    {
        await AbrirPendienteDeEjercicio();
        Refrescar();

        // El LLM falló (cuota 429, red, salida no parseable): análisis con
        // Exito=false y sin entidades — igual al fallback del pipeline real.
        var caido = new AnalisisCaptura(
            "primera serie 12 con 20",
            ClasificacionIntent.Fallo("429 RESOURCE_EXHAUSTED"), null, null);
        var respuesta = await Aplicar(caido);

        var aclaracion = await _db.AclaracionesPendientes.SingleAsync();
        Assert.Equal(0, aclaracion.Reintento); // una falla del LLM NO es una respuesta errónea
        Assert.Equal(EstadoSesionCaptura.EsperandoAclaracion, (await _db.SesionesCaptura.SingleAsync()).Estado);
        Assert.All(await _db.RegistrosParciales.ToListAsync(), f => Assert.Null(f.Reps));
        Assert.Equal(TextosBot.FalloLectura, respuesta.MensajeAlumno);
    }

    [Fact]
    public async Task AnalisisFallido_SinPregunta_NoTocaNadaYPideReenvio()
    {
        await AplicarAltaBase();
        Refrescar();

        var caido = new AnalisisCaptura(
            "primera serie 12 con 20",
            ClasificacionIntent.Fallo("429 RESOURCE_EXHAUSTED"), null, null);
        var respuesta = await Aplicar(caido);

        var sesion = await _db.SesionesCaptura.SingleAsync();
        Assert.Equal(EstadoSesionCaptura.EnCurso, sesion.Estado);
        Assert.All(await _db.RegistrosParciales.ToListAsync(), f => Assert.Null(f.Reps));
        Assert.Equal(0, await _db.AclaracionesPendientes.CountAsync());
        Assert.Equal(TextosBot.FalloLectura, respuesta.MensajeAlumno);
    }

    // ─── Aclaración de ejercicio (la única aclaración viva) ─────────────────────

    [Fact]
    public async Task EjercicioAmbiguo_LasFilasEsperanLaEleccionYPuedenCompletarElLote()
    {
        var respuesta = await Aplicar(AnalisisCargaEjercicio(
            "el pecho 4 series", "el pecho",
            Ambiguo((3, "Press de pecho en máquina"), (4, "Cruces en polea")),
            cantidad: 4));

        var filas = await _db.RegistrosParciales.OrderBy(f => f.NroSerie).ToListAsync();
        Assert.Equal(4, filas.Count);
        Assert.Equal(new[] { 1, 2, 3, 4 }, filas.Select(f => f.NroSerie));
        Assert.All(filas, f =>
        {
            Assert.Null(f.EjercicioId);
            Assert.Equal("el pecho", f.EjercicioCrudo);
            Assert.True(f.PendienteAclaracion);
            Assert.False(f.DescripcionPendiente);
        });

        var aclaracion = await _db.AclaracionesPendientes.SingleAsync();
        Assert.Equal("ejercicio", aclaracion.Campo);
        Assert.Equal(filas.Select(f => f.Id), ExtraerRegistroIds(aclaracion.Contexto));
        Assert.Equal(EstadoSesionCaptura.EsperandoAclaracion, (await _db.SesionesCaptura.SingleAsync()).Estado);
        Assert.True(respuesta.AclaracionCreada);
        Assert.Contains("1) Press de pecho en máquina", respuesta.MensajeAlumno);

        Refrescar();
        var eleccion = await Aplicar(AnalisisLibre("2"));

        var sesion = await _db.SesionesCaptura.SingleAsync();
        Assert.Equal(EstadoSesionCaptura.EnCurso, sesion.Estado);
        Assert.Equal(4, sesion.EjercicioActivoId);
        Assert.Equal(5, sesion.OrdenSerieSiguiente);
        filas = await _db.RegistrosParciales.OrderBy(f => f.Id).ToListAsync();
        Assert.All(filas, f =>
        {
            Assert.Equal(4, f.EjercicioId);
            Assert.Equal("el pecho", f.EjercicioCrudo); // el texto del alumno queda para auditar
            Assert.False(f.PendienteAclaracion);
            Assert.Null(f.Reps);
            Assert.Null(f.PesoKg);
        });
        Assert.Equal(0, await _db.AclaracionesPendientes.CountAsync());
        Assert.Contains("Anotadas 4 series de Cruces en polea (1 a 4)", eleccion.MensajeAlumno);
    }

    [Fact]
    public async Task EjercicioSinMatch_OtroDejaLasFilasComoTextoLibre()
    {
        await Aplicar(AnalisisCargaEjercicio(
            "movimiento raro 3 series", "movimiento raro", SinMatch(), cantidad: 3));

        Assert.Equal(EstadoSesionCaptura.EsperandoAclaracion, (await _db.SesionesCaptura.SingleAsync()).Estado);
        var aclaracion = await _db.AclaracionesPendientes.SingleAsync();
        Assert.Equal("ejercicio", aclaracion.Campo);
        Assert.Equal(3, ExtraerRegistroIds(aclaracion.Contexto).Length);

        Refrescar();
        var segundo = await Aplicar(AnalisisLibre("otro"));

        var sesion = await _db.SesionesCaptura.SingleAsync();
        Assert.Equal(EstadoSesionCaptura.EnCurso, sesion.Estado);
        Assert.Null(sesion.EjercicioActivoId);
        Assert.Equal(0, await _db.AclaracionesPendientes.CountAsync());
        var filas = await _db.RegistrosParciales.ToListAsync();
        Assert.Equal(3, filas.Count);
        Assert.All(filas, f =>
        {
            Assert.Null(f.EjercicioId);
            Assert.Equal("movimiento raro", f.EjercicioCrudo);
            Assert.True(f.DescripcionPendiente);
            Assert.False(f.PendienteAclaracion);
        });
        Assert.Contains("Anotado como texto", segundo.MensajeAlumno);
    }

    [Fact]
    public async Task ReintentoDePregunta_RePreguntaConLaMismaLista()
    {
        await Aplicar(AnalisisCargaEjercicio(
            "el pecho 4 series", "el pecho",
            Ambiguo((3, "Press de pecho en máquina"), (4, "Cruces en polea")), cantidad: 4));
        Refrescar();

        var reintento = await Aplicar(AnalisisLibre("si jeje"));

        Assert.Contains("1) Press de pecho en máquina", reintento.MensajeAlumno);
        var aclaracion = await _db.AclaracionesPendientes.SingleAsync();
        Assert.Equal(1, aclaracion.Reintento);
    }

    [Fact]
    public async Task DosRespuestasIncomprensibles_AbandonanYElFlujoSigue()
    {
        await AbrirPendienteDeEjercicio();
        Refrescar();

        await Aplicar(AnalisisLibre("no sé"));
        Refrescar();
        var aclaracion = await _db.AclaracionesPendientes.SingleAsync();
        Assert.Equal(1, aclaracion.Reintento);

        var segunda = await Aplicar(AnalisisLibre("sigue sin saberlo"));

        Assert.Equal(0, await _db.AclaracionesPendientes.CountAsync());

        var filasPendientes = await _db.RegistrosParciales.Where(f => f.EjercicioId == null).ToListAsync();
        Assert.All(filasPendientes, f =>
        {
            Assert.True(f.DescripcionPendiente); // queda marcada para la planilla
            Assert.True(f.PendienteAclaracion);  // la marca sobrevive: la limpia la edición de la SPA
        });

        var sesion = await _db.SesionesCaptura.SingleAsync();
        Assert.Equal(EstadoSesionCaptura.EnCurso, sesion.Estado);
        Assert.Contains("marcada", segunda.MensajeAlumno);
    }

    // ─── Cierre y reapertura (ADR-0012 + regla ADR-0013) ────────────────────────

    [Fact]
    public async Task Cierre_DejaLaSesionLista_EInvitaAConfirmar()
    {
        await AplicarAltaBase();
        Refrescar();

        var cierre = await Aplicar(AnalisisTexto("terminé", IntentoCaptura.Cierre));

        var sesion = await _db.SesionesCaptura.SingleAsync();
        Assert.Equal(EstadoSesionCaptura.Lista, sesion.Estado);
        Assert.Contains("planilla", cierre.MensajeAlumno);

        // Un mensaje sin carga (ayuda/cortesía) solo recuerda el paso por la
        // SPA; un dictado EN REGLA la reabriría (tests propios más abajo).
        var despues = await Aplicar(AnalisisTexto("me olvidaba algo", IntentoCaptura.Invalido));
        Assert.Contains("ya está armada", despues.MensajeAlumno);
        Assert.Equal(EstadoSesionCaptura.Lista, (await _db.SesionesCaptura.SingleAsync()).Estado);
    }

    [Fact]
    public async Task CierreConPreguntaViva_AbandonaElEjercicioYCierra()
    {
        await AbrirPendienteDeEjercicio();
        Refrescar();

        var cierre = await Aplicar(AnalisisTexto("terminé", IntentoCaptura.Cierre));

        var sesion = await _db.SesionesCaptura.SingleAsync();
        Assert.Equal(EstadoSesionCaptura.Lista, sesion.Estado);
        Assert.Equal(0, await _db.AclaracionesPendientes.CountAsync());
        var filasPendientes = await _db.RegistrosParciales.Where(f => f.EjercicioId == null).ToListAsync();
        Assert.All(filasPendientes, f => Assert.True(f.DescripcionPendiente)); // las filas quedan, marcadas
        Assert.Contains("planilla", cierre.MensajeAlumno);
    }

    [Fact]
    public async Task LoteBaseConSesionLista_ReabreYAplica()
    {
        await AplicarAltaBase();
        Refrescar();
        await Aplicar(AnalisisTexto("terminé", IntentoCaptura.Cierre));
        Refrescar();

        var respuesta = await Aplicar(AnalisisCargaEjercicio(
            "dominadas 4 series", "dominadas", Match(9, "Dominadas"), cantidad: 4));

        var sesion = await _db.SesionesCaptura.SingleAsync();
        Assert.Equal(EstadoSesionCaptura.EnCurso, sesion.Estado);
        Assert.Equal(9, sesion.EjercicioActivoId);
        Assert.Equal(5, sesion.OrdenSerieSiguiente);

        var nuevas = await _db.RegistrosParciales.Where(f => f.EjercicioId == 9).OrderBy(f => f.NroSerie).ToListAsync();
        Assert.Equal(new[] { 1, 2, 3, 4 }, nuevas.Select(f => f.NroSerie));
        Assert.All(nuevas, f =>
        {
            Assert.Equal("dominadas", f.EjercicioCrudo);
            Assert.Null(f.PesoKg);
            Assert.Null(f.Reps);
        });
        Assert.Contains("Anotadas 4 series de Dominadas (1 a 4)", respuesta.MensajeAlumno);
    }

    [Fact]
    public async Task DictadoFueraDeReglaConSesionLista_NoLaReabre()
    {
        await AplicarAltaBase();
        Refrescar();
        await Aplicar(AnalisisTexto("terminé", IntentoCaptura.Cierre));
        Refrescar();

        var respuesta = await Aplicar(AnalisisCargaSerie(
            "3x12 con 60", ejercicio: null, reps: 12, pesoKg: 40m, unidad: Unidad.Kg, cantidad: 3));

        Assert.Equal(EstadoSesionCaptura.Lista, (await _db.SesionesCaptura.SingleAsync()).Estado);
        Assert.Equal(TextosBot.FueraDeRegla, respuesta.MensajeAlumno);
    }

    [Fact]
    public async Task SeguirConSesionLista_ReabreSinTocarFilas()
    {
        await AplicarAltaBase();
        Refrescar();
        await Aplicar(AnalisisTexto("terminé", IntentoCaptura.Cierre));
        Refrescar();

        var respuesta = await Aplicar(AnalisisLibre("seguir"));

        var sesion = await _db.SesionesCaptura.SingleAsync();
        Assert.Equal(EstadoSesionCaptura.EnCurso, sesion.Estado);
        Assert.Equal(4, await _db.RegistrosParciales.CountAsync()); // las filas del lote quedan
        Assert.Equal(0, await _db.AclaracionesPendientes.CountAsync());
        Assert.Contains("seguimos", respuesta.MensajeAlumno.ToLowerInvariant());
    }

    [Fact]
    public async Task AyudaConSesionLista_SigueInvitandoAConfirmar()
    {
        await AplicarAltaBase();
        Refrescar();
        await Aplicar(AnalisisTexto("terminé", IntentoCaptura.Cierre));
        Refrescar();

        var ayuda = await Aplicar(AnalisisTexto("¿cómo hago el remo?", IntentoCaptura.AyudaConsulta));

        var sesion = await _db.SesionesCaptura.SingleAsync();
        Assert.Equal(EstadoSesionCaptura.Lista, sesion.Estado);
        Assert.Contains("ya está armada", ayuda.MensajeAlumno);
    }

    // ─── Pausa por inactividad ─────────────────────────────────────────────────

    [Fact]
    public async Task Inactividad_PausaYSeReanudaConSeguir()
    {
        await AplicarAltaBase();
        Refrescar();

        var pausa = await Aplicar(AnalisisLibre("12 con 45"), Ahora.AddHours(3));

        Assert.Equal(EstadoSesionCaptura.Expirada, (await _db.SesionesCaptura.SingleAsync()).Estado);
        Assert.Contains("paus", pausa.MensajeAlumno.ToLowerInvariant());

        var reanudo = await Aplicar(AnalisisLibre("seguir"), Ahora.AddHours(3).AddMinutes(5));

        var sesion = await _db.SesionesCaptura.SingleAsync();
        Assert.Equal(EstadoSesionCaptura.EnCurso, sesion.Estado);
        Assert.Contains("seguimos", reanudo.MensajeAlumno.ToLowerInvariant());
    }

    [Fact]
    public async Task PausaConPreguntaViva_SeguirLaConserva()
    {
        await AbrirPendienteDeEjercicio();
        Refrescar();

        var reanudo = await Aplicar(AnalisisLibre("seguir"), Ahora.AddHours(3));

        var sesion = await _db.SesionesCaptura.SingleAsync();
        Assert.Equal(EstadoSesionCaptura.EsperandoAclaracion, sesion.Estado);
        Assert.Equal(1, await _db.AclaracionesPendientes.CountAsync());
        Assert.Contains("pendiente", reanudo.MensajeAlumno.ToLowerInvariant());
    }

    // ─── Ayuda y cortesías ──────────────────────────────────────────────────────

    [Fact]
    public async Task AyudaRespondeYNoTocaEstado()
    {
        await AplicarAltaBase();
        Refrescar();

        var ayuda = await Aplicar(AnalisisTexto("¿cómo hago?", IntentoCaptura.AyudaConsulta));

        var sesion = await _db.SesionesCaptura.SingleAsync();
        Assert.Equal(EstadoSesionCaptura.EnCurso, sesion.Estado);
        Assert.Contains("ejercicio + series", ayuda.MensajeAlumno);
        Assert.Contains("4 series", ayuda.MensajeAlumno);
        Assert.Contains("primera serie 12 repeticiones", ayuda.MensajeAlumno);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────────

    // Alta base = primer dictado en regla: "sillón 4 series".
    private async Task<CapturaRespuesta> AplicarAltaBase() =>
        await Aplicar(AnalisisCargaEjercicio("sillón 4 series", "sillón", Match(1, "Sillón de cuadríceps"), cantidad: 4));

    // Abre una aclaración de ejercicio VIVA: lote de 4 filas sin ejercicio.
    private async Task AbrirPendienteDeEjercicio()
    {
        await AplicarAltaBase();
        Refrescar();

        // Un minuto después: el lote pendiente es el «lote vivo» (último creado).
        await Aplicar(AnalisisCargaEjercicio(
            "el pecho 4 series", "el pecho",
            Ambiguo((3, "Press de pecho en máquina"), (4, "Cruces en polea")), cantidad: 4),
            Ahora.AddMinutes(1));
    }

    private Task<CapturaRespuesta> Aplicar(AnalisisCaptura analisis, DateTime? ahora = null)
        => _servicio.AplicarTextoAsync(_alumnoId, analisis, ahora ?? Ahora, CancellationToken.None);

    private void Refrescar() => _db.ChangeTracker.Clear();

    private static int[] ExtraerRegistroIds(string? contexto)
    {
        if (string.IsNullOrWhiteSpace(contexto))
        {
            return [];
        }

        using var doc = System.Text.Json.JsonDocument.Parse(contexto);
        var ids = new List<int>();
        if (doc.RootElement.TryGetProperty("registroIds", out var arreglo))
        {
            foreach (var item in arreglo.EnumerateArray())
            {
                if (item.TryGetInt32(out var id))
                {
                    ids.Add(id);
                }
            }
        }
        return [.. ids];
    }

    private static ClasificacionIntent Clasif(IntentoCaptura intento) => ClasificacionIntent.Ok(intento, 0.9);

    private static AnalisisCaptura AnalisisTexto(string texto, IntentoCaptura intento) =>
        new(texto, Clasif(intento), null, null);

    private static AnalisisCaptura AnalisisLibre(string texto) =>
        // Respuesta directa a una aclaración: el intent no aplica (no es CIERRE).
        AnalisisTexto(texto, IntentoCaptura.Invalido);

    private static AnalisisCaptura AnalisisCargaEjercicio(
        string texto,
        string? ejercicio = null,
        ResultadoResolucionEjercicio? resolucion = null,
        int? reps = null,
        decimal? pesoKg = null,
        Unidad? unidad = null,
        int? serie = null,
        int? cantidad = null,
        int? tiempoSeg = null) =>
        AnalisisCarga(IntentoCaptura.CargaEjercicio, texto, ejercicio, resolucion, reps, pesoKg, unidad, serie, cantidad, tiempoSeg);

    private static AnalisisCaptura AnalisisCargaSerie(
        string texto,
        string? ejercicio = null,
        ResultadoResolucionEjercicio? resolucion = null,
        int? reps = null,
        decimal? pesoKg = null,
        Unidad? unidad = null,
        int? serie = null,
        int? cantidad = null,
        int? tiempoSeg = null) =>
        AnalisisCarga(IntentoCaptura.CargaSerie, texto, ejercicio, resolucion, reps, pesoKg, unidad, serie, cantidad, tiempoSeg);

    private static AnalisisCaptura AnalisisCarga(
        IntentoCaptura intento,
        string texto,
        string? ejercicio,
        ResultadoResolucionEjercicio? resolucion,
        int? reps,
        decimal? pesoKg,
        Unidad? unidad,
        int? serie,
        int? cantidad,
        int? tiempoSeg) =>
        new(
            texto,
            Clasif(intento),
            new EntidadesNlu(ejercicio, serie, cantidad, reps, pesoKg, unidad, tiempoSeg, null, null, null),
            resolucion);

    private static ResultadoResolucionEjercicio Match(int id, string canonico) =>
        ResultadoResolucionEjercicio.Matcheado(id, canonico, 1.0);

    private static ResultadoResolucionEjercicio Ambiguo(params (int Id, string Nombre)[] opciones) =>
        ResultadoResolucionEjercicio.Ambiguo(
            opciones.Select(o => new SugerenciaEjercicio(o.Id, o.Nombre, 0.9)).ToList());

    private static ResultadoResolucionEjercicio SinMatch(params (int Id, string Nombre)[] cercanos) =>
        ResultadoResolucionEjercicio.SinMatch(
            cercanos.Select(o => new SugerenciaEjercicio(o.Id, o.Nombre, 0.6)).ToList());

    private void BorrarBase()
    {
        foreach (var sufijo in new[] { "", "-wal", "-shm", "-journal" })
        {
            var file = _dbPath + sufijo;
            try
            {
                if (File.Exists(file))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                    File.Delete(file);
                }
            }
            catch (IOException)
            {
                // best effort: los artilugios -wal/-shm quedan en %TEMP%.
            }
        }
    }
}
