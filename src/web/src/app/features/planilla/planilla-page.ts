import { DatePipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  computed,
  effect,
  inject,
  input,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { Observable } from 'rxjs';
import {
  ComandoEdicionFila,
  ComandoNuevaFila,
  FallaApi,
  FilaParcialVista,
  FormatoPlanillaExportable,
  PlanillaApi,
  PlanillaConfirmadaVista,
  RespuestaSesionActual,
  SesionActualVista,
  fallaDe,
} from '../../core/planilla-api';
import { DictadoVoz } from './dictado-voz';
import { ReportePreview, FormatoVistaPrevia } from './reporte-preview';
import { ReporteWorkspace, ModoReporte } from './reporte-workspace';

/** Borrador local de una fila (alta o edición); los números viajan como texto. */
interface BorradorFila {
  ejercicioCrudo: string;
  serie: string;
  reps: string;
  peso: string;
  pesoAlCuerpo: boolean;
}

/** Campos del borrador que se editan como texto libre en la cuadrícula. */
type CampoTextoBorrador = 'ejercicioCrudo' | 'serie' | 'reps' | 'peso';

const INTERVALO_ENCUESTA_MS = 10_000;

/**
 * Vista Alumno · Planilla (arquitectura §6): cuadrícula editable de parciales
 * capturados por texto y voz en la PWA (ADR-0009) + confirmación (snapshot).
 * Reglas de edición en
 * ADR-0007/0017: solo mientras la sesión admite edición; se puede confirmar
 * desde EnCurso o Lista cuando no hay una aclaración pendiente.
 *
 * La identificación usa el alumno de prueba configurado en la ruta raíz hasta
 * AuthApi; mientras tanto los dispositivos de prueba comparten su historial.
 */
@Component({
  selector: 'app-planilla-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, ReportePreview, ReporteWorkspace],
  templateUrl: './planilla-page.html',
  styleUrl: './planilla-page.scss',
})
export class PlanillaPage {
  private readonly planillaApi = inject(PlanillaApi);
  readonly dictado = inject(DictadoVoz);

  /** Viene de la ruta `alumno/:alumnoId/planilla` (input binding de rutas). */
  readonly alumnoId = input.required<number>();
  private readonly entradaCaptura = viewChild<ElementRef<HTMLTextAreaElement>>('entradaCaptura');

  readonly cargando = signal(true);
  readonly sesion = signal<RespuestaSesionActual | null>(null);
  readonly planillaConfirmada = signal<PlanillaConfirmadaVista | null>(null);
  readonly historialPlanillas = signal<PlanillaConfirmadaVista[]>([]);
  readonly historialAbierto = signal(false);
  readonly cargandoHistorial = signal(false);
  readonly fallaHistorial = signal<FallaApi | null>(null);
  readonly nuevoEntrenamiento = signal(false);
  private readonly reporteDesdeHistorial = signal(false);
  readonly fallaCarga = signal<FallaApi | null>(null);
  readonly fallaAccion = signal<FallaApi | null>(null);
  readonly accionEnCurso = signal(false);
  readonly exportacionEnCurso = signal(false);
  readonly vistaPrevia = signal<FormatoVistaPrevia | null>(null);
  readonly pantallaReportes = signal<ModoReporte | null>(null);
  readonly correoDestino = signal('');
  readonly formatoCorreo = signal<FormatoPlanillaExportable>('excel');
  readonly mensajeExportacion = signal<string | null>(null);
  readonly fallaExportacion = signal<string | null>(null);

  readonly filaEditandoId = signal<number | null>(null);
  readonly borradorEdicion = signal<BorradorFila | null>(null);
  readonly altaAbierta = signal(false);
  readonly borradorAlta = signal<BorradorFila | null>(null);

  /** Borrado en dos pasos: primer click arma la confirmación. */
  readonly filaEnConfirmacion = signal<number | null>(null);
  readonly borrarTodasEnConfirmacion = signal(false);

  /** Panel de dictado (ADR-0009): borrador del texto + última respuesta del bot. */
  readonly borradorCaptura = signal('');
  readonly ultimoMensajeBot = signal<string | null>(null);
  readonly capturaEnCurso = signal(false);
  readonly fallaCaptura = signal<FallaApi | null>(null);

  /** Aviso flotante estilo prototipo (cooperación en la esquina inferior derecha). */
  readonly toast = signal<{ texto: string; malo: boolean } | null>(null);
  private temporizadorToast: ReturnType<typeof setTimeout> | null = null;

  readonly sesionViva = computed(() => this.sesion()?.sesion ?? null);
  readonly filas = computed(() => this.sesionViva()?.filas ?? []);
  readonly filasPendientesDeDatos = computed(() => {
    return this.filas()
      .filter((fila) => fila.reps === null || (fila.pesoKg === null && fila.unidad !== 'PesoCorporal'))
      .sort((a, b) => a.creadoUtc.localeCompare(b.creadoUtc) || a.nroSerie - b.nroSerie);
  });
  readonly progresoSeries = computed(() => {
    const total = this.filas().length;
    const completas = total - this.filasPendientesDeDatos().length;
    return {
      completas,
      total,
      porcentaje: total === 0 ? 0 : Math.round((completas / total) * 100),
    };
  });
  readonly avisoCompletitud = computed(() => {
    const pendientes = this.filasPendientesDeDatos();
    if (pendientes.length === 0) {
      return null;
    }

    const detalle = pendientes.map((fila) => {
      const faltantes: string[] = [];
      if (fila.reps === null) faltantes.push('repeticiones');
      if (fila.pesoKg === null && fila.unidad !== 'PesoCorporal') faltantes.push('peso');
      const nombre = fila.ejercicioCanonico ?? fila.ejercicioCrudo ?? 'ejercicio sin nombre';
      return `${nombre}, serie ${fila.nroSerie} (${faltantes.join(' y ')})`;
    });

    return detalle.join(' · ');
  });
  readonly cuadriculaEditable = computed(() => {
    const estado = this.sesionViva()?.estado;
    return !this.planillaConfirmada() && (estado === 'EnCurso' || estado === 'Lista');
  });
  readonly serieSugerida = computed(() => this.sesionViva()?.ordenSerieSiguiente ?? 1);

  private encuesta: ReturnType<typeof setInterval> | null = null;

  constructor() {
    effect(() => {
      const id = this.alumnoId();
      untracked(() => {
        this.planillaConfirmada.set(null);
        this.historialPlanillas.set([]);
        this.historialAbierto.set(false);
        this.nuevoEntrenamiento.set(false);
        this.reporteDesdeHistorial.set(false);
        this.cerrarBorradores();
        this.borradorCaptura.set('');
        this.ultimoMensajeBot.set(null);
        this.fallaCaptura.set(null);
        this.mensajeExportacion.set(null);
        this.fallaExportacion.set(null);
        this.cargar(id);
        this.rearmarEncuesta(id);
      });
    });

    inject(DestroyRef).onDestroy(() => {
      this.detenerEncuesta();
      if (this.temporizadorToast !== null) {
        clearTimeout(this.temporizadorToast);
      }
    });
  }

  // ─── Carga y encuesta (la cuadrícula se actualiza sola, arquitectura S7) ────

  recargar(): void {
    this.fallaAccion.set(null);
    this.cargar(this.alumnoId());
  }

  private cargar(alumnoId: number): void {
    this.planillaApi.sesionActual(alumnoId).subscribe({
      next: (respuesta) => {
        this.sesion.set(respuesta);
        this.fallaCarga.set(null);
        this.cargando.set(false);
      },
      error: (error: unknown) => {
        this.fallaCarga.set(fallaDe(error));
        this.cargando.set(false);
      },
    });

    this.cargarHistorial(alumnoId);
  }

  private cargarHistorial(alumnoId: number): void {
    this.cargandoHistorial.set(true);
    this.fallaHistorial.set(null);
    this.planillaApi.historial(alumnoId).subscribe({
      next: (historial) => {
        this.historialPlanillas.set(historial);
        this.cargandoHistorial.set(false);
      },
      error: (error: unknown) => {
        this.fallaHistorial.set(fallaDe(error));
        this.cargandoHistorial.set(false);
      },
    });

  }

  private rearmarEncuesta(alumnoId: number): void {
    this.detenerEncuesta();
    this.encuesta = setInterval(() => this.encuestar(alumnoId), INTERVALO_ENCUESTA_MS);
  }

  private detenerEncuesta(): void {
    if (this.encuesta !== null) {
      clearInterval(this.encuesta);
      this.encuesta = null;
    }
  }

  /** Encuesta pausada mientras hay borrador local abierto: el refresco no pisa. */
  private encuestar(alumnoId: number): void {
    if (
      this.filaEditandoId() !== null
      || this.altaAbierta()
      || this.planillaConfirmada()
      || this.accionEnCurso()
      || this.capturaEnCurso()
    ) {
      return;
    }

    this.planillaApi.sesionActual(alumnoId).subscribe({
      next: (respuesta) => {
        this.sesion.set(respuesta);
        this.fallaCarga.set(null);
      },
      error: () => {
        // La encuesta falla en silencio: el estado de carga ya está mostrado.
      },
    });
  }

  // ─── Captura por texto (dictado desde la PWA, ADR-0009) ─────────────────────

  /**
   * Envia el texto crudo del dictado al mismo pipeline NLU del chat: la
   * respuesta repinta la cuadrícula y el banner muestra lo que respondió el bot.
   * La sesión nueva nace con el primer texto válido; la confirmada permanece
   * como snapshot consultable en el historial.
   */
  enviarCaptura(): void {
    if (this.capturaEnCurso()) {
      return;
    }

    const texto = this.borradorCaptura().trim();
    if (texto.length === 0) {
      this.fallaCaptura.set({
        estadoHttp: 0,
        titulo: 'Sin texto',
        detalle: 'Dictá o escribí el ejercicio y volvé a enviar.',
      });
      return;
    }

    this.capturaEnCurso.set(true);
    this.fallaCaptura.set(null);
    this.planillaApi.capturarTexto(this.alumnoId(), texto).subscribe({
      next: (respuesta) => {
        this.planillaConfirmada.set(null);
        this.nuevoEntrenamiento.set(false);
        this.sesion.set(respuesta.respuesta);
        this.ultimoMensajeBot.set(respuesta.mensajeAlumno);
        this.borradorCaptura.set('');
        this.capturaEnCurso.set(false);
      },
      error: (error: unknown) => {
        this.fallaCaptura.set(fallaDe(error));
        this.capturaEnCurso.set(false);
      },
    });
  }

  // ─── Dictado por voz en la PWA (ADR-0011) ───────────────────────────────────

  /**
   * El micrófono del navegador alimenta el MISMO borrador de captura: el
   * transcript final parece escrito por el alumno, que lo revisa antes de
   * enviar. Si ya está escuchando, alterna a detener.
   */
  alternarDictado(): void {
    if (this.dictado.escuchando()) {
      this.dictado.detener();
      return;
    }

    this.dictado.escuchar((texto) =>
      this.borradorCaptura.update((previo) =>
        previo.trim().length === 0 ? texto : `${previo} ${texto}`),
    );
  }

  // ─── Alta de fila (POST: fila completa, ADR-0007 S3) ────────────────────────

  abrirAlta(): void {
    this.filaEditandoId.set(null);
    this.borradorEdicion.set(null);
    this.fallaAccion.set(null);
    this.altaAbierta.set(true);
    this.borradorAlta.set({
      ejercicioCrudo: '',
      serie: String(this.serieSugerida()),
      reps: '',
      peso: '',
      pesoAlCuerpo: false,
    });
  }

  cerrarAlta(): void {
    this.altaAbierta.set(false);
    this.borradorAlta.set(null);
    this.fallaAccion.set(null);
  }

  /** ¿La fila está abierta en edición? (lo consultan las celdas de la cuadrícula). */
  editando(fila: FilaParcialVista): boolean {
    return this.filaEditandoId() === fila.id;
  }

  actualizarBorradorEdicion(campo: CampoTextoBorrador, valor: string): void {
    this.borradorEdicion.update((borrador) =>
      borrador ? this.conValorTexto(borrador, campo, valor) : borrador,
    );
  }

  marcarAlCuerpoEdicion(marcado: boolean): void {
    this.borradorEdicion.update((borrador) =>
      borrador ? { ...borrador, pesoAlCuerpo: marcado, peso: marcado ? '' : borrador.peso } : borrador,
    );
  }

  actualizarBorradorAlta(campo: CampoTextoBorrador, valor: string): void {
    this.borradorAlta.update((borrador) =>
      borrador ? this.conValorTexto(borrador, campo, valor) : borrador,
    );
  }

  marcarAlCuerpoAlta(marcado: boolean): void {
    this.borradorAlta.update((borrador) =>
      borrador ? { ...borrador, pesoAlCuerpo: marcado, peso: marcado ? '' : borrador.peso } : borrador,
    );
  }

  private conValorTexto(
    borrador: BorradorFila,
    campo: CampoTextoBorrador,
    valor: string,
  ): BorradorFila {
    switch (campo) {
      case 'ejercicioCrudo':
        return { ...borrador, ejercicioCrudo: valor };
      case 'serie':
        return { ...borrador, serie: valor };
      case 'reps':
        return { ...borrador, reps: valor };
      case 'peso':
        return { ...borrador, peso: valor };
    }
  }

  guardarAlta(): void {
    const borrador = this.borradorAlta();
    if (!borrador) {
      return;
    }

    const errores = this.validarAlta(borrador);
    if (errores.length > 0) {
      return this.mostrarFallaLocal(errores);
    }

    const reps = parsearEnteroPositivo(borrador.reps);
    const pesoKg = borrador.pesoAlCuerpo ? null : parsearDecimalNoNegativo(borrador.peso);

    // Fila completa según el TIPO de ejercicio (misma regla que CampoFaltante,
    // ADR-0014): el ejercicio por tiempo ya no exige dato; peso kg, o «al
    // cuerpo» con su unidad; el tiempo no se captura (campo retirado del MVP).
    const comando: ComandoNuevaFila = {
      alumnoId: this.alumnoId(),
      ejercicioId: null,
      ejercicioNombre: borrador.ejercicioCrudo.trim(),
      nroSerie: Number(borrador.serie),
      reps,
      pesoKg,
      unidad: borrador.pesoAlCuerpo ? 'PesoCorporal' : pesoKg !== null ? 'Kg' : null,
      tiempoSeg: null,
    };

    this.ejecutar(
      this.planillaApi.agregarFila(comando),
      (respuesta) => {
        this.sesion.set(respuesta);
        this.cerrarAlta();
      },
      'Fila agregada a la cuadrícula.',
    );
  }

  private validarAlta(borrador: BorradorFila): string[] {
    const errores: string[] = [];

    if (borrador.ejercicioCrudo.trim().length === 0) {
      errores.push('Escribí el nombre del ejercicio.');
    }

    if (leerEntero(borrador.serie) === null) {
      errores.push('El número de serie va de 1 en adelante.');
    }

    if (leerEntero(borrador.reps) === null) {
      errores.push('La fila necesita las repeticiones antes de guardarse.');
    }
    if (!borrador.pesoAlCuerpo && parsearDecimalNoNegativo(borrador.peso) === null) {
      errores.push('La fila necesita el peso o marcar «al cuerpo».');
    }

    return errores;
  }

  // ─── Edición de fila (PUT: reemplazo completo, ADR-0007 S2) ─────────────────

  abrirEdicion(fila: FilaParcialVista): void {
    this.altaAbierta.set(false);
    this.borradorAlta.set(null);
    this.filaEnConfirmacion.set(null);
    this.borrarTodasEnConfirmacion.set(false);
    this.fallaAccion.set(null);
    const pesoAlCuerpo = fila.unidad === 'PesoCorporal';
    this.filaEditandoId.set(fila.id);
    this.borradorEdicion.set({
      ejercicioCrudo: fila.ejercicioCanonico ?? fila.ejercicioCrudo ?? '',
      serie: String(fila.nroSerie),
      reps: fila.reps === null ? '' : String(fila.reps),
      peso: pesoAlCuerpo ? '' : fila.pesoKg === null ? '' : String(fila.pesoKg),
      pesoAlCuerpo,
    });
  }

  cancelarEdicion(): void {
    this.filaEditandoId.set(null);
    this.borradorEdicion.set(null);
    this.fallaAccion.set(null);
  }

  guardarEdicion(fila: FilaParcialVista): void {
    const borrador = this.borradorEdicion();
    if (!borrador || this.filaEditandoId() !== fila.id) {
      return;
    }

    const errores = this.validarEdicion(borrador);
    if (errores.length > 0) {
      return this.mostrarFallaLocal(errores);
    }

    const pesoKg = borrador.pesoAlCuerpo
      ? null
      : parsearDecimalNoNegativo(borrador.peso);

    // Peso en kg siempre (ADR-0004 S6: planilla 100 % en kg); «al cuerpo» lleva
    // su unidad; peso vacío limpia el campo. El tiempo ya no se captura
    // (ADR-0014): se envía siempre null.
    const unidad = borrador.pesoAlCuerpo
        ? 'PesoCorporal'
        : pesoKg !== null
          ? 'Kg'
          : null;

    const comando: ComandoEdicionFila = {
      ejercicioId: null,
      ejercicioCrudo: borrador.ejercicioCrudo.trim(),
      nroSerie: Number(borrador.serie),
      reps: leerEntero(borrador.reps),
      pesoKg,
      unidad,
      tiempoSeg: null,
    };

    this.ejecutar(
      this.planillaApi.editarFila(fila.id, comando),
      (respuesta) => {
        this.sesion.set(respuesta);
        this.cancelarEdicion();
      },
      'Cambios guardados en la fila.',
    );
  }

  private validarEdicion(borrador: BorradorFila): string[] {
    const errores: string[] = [];

    if (borrador.ejercicioCrudo.trim().length === 0) {
      errores.push('Escribí el nombre del ejercicio.');
    }

    if (leerEntero(borrador.serie) === null) {
      errores.push('El número de serie va de 1 en adelante.');
    }

    if (borrador.reps.length > 0 && leerEntero(borrador.reps) === null) {
      errores.push('Las repeticiones van de 1 en adelante.');
    }

    if (!borrador.pesoAlCuerpo
      && borrador.peso.length > 0
      && parsearDecimalNoNegativo(borrador.peso) === null) {
      errores.push('El peso no puede ser negativo.');
    }

    return errores;
  }

  // ─── Eliminación de fila (dos pasos) ────────────────────────────────────────

  pedirBorrado(filaId: number): void {
    this.filaEnConfirmacion.set(filaId);
  }

  cancelarBorrado(): void {
    this.filaEnConfirmacion.set(null);
  }

  eliminarFila(fila: FilaParcialVista): void {
    this.ejecutar(
      this.planillaApi.eliminarFila(fila.id),
      (respuesta) => {
        this.sesion.set(respuesta);
        this.filaEnConfirmacion.set(null);
      },
      'Fila eliminada de la sesión.',
    );
  }

  pedirBorradoTotal(): void {
    this.borrarTodasEnConfirmacion.set(true);
  }

  cancelarBorradoTotal(): void {
    this.borrarTodasEnConfirmacion.set(false);
  }

  eliminarTodasLasFilas(): void {
    this.ejecutar(
      this.planillaApi.eliminarTodasLasFilas(this.alumnoId()),
      (respuesta) => {
        this.sesion.set(respuesta);
        this.borrarTodasEnConfirmacion.set(false);
        this.filaEnConfirmacion.set(null);
        this.cerrarBorradores();
      },
      'Se eliminaron todos los registros de la cuadrícula.',
    );
  }

  // ─── Confirmación (snapshot write-once, ADR-0007 S4) ────────────────────────

  confirmar(): void {
    const alumnoId = this.alumnoId();
    this.ejecutar(
      this.planillaApi.confirmar(alumnoId),
      (planilla) => {
        this.planillaConfirmada.set(planilla);
        this.nuevoEntrenamiento.set(false);
        this.reporteDesdeHistorial.set(false);
        this.historialAbierto.set(false);
        this.historialPlanillas.update((historial) => [
          planilla,
          ...historial.filter((existente) => existente.id !== planilla.id),
        ].sort((a, b) => Date.parse(b.confirmadaUtc) - Date.parse(a.confirmadaUtc)));
        this.pantallaReportes.set('seleccion');
        this.vistaPrevia.set(null);
        // La sesión confirmada deja de ser hablante: la cuadrícula queda vacía.
        this.sesion.update((respuesta) => (respuesta ? { ...respuesta, sesion: null } : respuesta));
      },
      'Planilla confirmada y congelada.',
    );
  }

  descargarPlanilla(formato: FormatoPlanillaExportable): void {
    const planilla = this.planillaConfirmada();
    if (!planilla || this.exportacionEnCurso()) {
      return;
    }

    this.exportacionEnCurso.set(true);
    this.mensajeExportacion.set(null);
    this.fallaExportacion.set(null);
    this.planillaApi.descargarPlanilla(planilla.id, this.alumnoId(), formato).subscribe({
      next: (archivo) => {
        const url = URL.createObjectURL(archivo);
        const enlace = document.createElement('a');
        enlace.href = url;
        enlace.download = `planilla-${planilla.id}.${formato === 'excel' ? 'xlsx' : 'pdf'}`;
        enlace.click();
        URL.revokeObjectURL(url);
        this.mensajeExportacion.set(`Descargaste la planilla en formato ${formato.toUpperCase()}.`);
        this.exportacionEnCurso.set(false);
      },
      error: (error: unknown) => {
        this.fallaExportacion.set(fallaDe(error).detalle ?? fallaDe(error).titulo);
        this.exportacionEnCurso.set(false);
      },
    });
  }

  abrirPrevisualizacion(formato: FormatoVistaPrevia): void {
    if (this.planillaConfirmada() && this.pantallaReportes() !== 'seleccion') {
      this.pantallaReportes.set(formato);
      this.vistaPrevia.set(formato);
    }
  }

  cerrarPrevisualizacion(): void {
    this.vistaPrevia.set(null);
  }

  cerrarReportes(): void {
    const volverAlHistorial = this.reporteDesdeHistorial();
    this.reporteDesdeHistorial.set(false);
    if (volverAlHistorial) {
      this.planillaConfirmada.set(null);
      this.historialAbierto.set(true);
    }
    this.vistaPrevia.set(null);
    this.pantallaReportes.set(null);
  }

  iniciarNuevoEntrenamiento(): void {
    this.planillaConfirmada.set(null);
    this.pantallaReportes.set(null);
    this.vistaPrevia.set(null);
    this.reporteDesdeHistorial.set(false);
    this.historialAbierto.set(false);
    this.nuevoEntrenamiento.set(true);
    this.borradorCaptura.set('');
    this.ultimoMensajeBot.set(null);
    this.fallaCaptura.set(null);
    this.fallaAccion.set(null);
    this.mensajeExportacion.set(null);
    this.fallaExportacion.set(null);
    this.entradaCaptura()?.nativeElement.focus();
  }

  alternarHistorial(): void {
    this.historialAbierto.update((abierto) => !abierto);
    if (this.historialAbierto() && this.fallaHistorial()) {
      this.cargarHistorial(this.alumnoId());
    }
  }

  verPlanillaHistorial(planilla: PlanillaConfirmadaVista): void {
    this.planillaConfirmada.set(planilla);
    this.reporteDesdeHistorial.set(true);
    this.historialAbierto.set(false);
    this.pantallaReportes.set('seleccion');
    this.vistaPrevia.set(null);
  }

  enviarPlanillaPorCorreo(): void {
    const planilla = this.planillaConfirmada();
    const correo = this.correoDestino().trim();
    if (!planilla || this.exportacionEnCurso()) {
      return;
    }
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(correo)) {
      this.fallaExportacion.set('Escribí una dirección de correo válida.');
      this.mensajeExportacion.set(null);
      return;
    }

    this.exportacionEnCurso.set(true);
    this.mensajeExportacion.set(null);
    this.fallaExportacion.set(null);
    this.planillaApi.enviarPlanillaPorCorreo(planilla.id, {
      alumnoId: this.alumnoId(),
      correo,
      formato: this.formatoCorreo(),
    }).subscribe({
      next: (respuesta) => {
        this.mensajeExportacion.set(respuesta.mensaje);
        this.exportacionEnCurso.set(false);
      },
      error: (error: unknown) => {
        this.fallaExportacion.set(fallaDe(error).detalle ?? fallaDe(error).titulo);
        this.exportacionEnCurso.set(false);
      },
    });
  }

  // ─── Ayudas de presentación ────────────────────────────────────────────────

  /**
   * Ejercicio del «lote vivo»: la última fila creada manda (así lo resuelve el
   * backend con la regla de datos de serie). Canónico si pudo resolverlo con
   * el catálogo; si no, el texto crudo que dictó el alumno.
   */
  cargandoActual(viva: SesionActualVista): string | null {
    const ultima = [...viva.filas].sort((a, b) => b.creadoUtc.localeCompare(a.creadoUtc))[0];
    if (!ultima) {
      return null;
    }
    return ultima.ejercicioCanonico ?? ultima.ejercicioCrudo ?? null;
  }

  etiquetaEjercicio(fila: FilaParcialVista): string {
    return fila.ejercicioCanonico ?? fila.ejercicioCrudo ?? 'sin ejercicio';
  }

  pesoTexto(fila: FilaParcialVista): string {
    if (fila.unidad === 'PesoCorporal') {
      return 'al cuerpo';
    }
    return fila.pesoKg === null ? '—' : this.formatoKg(fila.pesoKg);
  }

  /** Peso del snapshot confirmado, con una decimal como el prototipo («14.0 kg»). */
  formatoKg(pesoKg: number): string {
    return `${pesoKg.toFixed(1)} kg`;
  }

  /** Chip del encabezado según el estado de la sesión hablante. */
  chipEstado(): { clase: string; texto: string } {
    const estado = this.sesionViva()?.estado ?? null;
    switch (estado) {
      case 'EnCurso':
        return { clase: 'badge-violeta', texto: 'Capturando' };
      case 'Lista':
        return { clase: 'badge-ok', texto: 'Lista para confirmar' };
      case 'EsperandoAclaracion':
        return { clase: 'badge-aviso', texto: 'Pendiente del bot' };
      case 'Expirada':
        return { clase: 'badge-aviso', texto: 'Pausada' };
      default:
        if (this.nuevoEntrenamiento()) {
          return { clase: 'badge-ok', texto: 'Nueva planilla' };
        }
        return { clase: '', texto: 'Sin sesión' };
    }
  }

  // ─── Ejecución común de comandos ────────────────────────────────────────────

  private ejecutar<T extends RespuestaSesionActual | PlanillaConfirmadaVista>(
    peticion: Observable<T>,
    alTerminar: (respuesta: T) => void,
    toastExito?: string,
  ): void {
    this.accionEnCurso.set(true);
    this.fallaAccion.set(null);
    peticion.subscribe({
      next: (respuesta) => {
        alTerminar(respuesta);
        if (toastExito) {
          this.mostrarToast(toastExito);
        }
        this.accionEnCurso.set(false);
      },
      error: (error: unknown) => {
        this.fallaAccion.set(fallaDe(error));
        this.accionEnCurso.set(false);
        this.mostrarToast('No se pudo aplicar el cambio.', true);
      },
    });
  }

  /** Muestra el toast del prototipo 3.2 s (esquina inferior derecha). */
  private mostrarToast(texto: string, malo = false): void {
    if (this.temporizadorToast !== null) {
      clearTimeout(this.temporizadorToast);
    }
    this.toast.set({ texto, malo });
    this.temporizadorToast = setTimeout(() => this.toast.set(null), 3200);
  }

  private mostrarFallaLocal(errores: string[]): void {
    const detalles = errores.filter((mensaje) => mensaje.length > 0);
    this.fallaAccion.set({
      estadoHttp: 0,
      titulo: 'Revisá los datos de la fila',
      detalle: detalles.length > 0 ? detalles.join(' ') : null,
    });
  }

  private cerrarBorradores(): void {
    this.filaEditandoId.set(null);
    this.borradorEdicion.set(null);
    this.altaAbierta.set(false);
    this.borradorAlta.set(null);
    this.filaEnConfirmacion.set(null);
    this.borrarTodasEnConfirmacion.set(false);
    this.fallaAccion.set(null);
  }
}

/** Entero estricto ≥ 1 (para serie y reps). */
function leerEntero(valor: string): number | null {
  const limpio = valor.trim();
  if (!/^\d+$/.test(limpio)) {
    return null;
  }
  const numero = Number(limpio);
  return Number.isSafeInteger(numero) && numero >= 1 ? numero : null;
}

const parsearEnteroPositivo = leerEntero;

/** Decimal ≥ 0 con hasta 2 decimales (para el peso en kg). */
function parsearDecimalNoNegativo(valor: string): number | null {
  const limpio = valor.trim().replace(',', '.');
  if (!/^\d+(?:\.\d{1,2})?$/.test(limpio)) {
    return null;
  }
  const numero = Number(limpio);
  return Number.isFinite(numero) && numero >= 0 && numero < 100_000 ? numero : null;
}
