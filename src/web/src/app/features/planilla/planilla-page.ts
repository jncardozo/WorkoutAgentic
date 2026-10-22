import { DatePipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  effect,
  inject,
  input,
  signal,
  untracked,
} from '@angular/core';
import { Observable } from 'rxjs';
import { Router } from '@angular/router';
import {
  ComandoEdicionFila,
  ComandoNuevaFila,
  EjercicioCatalogoVista,
  FallaApi,
  FilaParcialVista,
  PlanillaApi,
  PlanillaConfirmadaVista,
  RespuestaSesionActual,
  fallaDe,
} from '../../core/planilla-api';
import { DictadoVoz } from './dictado-voz';

/** Borrador local de una fila (alta o edición); los números viajan como texto. */
interface BorradorFila {
  // null = fila de descripción libre (solo edición; el alta exige catálogo).
  ejercicioId: number | null;
  ejercicioCrudo: string;
  serie: string;
  reps: string;
  peso: string;
  pesoAlCuerpo: boolean;
  tiempo: string;
}

/** Campos del borrador que se editan como texto libre en la cuadrícula. */
type CampoTextoBorrador = 'ejercicioCrudo' | 'serie' | 'reps' | 'peso' | 'tiempo';

const INTERVALO_ENCUESTA_MS = 10_000;

/**
 * Vista Alumno · Planilla (arquitectura §6): cuadrícula editable de parciales
 * capturados por WhatsApp + confirmación (snapshot). Reglas de edición en
 * ADR-0007: solo mientras el chat no tiene la palabra; el alta exige fila
 * completa; confirmar solo cuando la sesión está en Lista.
 *
 * La identificación del alumno es provisional (ruta + localStorage) hasta
 * AuthApi: la vista queda igual, solo cambia el borde.
 */
@Component({
  selector: 'app-planilla-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe],
  templateUrl: './planilla-page.html',
  styleUrl: './planilla-page.scss',
})
export class PlanillaPage {
  private readonly planillaApi = inject(PlanillaApi);
  private readonly router = inject(Router);
  readonly dictado = inject(DictadoVoz);

  /** Viene de la ruta `alumno/:alumnoId/planilla` (input binding de rutas). */
  readonly alumnoId = input.required<number>();

  readonly cargando = signal(true);
  readonly sesion = signal<RespuestaSesionActual | null>(null);
  readonly planillaConfirmada = signal<PlanillaConfirmadaVista | null>(null);
  readonly catalogo = signal<EjercicioCatalogoVista[]>([]);
  readonly falloCatalogo = signal(false);
  readonly fallaCarga = signal<FallaApi | null>(null);
  readonly fallaAccion = signal<FallaApi | null>(null);
  readonly accionEnCurso = signal(false);

  readonly filaEditandoId = signal<number | null>(null);
  readonly borradorEdicion = signal<BorradorFila | null>(null);
  readonly altaAbierta = signal(false);
  readonly borradorAlta = signal<BorradorFila | null>(null);

  /** Borrado en dos pasos: primer click arma la confirmación. */
  readonly filaEnConfirmacion = signal<number | null>(null);

  /** Panel de dictado (ADR-0009): borrador del texto + última respuesta del bot. */
  readonly borradorCaptura = signal('');
  readonly ultimoMensajeBot = signal<string | null>(null);
  readonly capturaEnCurso = signal(false);
  readonly fallaCaptura = signal<FallaApi | null>(null);

