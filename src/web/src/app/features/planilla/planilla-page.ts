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
import { TrainingEditor } from './training-editor';
import { TrainingProgress } from './training-progress';
import { CommandExample } from './command-example';
import { groupExerciseRows } from './exercise-groups';
import { TrainingHistory } from './training-history';
import { ReportEmail } from './report-email';
import { ConfirmationDialog } from '../../shared/confirmation-dialog/confirmation-dialog';
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
  isOneRepMax: boolean;
  comments: string;
  observations: string;
}

/** Campos del borrador que se editan como texto libre en la cuadrícula. */
type CampoTextoBorrador = 'ejercicioCrudo' | 'serie' | 'reps' | 'peso' | 'comments' | 'observations';

const INTERVALO_ENCUESTA_MS = 10_000;

/**
 * Vista Alumno · Planilla (arquitectura §6): cuadrícula editable de parciales
 * capturados por texto y voz en la PWA (ADR-0009) + confirmación (snapshot).
 * Reglas de edición en
 * ADR-0007/0017: solo mientras la sesión admite edición; se puede confirmar
 * desde EnCurso o Lista cuando no hay una aclaración pendiente.
 *
 * La identidad se resuelve desde la cuenta autenticada (ADR-0038).
 */
@Component({
  selector: 'app-planilla-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, ReportePreview, ReporteWorkspace, TrainingEditor, TrainingProgress, CommandExample, TrainingHistory, ReportEmail, ConfirmationDialog],
  templateUrl: './planilla-page.html',
  styleUrl: './planilla-page.scss',
})
export class PlanillaPage {
  private readonly planillaApi = inject(PlanillaApi);
  readonly dictado = inject(DictadoVoz);

  /** Resuelto por el router desde la cuenta autenticada. */
  readonly alumnoId = input.required<number>();
  readonly trainingEditorOpen = signal(false);
  private readonly trainingEditor = viewChild<TrainingEditor>('fullTrainingEditor');
  private readonly mobileTrainingEditor = viewChild<TrainingEditor>('mobileTrainingEditor');
  private readonly mobileMedia = window.matchMedia?.('(max-width: 767px)');
  readonly mobileLayout = signal(this.mobileMedia?.matches ?? false);
  readonly mobileView = signal<'cards' | 'grid'>('cards');
  readonly mobileDirty = signal(false);
  readonly mobileSaving = signal(false);
  readonly mobileLocked = computed(() => this.mobileDirty() || this.mobileSaving());
  // Mantener montados los borradores si cambia el ancho mientras se edita.
  readonly showMobileCards = computed(() => (this.mobileLayout() && this.mobileView() === 'cards') || this.mobileLocked());

  async changeMobileView(view: 'cards' | 'grid'): Promise<void> {
    if (this.mobileSaving() || this.filaEditandoId() !== null || this.altaAbierta()) return;
    if (view === this.mobileView()) return;
    if (!(await (this.mobileTrainingEditor()?.requestLeave() ?? Promise.resolve(true)))) return;
    this.mobileDirty.set(false);
    this.mobileView.set(view);
  }

  mobileDraftChanged(dirty: boolean): void {
    this.mobileDirty.set(dirty);
    if (dirty) this.dictado.cancelar();
  }

  savedMobileTraining(response: RespuestaSesionActual): void {
    this.mobileDirty.set(false);
    this.mobileSaving.set(false);
    this.sesion.set(response);
    this.mostrarToast('Cambios guardados.');
  }

  removeMobileSeries(id: number): void {
    const row = this.filas().find(row => row.id === id);
    if (row) this.eliminarFila(row);
  }

  openTrainingEditor(): void {
    if (!this.cuadriculaEditable() || this.accionEnCurso() || this.capturaEnCurso() || this.mobileLocked()) return;
    this.dictado.cancelar();
    this.cerrarBorradores();
    this.trainingEditorOpen.set(true);
  }

  closeTrainingEditor(): void {
    this.trainingEditorOpen.set(false);
    this.recargar();
  }

  savedTraining(response: RespuestaSesionActual): void {
    this.sesion.set(response);
    this.trainingEditorOpen.set(false);
  }

  canLeave(): Promise<boolean> { return (this.trainingEditor() ?? this.mobileTrainingEditor())?.requestLeave() ?? Promise.resolve(true); }
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
  readonly confirmationOpen = signal(false);
  private confirmationSessionId: number | null = null;
  private confirmationRowsKey: string | null = null;
  readonly exportacionEnCurso = signal(false);
  readonly vistaPrevia = signal<FormatoVistaPrevia | null>(null);
  readonly pantallaReportes = signal<ModoReporte | null>(null);
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
  private captureMessageTimer: ReturnType<typeof setTimeout> | null = null;
  readonly mensajeBotDescartable = computed(() => {
    const message = this.ultimoMensajeBot();
    return message !== null && !this.sesionViva()?.aclaracion && !/[¿?]/.test(message);
  });

