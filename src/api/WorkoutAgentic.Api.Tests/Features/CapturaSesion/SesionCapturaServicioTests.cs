using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WorkoutAgentic.Api.Data;
using WorkoutAgentic.Api.Data.Entities;
using WorkoutAgentic.Api.Features.CapturaSesion;
using WorkoutAgentic.Api.Features.Nlu;
using WorkoutAgentic.Api.Features.WhatsappWebhook;

namespace WorkoutAgentic.Api.Tests.Features.CapturaSesion;

// Máquina de estados del diálogo de captura sobre SQLite temporal con el
// esquema REAL por migraciones y el catálogo sembrado. Los análisis son
// sintéticos (el slice NLU ya los cubre): nunca se llama a Gemini ni a Meta.
public class SesionCapturaServicioTests : IDisposable
{
    private const string Telefono = "5215512345678";

    private static readonly DateTime Ahora = new(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc);

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"wa-sesion-{Guid.NewGuid():N}.db");
    private readonly CapturasDbContext _db;
    private readonly SesionCapturaServicio _servicio;

    public SesionCapturaServicioTests()
    {
        _db = new CapturasDbContext(
            new DbContextOptionsBuilder<CapturasDbContext>()
                .UseSqlite($"Data Source={_dbPath};Cache=Shared")
                .Options);

        _db.Database.Migrate();
        _db.Ejercicios.AddRange(CatalogoEjercicioSeed.Crear());
        _db.SaveChanges();

        _servicio = new SesionCapturaServicio(
            _db,
            new EjercicioCatalogoSource(_db),
            NullLogger<SesionCapturaServicio>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        BorrarBase();
    }

    // ─── Flujo completo: alta → series → cierre ────────────────────────────────

    [Fact]
    public async Task PrimerContacto_CreaAlumnoSesionYAltoEjercicio()
    {
        var respuesta = await AplicarAlta();

        Assert.Equal(1, await _db.Alumnos.CountAsync());
        var alumno = await _db.Alumnos.SingleAsync();
        Assert.Equal(Telefono, alumno.TelefonoWa);
        Assert.Equal("ACTIVO", alumno.Estado);

        var sesion = await _db.SesionesCaptura.SingleAsync();
        Assert.Equal(EstadoSesionCaptura.EnCurso, sesion.Estado);
        Assert.Equal(1, sesion.EjercicioActivoId);
        Assert.Equal(1, sesion.OrdenSerieSiguiente);
        Assert.Empty(await _db.RegistrosParciales.ToListAsync());
        Assert.Equal(respuesta.SesionId, sesion.Id);
        Assert.Contains("Anotado: Sillón de cuadríceps", respuesta.MensajeAlumno);
    }

    [Fact]
    public async Task SerieSobreActivo_AnotaFilaCompleta()
    {
        await AplicarAlta();
        Refrescar();

        var respuesta = await Aplicar(AnalisisCargaSerie(
            "segunda serie, 10 con 45",
            ejercicio: null,
            reps: 10,
            pesoKg: 45m,
            unidad: Unidad.Kg,
            serie: 2));

        var fila = await _db.RegistrosParciales.SingleAsync();
        Assert.Equal(1, fila.EjercicioId);
        Assert.Equal(2, fila.NroSerie);
        Assert.Equal(10, fila.Reps);
        Assert.Equal(45m, fila.PesoKg);
        Assert.Equal(Unidad.Kg, fila.Unidad);
        Assert.False(fila.PendienteAclaracion);
        Assert.Null(await _db.AclaracionesPendientes.SingleOrDefaultAsync());

        var sesion = await _db.SesionesCaptura.SingleAsync();
        Assert.Equal(3, sesion.OrdenSerieSiguiente);
        Assert.Contains("Serie 2", respuesta.MensajeAlumno);
        Assert.Contains("45 kg", respuesta.MensajeAlumno);
    }

    [Fact]
    public async Task FaltaPeso_CreaLaPreguntaYDejaLaFilaPendiente()
    {
        await AplicarAlta();
        Refrescar();

        var respuesta = await Aplicar(AnalisisCargaSerie(
            "segunda serie, 10", ejercicio: null, reps: 10, serie: 2));

        var fila = await _db.RegistrosParciales.SingleAsync();
        Assert.True(fila.PendienteAclaracion);
        Assert.Null(fila.PesoKg);
        Assert.Null(fila.Unidad);

        var aclaracion = await _db.AclaracionesPendientes.SingleAsync();
        Assert.Equal("peso", aclaracion.Campo);
        Assert.Equal(fila.Id, ExtraerRegistroIds(aclaracion.Contexto).Single());
        Assert.Equal(0, aclaracion.Reintento);

        var sesion = await _db.SesionesCaptura.SingleAsync();
        Assert.Equal(EstadoSesionCaptura.EsperandoAclaracion, sesion.Estado);
        Assert.True(respuesta.AclaracionCreada);
        Assert.Contains("¿Con cuánto peso", respuesta.MensajeAlumno);
    }

    [Fact]
    public async Task RespuestaPesoSuelta_LlenaLaFilaYConfirma()
    {
        await CargarSerieSinPeso();
        Refrescar();

        var respuesta = await Aplicar(AnalisisLibre("50"));

        var fila = await _db.RegistrosParciales.SingleAsync();
        Assert.Equal(50m, fila.PesoKg);
        Assert.Equal(Unidad.Kg, fila.Unidad);
        Assert.False(fila.PendienteAclaracion);
        Assert.Equal(0, await _db.AclaracionesPendientes.CountAsync());

        var sesion = await _db.SesionesCaptura.SingleAsync();
        Assert.Equal(EstadoSesionCaptura.EnCurso, sesion.Estado);
        Assert.Contains("10 reps con 50 kg", respuesta.MensajeAlumno);
    }

    [Fact]
    public async Task DosRespuestasIncomprensibles_AbandonanYElFlujoSigue()
    {
        await CargarSerieSinPeso();
        Refrescar();

        await Aplicar(AnalisisLibre("no sé"));
        Refrescar();
        var aclaracion = await _db.AclaracionesPendientes.SingleAsync();
        Assert.Equal(1, aclaracion.Reintento);

        var segunda = await Aplicar(AnalisisLibre("sigue sin saberlo"));

        Assert.Equal(0, await _db.AclaracionesPendientes.CountAsync());

        var fila = await _db.RegistrosParciales.SingleAsync();
        Assert.Null(fila.PesoKg);
        Assert.True(fila.PendienteAclaracion); // queda marcada para la planilla
        Assert.False(fila.DescripcionPendiente); // sí hay ejercicio: no es descripción libre

        var sesion = await _db.SesionesCaptura.SingleAsync();
        Assert.Equal(EstadoSesionCaptura.EnCurso, sesion.Estado);
        Assert.Contains("marcada", segunda.MensajeAlumno);
    }

    [Fact]
    public async Task ElPesoEnLibras_SeConvierteAKg()
    {
        await AplicarAlta();
        Refrescar();

        await Aplicar(AnalisisCargaSerie(
            "12 con 40 libras", ejercicio: null, reps: 12, pesoKg: 40m, unidad: Unidad.Lb));

        var fila = await _db.RegistrosParciales.SingleAsync();
        Assert.Equal(18.14m, fila.PesoKg);
        Assert.Equal(Unidad.Kg, fila.Unidad);
        Assert.False(fila.PendienteAclaracion);
    }

    // ─── Aclaraciones de ejercicio ─────────────────────────────────────────────

    [Fact]
    public async Task EjercicioAmbiguoSinDatos_PreguntaConOpcionesYAltaConNumero()
    {
        var respuesta = await Aplicar(AnalisisCargaEjercicio(
            "el pecho", "el pecho", Ambiguo((3, "Press de pecho en máquina"), (4, "Cruces en polea"))));

        Assert.Contains("¿A cuál te referís", respuesta.MensajeAlumno);
        Assert.Contains("1) Press de pecho en máquina", respuesta.MensajeAlumno);
        Assert.Contains("2) Cruces en polea", respuesta.MensajeAlumno);
        Assert.True(respuesta.AclaracionCreada);
        Assert.Empty(await _db.RegistrosParciales.ToListAsync()); // sin datos: nada anotado aún

        Refrescar();
        var aclaracion = await _db.AclaracionesPendientes.SingleAsync();
        Assert.Equal("ejercicio", aclaracion.Campo);
        Assert.Empty(ExtraerRegistroIds(aclaracion.Contexto));

        var eleccion = await Aplicar(AnalisisLibre("1"));

        var sesion = await _db.SesionesCaptura.SingleAsync();
        Assert.Equal(3, sesion.EjercicioActivoId);
        Assert.Equal(EstadoSesionCaptura.EnCurso, sesion.Estado);
        Assert.Equal(0, await _db.AclaracionesPendientes.CountAsync());
        Assert.Contains("Anotado: Press de pecho en máquina", eleccion.MensajeAlumno);
    }

    [Fact]
    public async Task EjercicioAmbiguoConDatos_LasFilasEsperanLaEleccion()
    {
        var respuesta = await Aplicar(AnalisisCargaEjercicio(
            "el pecho con 12 y 40",
            "el pecho",
            Ambiguo((3, "Press de pecho en máquina"), (4, "Cruces en polea")),
            reps: 12,
            pesoKg: 40m,
            unidad: Unidad.Kg));

        var filas = await _db.RegistrosParciales.ToListAsync();
        var fila = Assert.Single(filas);
        Assert.Null(fila.EjercicioId);
        Assert.True(fila.PendienteAclaracion);
        Assert.Equal(fila.Id, ExtraerRegistroIds((await _db.AclaracionesPendientes.SingleAsync()).Contexto).Single());

        Refrescar();
        var eleccion = await Aplicar(AnalisisLibre("2"));

        fila = await _db.RegistrosParciales.SingleAsync();
        Assert.Equal(4, fila.EjercicioId);
        Assert.False(fila.PendienteAclaracion);
        Assert.Equal("el pecho", fila.EjercicioCrudo); // el texto del alumno queda para auditar
        Assert.Equal(0, await _db.AclaracionesPendientes.CountAsync());
        Assert.Contains("12 reps con 40 kg", eleccion.MensajeAlumno);
    }

    [Fact]
    public async Task EjercicioDesconocidoRespuestaOtro_QuedaComoTextoLibre()
    {
        var respuesta = await Aplicar(AnalisisCargaEjercicio(
            "movimiento raro", "movimiento raro", SinMatch()));

        Assert.Contains("¿De qué ejercicio es esa serie?", respuesta.MensajeAlumno);

        var segundo = await Aplicar(AnalisisLibre("otro"));

        var sesion = await _db.SesionesCaptura.SingleAsync();
        Assert.Equal(EstadoSesionCaptura.EnCurso, sesion.Estado);
        Assert.Equal(0, await _db.AclaracionesPendientes.CountAsync());
        Assert.Null(sesion.EjercicioActivoId);
        Assert.Contains("Anotado como texto", segundo.MensajeAlumno);
    }

    [Fact]
    public async Task ReintentoDePregunta_RePreguntaConLaMismaLista()
    {
        await Aplicar(AnalisisCargaEjercicio(
            "el pecho", "el pecho", Ambiguo((3, "Press de pecho en máquina"), (4, "Cruces en polea"))));
        Refrescar();

        var reintento = await Aplicar(AnalisisLibre("si jeje"));

        Assert.Contains("1) Press de pecho en máquina", reintento.MensajeAlumno);
        var aclaracion = await _db.AclaracionesPendientes.SingleAsync();
        Assert.Equal(1, aclaracion.Reintento);
    }

    // ─── Lotes, tipos de ejercicio y cierre ─────────────────────────────────────

    [Fact]
    public async Task BloqueTresPorDoce_CreaTresFilasOrdenadas()
    {
        await AplicarAlta();
        Refrescar();

        var respuesta = await Aplicar(AnalisisCargaSerie(
            "3x12 con 40",
            ejercicio: null,
            reps: 12,
            pesoKg: 40m,
            unidad: Unidad.Kg,
            cantidad: 3));

        var filas = await _db.RegistrosParciales.OrderBy(f => f.NroSerie).ToListAsync();
        Assert.Equal(3, filas.Count);
        Assert.Equal(new[] { 1, 2, 3 }, filas.Select(f => f.NroSerie));
        Assert.All(filas, f =>
        {
            Assert.Equal(12, f.Reps);
            Assert.Equal(40m, f.PesoKg);
            Assert.False(f.PendienteAclaracion);
        });
        Assert.Equal(4, (await _db.SesionesCaptura.SingleAsync()).OrdenSerieSiguiente);
        Assert.Contains("Anotadas 3 series", respuesta.MensajeAlumno);
    }

    [Fact]
    public async Task RespuestaPeso_EnUnLoteDeTres_LlenaLasTresFilas()
    {
        await AplicarAlta();
        Refrescar();

        await Aplicar(AnalisisCargaSerie("3x12", ejercicio: null, reps: 12, cantidad: 3));
        Refrescar();
        var pendiente = await _db.AclaracionesPendientes.SingleAsync();
        Assert.Equal("peso", pendiente.Campo);
        Assert.Equal(3, ExtraerRegistroIds(pendiente.Contexto).Length);

        var respuesta = await Aplicar(AnalisisLibre("45"));

        var filas = await _db.RegistrosParciales.ToListAsync();
        Assert.All(filas, f => Assert.Equal(45m, f.PesoKg));
        Assert.All(filas, f => Assert.False(f.PendienteAclaracion));
        Assert.Equal(0, await _db.AclaracionesPendientes.CountAsync());
        Assert.Contains("12 reps con 45 kg", respuesta.MensajeAlumno);
    }

    [Fact]
    public async Task EjercicioPorTiempo_AnotaLosSegundos()
    {
        var respuesta = await Aplicar(AnalisisCargaEjercicio(
            "plancha 1 minuto", "plancha", Match(10, "Plancha"),
            unidad: Unidad.Tiempo, tiempoSeg: 60));

        var fila = await _db.RegistrosParciales.SingleAsync();
        Assert.Equal(10, fila.EjercicioId);
        Assert.Equal(60, fila.TiempoSeg);
        Assert.Equal(Unidad.Tiempo, fila.Unidad);
        Assert.Equal(0, await _db.AclaracionesPendientes.CountAsync());
        Assert.Contains("60 s", respuesta.MensajeAlumno);
    }

    [Fact]
    public async Task EjercicioAlPesoCorporal_NoPreguntaPeso()
    {
        var respuesta = await Aplicar(AnalisisCargaEjercicio(
            "dominadas 10 al cuerpo", "dominadas", Match(9, "Dominadas"),
            reps: 10, unidad: Unidad.PesoCorporal));

        var fila = await _db.RegistrosParciales.SingleAsync();
        Assert.Null(fila.PesoKg);
        Assert.Equal(Unidad.PesoCorporal, fila.Unidad);
        Assert.False(fila.PendienteAclaracion);
        Assert.Equal(0, await _db.AclaracionesPendientes.CountAsync());
        Assert.Contains("10 reps al cuerpo", respuesta.MensajeAlumno);
    }

    [Fact]
    public async Task Cierre_LlevaALista_EInvitaAConfirmar()
    {
        await AplicarAlta();
        Refrescar();

        var cierre = await Aplicar(AnalisisTexto("terminé", IntentoCaptura.Cierre));

        var sesion = await _db.SesionesCaptura.SingleAsync();
        Assert.Equal(EstadoSesionCaptura.Lista, sesion.Estado);
        Assert.Contains("planilla", cierre.MensajeAlumno);

        // Cualquier mensaje siguiente solo recuerda el paso por la SPA.
        var despues = await Aplicar(AnalisisTexto("me olvidaba algo", IntentoCaptura.Invalido));
        Assert.Contains("ya está armada", despues.MensajeAlumno);
        Assert.Equal(EstadoSesionCaptura.Lista, (await _db.SesionesCaptura.SingleAsync()).Estado);
    }

    [Fact]
    public async Task CierreConPreguntaViva_AbandonaLoPendienteYCierra()
    {
        await CargarSerieSinPeso();
        Refrescar();

        var cierre = await Aplicar(AnalisisTexto("terminé", IntentoCaptura.Cierre));

        var sesion = await _db.SesionesCaptura.SingleAsync();
        Assert.Equal(EstadoSesionCaptura.Lista, sesion.Estado);
        Assert.Equal(0, await _db.AclaracionesPendientes.CountAsync());
        var fila = await _db.RegistrosParciales.SingleAsync();
        Assert.True(fila.PendienteAclaracion); // las filas quedan, marcadas
        Assert.Contains("planilla", cierre.MensajeAlumno);
    }

    // ─── Pausa por inactividad ───────────────────────────────────────────────────

    [Fact]
    public async Task Inactividad_PausaYSeReanudaConSeguir()
    {
        await AplicarAlta();
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
        await CargarSerieSinPeso();
        Refrescar();

        var reanudo = await Aplicar(AnalisisLibre("seguir"), Ahora.AddHours(3));

        var sesion = await _db.SesionesCaptura.SingleAsync();
        Assert.Equal(EstadoSesionCaptura.EsperandoAclaracion, sesion.Estado);
        Assert.Equal(1, await _db.AclaracionesPendientes.CountAsync());
        Assert.Contains("pendiente", reanudo.MensajeAlumno.ToLowerInvariant());
    }

    // ─── Auditoría de audio ─────────────────────────────────────────────────────

    [Fact]
    public async Task AudioExito_GuardaLaTranscripcion()
    {
        var mensaje = new IncomingWhatsappMessage(
            "wa-audio-1", Telefono, WhatsappMessageKind.Audio, null, "media-1", "audio/ogg", 7, "sha");
        var analisis = AnalisisCargaEjercicio(
            "hice sillón", "sillón", Match(1, "Sillón de cuadríceps"),
            transcripcion: TranscripcionAudio.Ok("hice sillón"));

        await _servicio.AplicarAsync(mensaje, analisis, Ahora, CancellationToken.None);

        var transcripcion = await _db.Transcripciones.SingleAsync();
        Assert.Equal("media-1", transcripcion.MediaId);
        Assert.Equal("hice sillón", transcripcion.TextoCrudo);
        Assert.Equal("Gemini", transcripcion.Proveedor);
        Assert.Equal(7, transcripcion.DuracionSeg);
        Assert.Equal(transcripcion.SesionId, (await _db.SesionesCaptura.SingleAsync()).Id);
    }

    [Fact]
    public async Task AudioFallido_NoPersisteTranscripcion()
    {
        var mensaje = new IncomingWhatsappMessage(
            "wa-audio-2", Telefono, WhatsappMessageKind.Audio, null, "media-2", "audio/ogg", 9, "sha");
        var analisis = new AnalisisCaptura(
            WhatsappMessageKind.Audio,
            TextoProcesado: "",
            Transcripcion: TranscripcionAudio.Fallo("SIN_AUDIO", null),
            Clasificacion: ClasificacionIntent.Ok(IntentoCaptura.Invalido, 0.2),
            Entidades: null,
            ResolucionEjercicio: null);

        await _servicio.AplicarAsync(mensaje, analisis, Ahora, CancellationToken.None);

        Assert.Equal(0, await _db.Transcripciones.CountAsync());
    }

    // ─── Ayuda y cortesías ──────────────────────────────────────────────────────

    [Fact]
    public async Task AyudaRespondeYNoTocaEstado()
    {
        await Aplicar(AnalisisCargaEjercicio("hice sillón", "sillón", Match(1, "Sillón de cuadríceps")));
        Refrescar();

        var ayuda = await Aplicar(AnalisisTexto("¿cómo hago?", IntentoCaptura.AyudaConsulta));

        var sesion = await _db.SesionesCaptura.SingleAsync();
        Assert.Equal(EstadoSesionCaptura.EnCurso, sesion.Estado);
        Assert.Contains("3x12", ayuda.MensajeAlumno);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────────

    private async Task<CapturaRespuesta> AplicarAlta() =>
        await Aplicar(AnalisisCargaEjercicio("hice sillón", "sillón", Match(1, "Sillón de cuadríceps")));

    private async Task<CapturaRespuesta> CargarSerieSinPeso()
    {
        await AplicarAlta();
        Refrescar();

        return await Aplicar(AnalisisCargaSerie("segunda serie, 10", ejercicio: null, reps: 10, serie: 2));
    }

    private Task<CapturaRespuesta> Aplicar(AnalisisCaptura analisis, DateTime? ahora = null)
    {
        var mensaje = analisis.MensajeKind == WhatsappMessageKind.Audio
            ? new IncomingWhatsappMessage("wa-audio-" + Guid.NewGuid().ToString("N"), Telefono, WhatsappMessageKind.Audio, null, "media-x", "audio/ogg", 3, "sha")
            : new IncomingWhatsappMessage("wa-" + Guid.NewGuid().ToString("N"), Telefono, WhatsappMessageKind.Text, analisis.TextoProcesado, null, null, null, null);

        return _servicio.AplicarAsync(mensaje, analisis, ahora ?? Ahora, CancellationToken.None);
    }

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
        new(WhatsappMessageKind.Text, texto, null, Clasif(intento), null, null);

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
        int? tiempoSeg = null,
        TranscripcionAudio? transcripcion = null) =>
        AnalisisCarga(IntentoCaptura.CargaEjercicio, texto, ejercicio, resolucion, reps, pesoKg, unidad, serie, cantidad, tiempoSeg, transcripcion);

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
        AnalisisCarga(IntentoCaptura.CargaSerie, texto, ejercicio, resolucion, reps, pesoKg, unidad, serie, cantidad, tiempoSeg, null);

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
        int? tiempoSeg,
        TranscripcionAudio? transcripcion) =>
        new(
            transcripcion is null ? WhatsappMessageKind.Text : WhatsappMessageKind.Audio,
            texto,
            transcripcion,
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