  readonly sesionViva = computed(() => this.sesion()?.sesion ?? null);
  readonly filas = computed(() => this.sesionViva()?.filas ?? []);
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
        this.cerrarBorradores();
        this.borradorCaptura.set('');
        this.ultimoMensajeBot.set(null);
        this.fallaCaptura.set(null);
        this.cargar(id);
        this.rearmarEncuesta(id);
      });
    });

    inject(DestroyRef).onDestroy(() => this.detenerEncuesta());
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

    this.planillaApi.catalogo().subscribe({
      next: (ejercicios) => {
        this.catalogo.set(ejercicios);
        this.falloCatalogo.set(false);
      },
      error: () => this.falloCatalogo.set(true),
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
   * Dictar tras una confirmación abre una carga nueva (la congelada cede).
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
      ejercicioId: this.sesionViva()?.ejercicioActivoId ?? this.catalogo()[0]?.id ?? null,
      ejercicioCrudo: '',
      serie: String(this.serieSugerida()),
      reps: '',
      peso: '',
      pesoAlCuerpo: false,
      tiempo: '',
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
      case 'tiempo':
        return { ...borrador, tiempo: valor };
    }
  }

  guardarAlta(): void {
    const borrador = this.borradorAlta();
    if (!borrador) {
      return;
    }

    const ejercicio = this.ejercicioDelCatalogo(borrador.ejercicioId);
    const errores = this.validarAlta(borrador, ejercicio);
    if (ejercicio === null || errores.length > 0) {
      return this.mostrarFallaLocal(errores);
    }

    const reps = parsearEnteroPositivo(borrador.reps);
    const pesoKg = borrador.pesoAlCuerpo ? null : parsearDecimalNoNegativo(borrador.peso);
    const tiempoSeg = parsearEnteroPositivo(borrador.tiempo);

    // Fila completa según el TIPO de ejercicio (misma regla que CampoFaltante).
    const comando: ComandoNuevaFila = ejercicio.usaTiempo
      ? {
          alumnoId: this.alumnoId(), ejercicioId: ejercicio.id, nroSerie: Number(borrador.serie),
          reps: null, pesoKg: null, unidad: 'Tiempo', tiempoSeg,
        }
      : ejercicio.usaPeso
        ? {
            alumnoId: this.alumnoId(), ejercicioId: ejercicio.id, nroSerie: Number(borrador.serie),
            reps,
            pesoKg,
            unidad: borrador.pesoAlCuerpo ? 'PesoCorporal' : 'Kg',
            tiempoSeg: null,
          }
        : {
            alumnoId: this.alumnoId(), ejercicioId: ejercicio.id, nroSerie: Number(borrador.serie),
            reps, pesoKg: null, unidad: null, tiempoSeg: null,
          };

    this.ejecutar(
      this.planillaApi.agregarFila(comando),
      (respuesta) => {
        this.sesion.set(respuesta);
        this.cerrarAlta();
      },
    );
  }

  private validarAlta(borrador: BorradorFila, ejercicio: EjercicioCatalogoVista | null): string[] {
    const errores: string[] = [];

    if (ejercicio === null) {
      errores.push('Elegí el ejercicio de la fila.');
    }

    if (leerEntero(borrador.serie) === null) {
      errores.push('El número de serie va de 1 en adelante.');
    }

    if (ejercicio === null) {
      return errores;
    }

    if (ejercicio.usaTiempo) {
      if (leerEntero(borrador.tiempo) === null) {
        errores.push('La fila necesita el tiempo en segundos.');
      }
      return errores;
    }

    if (leerEntero(borrador.reps) === null) {
      errores.push('La fila necesita las repeticiones.');
    }
    if (ejercicio.usaPeso && !borrador.pesoAlCuerpo && parsearDecimalNoNegativo(borrador.peso) === null) {
      errores.push('La fila necesita el peso (o marcá «al cuerpo»).');
    }
    return errores;
  }

  // ─── Edición de fila (PUT: reemplazo completo, ADR-0007 S2) ─────────────────

  abrirEdicion(fila: FilaParcialVista): void {
    this.altaAbierta.set(false);
    this.borradorAlta.set(null);
    this.filaEnConfirmacion.set(null);
    this.fallaAccion.set(null);
    const pesoAlCuerpo = fila.unidad === 'PesoCorporal';
    this.filaEditandoId.set(fila.id);
    this.borradorEdicion.set({
      ejercicioId: fila.ejercicioId,
      ejercicioCrudo: fila.ejercicioCrudo ?? '',
      serie: String(fila.nroSerie),
      reps: fila.reps === null ? '' : String(fila.reps),
      peso: pesoAlCuerpo ? '' : fila.pesoKg === null ? '' : String(fila.pesoKg),
      pesoAlCuerpo,
      tiempo: fila.tiempoSeg === null ? '' : String(fila.tiempoSeg),
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

    const sinEjercicio = borrador.ejercicioId === null;
    const ejercicio = sinEjercicio ? null : this.ejercicioDelCatalogo(borrador.ejercicioId);
    const pesoKg = sinEjercicio || borrador.pesoAlCuerpo
      ? null
      : parsearDecimalNoNegativo(borrador.peso);
    const tiempoSeg = parsearEnteroPositivo(borrador.tiempo);

    // Peso en kg siempre (ADR-0004 S6: planilla 100 % en kg); «al cuerpo» lleva
    // su unidad; peso vacío limpia el campo; el tiempo etiqueta Unidad.Tiempo.
    const unidad = sinEjercicio
      ? null
      : borrador.pesoAlCuerpo
        ? 'PesoCorporal'
        : pesoKg !== null
          ? 'Kg'
          : tiempoSeg !== null
            ? 'Tiempo'
            : null;

    const comando: ComandoEdicionFila = {
      ejercicioId: sinEjercicio ? null : (ejercicio?.id ?? null),
      ejercicioCrudo: sinEjercicio ? (borrador.ejercicioCrudo.trim() || null) : null,
      nroSerie: Number(borrador.serie),
      reps: leerEntero(borrador.reps),
      pesoKg,
      unidad,
      tiempoSeg,
    };

    this.ejecutar(
      this.planillaApi.editarFila(fila.id, comando),
      (respuesta) => {
        this.sesion.set(respuesta);
        this.cancelarEdicion();
      },
    );
  }

  private validarEdicion(borrador: BorradorFila): string[] {
    const errores: string[] = [];

    if (borrador.ejercicioId === null && borrador.ejercicioCrudo.trim().length === 0) {
      errores.push('Elegí un ejercicio del catálogo o escribí la descripción de la fila.');
    } else if (borrador.ejercicioId !== null && this.ejercicioDelCatalogo(borrador.ejercicioId) === null) {
      errores.push('El ejercicio elegido ya no está en el catálogo.');
    }

    if (leerEntero(borrador.serie) === null) {
      errores.push('El número de serie va de 1 en adelante.');
    }

    if (borrador.reps.length > 0 && leerEntero(borrador.reps) === null) {
      errores.push('Las repeticiones van de 1 en adelante.');
    }

    if (borrador.tiempo.length > 0 && leerEntero(borrador.tiempo) === null) {
      errores.push('El tiempo va de 1 segundo en adelante.');
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
    );
  }

  // ─── Confirmación (snapshot write-once, ADR-0007 S4) ────────────────────────

  confirmar(): void {
    const alumnoId = this.alumnoId();
    this.ejecutar(
      this.planillaApi.confirmar(alumnoId),
      (planilla) => {
        this.planillaConfirmada.set(planilla);
        // La sesión confirmada deja de ser hablante: la cuadrícula queda vacía.
        this.sesion.update((respuesta) => (respuesta ? { ...respuesta, sesion: null } : respuesta));
      },
    );
  }

  irAEntrada(): void {
    this.router.navigate(['/entrada']);
  }

  // ─── Ayudas de presentación ────────────────────────────────────────────────

  etiquetaEjercicio(fila: FilaParcialVista): string {
    return fila.ejercicioCanonico ?? fila.ejercicioCrudo ?? 'sin ejercicio';
  }

  pesoTexto(fila: FilaParcialVista): string {
    if (fila.unidad === 'PesoCorporal') {
      return 'al cuerpo';
    }
    return fila.pesoKg === null ? '—' : `${fila.pesoKg} kg`;
  }

  tiempoTexto(fila: FilaParcialVista): string {
    return fila.tiempoSeg === null ? '—' : `${fila.tiempoSeg} s`;
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
        return { clase: 'badge-aviso', texto: 'Falta un dato' };
      case 'Expirada':
        return { clase: 'badge-aviso', texto: 'Pausada' };
      default:
        return { clase: '', texto: 'Sin sesión' };
    }
  }

  ejercicioDelCatalogo(id: number | null): EjercicioCatalogoVista | null {
    return id === null ? null : (this.catalogo().find((e) => e.id === id) ?? null);
  }

  /** Id del borrador como texto para el `<option>` del selector (global String no existe). */
  idEjercicioTexto(id: number | null): string {
    return id === null ? '' : String(id);
  }

  // ─── Ejecución común de comandos ────────────────────────────────────────────

  private ejecutar<T extends RespuestaSesionActual | PlanillaConfirmadaVista>(
    peticion: Observable<T>,
    alTerminar: (respuesta: T) => void,
  ): void {
    this.accionEnCurso.set(true);
    this.fallaAccion.set(null);
    peticion.subscribe({
      next: (respuesta) => {
        alTerminar(respuesta);
        this.accionEnCurso.set(false);
      },
      error: (error: unknown) => {
        this.fallaAccion.set(fallaDe(error));
        this.accionEnCurso.set(false);
      },
    });
  }

  private mostrarFallaLocal(errores: string[]): void {
    const detalles = errores.filter((mensaje) => mensaje.length > 0);
    this.fallaAccion.set({
      estadoHttp: 0,
      titulo: 'Revisá los datos de la fila',
      detalle: detalles.length > 0 ? detalles.join(' ') : null,
    });
  }

  /** Al cambiar el ejercicio del borrador se limpian los campos que su tipo no lleva. */
  cambiarEjercicioBorrador(tipo: 'alta' | 'edicion', valor: string): void {
    const id = valor.length === 0 ? null : Number(valor);
    const ejercicio = this.ejercicioDelCatalogo(id);

    const anclar = (borrador: BorradorFila | null): BorradorFila | null => {
      if (!borrador) {
        return borrador;
      }
      const base: BorradorFila = {
        ...borrador,
        ejercicioId: id,
        peso: ejercicio?.usaTiempo || id === null ? '' : borrador.peso,
        pesoAlCuerpo:
          id === null ? false : ejercicio?.usaTiempo ? false : borrador.pesoAlCuerpo,
        reps: ejercicio?.usaTiempo ? '' : borrador.reps,
        tiempo: !id || !ejercicio?.usaTiempo ? '' : borrador.tiempo,
      };
      return base;
    };

    if (tipo === 'alta') {
      this.borradorAlta.update(anclar);
    } else {
      this.borradorEdicion.update(anclar);
    }
  }

  private cerrarBorradores(): void {
    this.filaEditandoId.set(null);
    this.borradorEdicion.set(null);
    this.altaAbierta.set(false);
    this.borradorAlta.set(null);
    this.filaEnConfirmacion.set(null);
    this.fallaAccion.set(null);
  }
}

/** Entero estricto ≥ 1 (para serie, reps y tiempo). */
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