  dismissCaptureMessage(): void {
    if (this.mensajeBotDescartable()) this.clearCaptureMessage();
  }

  private clearCaptureMessage(): void {
    if (this.captureMessageTimer !== null) clearTimeout(this.captureMessageTimer);
    this.captureMessageTimer = null;
    this.ultimoMensajeBot.set(null);
  }

  private showCaptureMessage(message: string): void {
    this.clearCaptureMessage();
    this.ultimoMensajeBot.set(message);
    if (this.mensajeBotDescartable()) {
      this.captureMessageTimer = setTimeout(() => {
        this.captureMessageTimer = null;
        this.dismissCaptureMessage();
      }, 8000);
    }
  }
  readonly capturaEnCurso = signal(false);
  readonly fallaCaptura = signal<FallaApi | null>(null);

  /** Aviso flotante estilo prototipo (cooperación en la esquina inferior derecha). */
  readonly toast = signal<{ texto: string; malo: boolean } | null>(null);
  private temporizadorToast: ReturnType<typeof setTimeout> | null = null;

  readonly sesionViva = computed(() => this.sesion()?.sesion ?? null);
  readonly filas = computed(() => this.sesionViva()?.filas ?? []);
  readonly exerciseGroups = computed(() => groupExerciseRows(
    this.filas(), row => this.etiquetaEjercicio(row), row => row.id,
  ));
  readonly confirmationWarnings = computed(() => this.filas().flatMap(row => {
    const missing: string[] = [];
    if (row.descripcionPendiente) missing.push('ejercicio por aclarar');
    if (row.reps === null) missing.push('repeticiones');
    if (row.pesoKg === null && row.unidad !== 'PesoCorporal') missing.push('peso (o «al cuerpo»)');
    return missing.length ? [`${this.etiquetaEjercicio(row)}, serie ${row.nroSerie}: ${missing.join(' y ')}.`] : [];
  }));
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
    const onMobileChange = (event: MediaQueryListEvent) => this.mobileLayout.set(event.matches);
    this.mobileMedia?.addEventListener('change', onMobileChange);
    effect(() => {
      const id = this.alumnoId();
      untracked(() => {
        this.planillaConfirmada.set(null);
        this.historialPlanillas.set([]);
        this.historialAbierto.set(false);
        this.confirmationOpen.set(false);
        this.nuevoEntrenamiento.set(false);
        this.reporteDesdeHistorial.set(false);
        this.cerrarBorradores();
        this.borradorCaptura.set('');
        this.clearCaptureMessage();
        this.fallaCaptura.set(null);
        this.mensajeExportacion.set(null);
        this.fallaExportacion.set(null);
        this.cargar(id);
        this.rearmarEncuesta(id);
      });
    });

    effect(() => {
      if (this.pantallaReportes()) this.dictado.cancelar();
    });

    inject(DestroyRef).onDestroy(() => {
      this.mobileMedia?.removeEventListener('change', onMobileChange);
      this.dictado.cancelar();
      this.detenerEncuesta();
      this.clearCaptureMessage();
      if (this.temporizadorToast !== null) {
        clearTimeout(this.temporizadorToast);
      }
    });
  }

  // ─── Carga y encuesta (la cuadrícula se actualiza sola, arquitectura S7) ────

  recargar(): void {
    if (this.mobileLocked()) return;
    this.fallaAccion.set(null);
    this.cargar(this.alumnoId());
  }

  private cargar(alumnoId: number): void {
    this.planillaApi.sesionActual(alumnoId).subscribe({
      next: (respuesta) => {
        if (this.trainingEditorOpen() || this.mobileLocked()) return;
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
      || this.trainingEditorOpen()
      || this.mobileLocked()
      || this.altaAbierta()
      || this.planillaConfirmada()
      || this.accionEnCurso()
      || this.capturaEnCurso()
    ) {
      return;
    }

    this.planillaApi.sesionActual(alumnoId).subscribe({
      next: (respuesta) => {
        if (this.trainingEditorOpen() || this.mobileLocked()) return;
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
    if (this.mobileLocked() || this.capturaEnCurso() || this.dictado.escuchando()) {
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

    this.clearCaptureMessage();
    this.capturaEnCurso.set(true);
    this.fallaCaptura.set(null);
    this.planillaApi.capturarTexto(this.alumnoId(), texto).subscribe({
      next: (respuesta) => {
        this.planillaConfirmada.set(null);
        this.nuevoEntrenamiento.set(false);
        this.sesion.set(respuesta.respuesta);
        this.showCaptureMessage(respuesta.mensajeAlumno);
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
    if (this.mobileLocked()) return;
    if (this.capturaEnCurso()) return;
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
    if (this.mobileLocked()) return;
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
      isOneRepMax: false,
      comments: '',
      observations: '',
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

  markOneRepMax(editing: boolean, checked: boolean): void {
    const draft = editing ? this.borradorEdicion : this.borradorAlta;
    draft.update(value => value ? { ...value, isOneRepMax: checked } : value);
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
      case 'comments':
        return { ...borrador, comments: valor };
      case 'observations':
        return { ...borrador, observations: valor };
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
      comments: borrador.comments.trim() || null,
      observations: borrador.observations.trim() || null,
      isOneRepMax: borrador.isOneRepMax,
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

    return [...errores, ...this.validateOneRepMax(borrador)];
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
      isOneRepMax: fila.isOneRepMax ?? false,
      comments: fila.comments ?? '',
      observations: fila.observations ?? '',
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
      comments: borrador.comments.trim() || null,
      observations: borrador.observations.trim() || null,
      isOneRepMax: borrador.isOneRepMax,
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

    return [...errores, ...this.validateOneRepMax(borrador)];
  }

  private validateOneRepMax(draft: BorradorFila): string[] {
    return draft.isOneRepMax && (leerEntero(draft.reps) !== 1 || draft.pesoAlCuerpo || !(parsearDecimalNoNegativo(draft.peso)! > 0))
      ? ['El RM requiere una repetición y un peso mayor que cero en kg.'] : [];
  }

  // ─── Eliminación de fila (dos pasos) ────────────────────────────────────────

  pedirBorrado(filaId: number): void {
    this.filaEnConfirmacion.set(filaId);
  }

  cancelarBorrado(): void {
    this.filaEnConfirmacion.set(null);
  }

  eliminarFila(fila: FilaParcialVista): void {
    if (this.mobileLocked()) return;
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
    if (this.mobileLocked()) return;
    this.borrarTodasEnConfirmacion.set(true);
  }

  cancelarBorradoTotal(): void {
    this.borrarTodasEnConfirmacion.set(false);
  }

  eliminarTodasLasFilas(): void {
    if (this.mobileLocked()) return;
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

  requestConfirmation(): void {
    if (!this.cuadriculaEditable() || !this.filas().length || this.sesionViva()?.aclaracion
      || this.accionEnCurso() || this.capturaEnCurso() || this.mobileLocked()) return;
    this.confirmationSessionId = this.sesionViva()!.id;
    this.confirmationRowsKey = JSON.stringify(this.filas());
    this.fallaAccion.set(null);
    this.confirmationOpen.set(true);
  }

  cancelConfirmation(): void {
    if (this.accionEnCurso()) return;
    this.confirmationOpen.set(false);
    this.fallaAccion.set(null);
  }

  confirmar(): void {
    if (!this.confirmationOpen() || this.accionEnCurso() || this.capturaEnCurso() || this.mobileLocked()) return;
    if (!this.cuadriculaEditable() || !this.filas().length || this.sesionViva()?.aclaracion
      || this.sesionViva()?.id !== this.confirmationSessionId
      || JSON.stringify(this.filas()) !== this.confirmationRowsKey) {
      this.confirmationOpen.set(false);
      this.mostrarToast('El entrenamiento cambió. Revisá la planilla antes de confirmar.', true);
      return;
    }
    const alumnoId = this.alumnoId();
    this.ejecutar(
      this.confirmationWarnings().length ? this.planillaApi.confirmar(alumnoId, true) : this.planillaApi.confirmar(alumnoId),
      (planilla) => {
        this.confirmationOpen.set(false);
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
    this.dictado.cancelar();
    this.planillaConfirmada.set(null);
    this.pantallaReportes.set(null);
    this.vistaPrevia.set(null);
    this.reporteDesdeHistorial.set(false);
    this.historialAbierto.set(false);
    this.nuevoEntrenamiento.set(true);
    this.borradorCaptura.set('');
    this.clearCaptureMessage();
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

  retryHistory(): void {
    if (!this.cargandoHistorial()) this.cargarHistorial(this.alumnoId());
  }

  renamedTraining(updated: PlanillaConfirmadaVista): void {
    this.historialPlanillas.update(entries => entries.map(entry => entry.id === updated.id ? updated : entry));
    if (this.planillaConfirmada()?.id === updated.id) this.planillaConfirmada.set(updated);
  }

  removedTraining(id: number): void {
    this.historialPlanillas.update(entries => entries.filter(entry => entry.id !== id));
    if (this.planillaConfirmada()?.id === id) {
      this.planillaConfirmada.set(null);
      this.pantallaReportes.set(null);
      this.vistaPrevia.set(null);
      this.reporteDesdeHistorial.set(false);
    }
  }

  verPlanillaHistorial(planilla: PlanillaConfirmadaVista): void {
    if (this.mobileLocked()) {
      this.mostrarToast('Guardá o cancelá los cambios de tus series antes de abrir otro entrenamiento.', true);
      return;
    }
    this.planillaConfirmada.set(planilla);
    this.reporteDesdeHistorial.set(true);
    this.historialAbierto.set(false);
    this.pantallaReportes.set('seleccion');
    this.vistaPrevia.set(null);
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
        return { clase: '', texto: 'Sin sesión de entrenamiento' };
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
