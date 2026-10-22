import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

// ─── DTOs del contrato (workoutagentic api, en camelCase por JSON) ────────────
// Espejan los records de Features/PlanillaApi del backend. Los faltantes reales
// viajan como null (regla madre del dominio: nunca inventar datos).

export interface AlumnoVista {
  id: number;
  telefonoWa: string;
}

export interface AclaracionVista {
  campo: string;
  reintento: number;
}

export interface FilaParcialVista {
  id: number;
  ejercicioId: number | null;
  ejercicioCanonico: string | null;
  ejercicioCrudo: string | null;
  descripcionPendiente: boolean;
  nroSerie: number;
  reps: number | null;
  pesoKg: number | null;
  unidad: string | null;
  tiempoSeg: number | null;
  pendienteAclaracion: boolean;
  creadoUtc: string;
}

export interface SesionActualVista {
  id: number;
  estado: string;
  ejercicioActivoId: number | null;
  ejercicioActivoNombre: string | null;
  ordenSerieSiguiente: number;
  aclaracion: AclaracionVista | null;
  ultimaActividadUtc: string;
  filas: FilaParcialVista[];
}

export interface RespuestaSesionActual {
  alumno: AlumnoVista;
  sesion: SesionActualVista | null;
}

/** Captura por texto (ADR-0009): sesión refrescada + respuesta del bot para el banner. */
export interface RespuestaCapturaTexto {
  respuesta: RespuestaSesionActual;
  mensajeAlumno: string;
}

export interface EjercicioCatalogoVista {
  id: number;
  nombreCanonico: string;
  grupoMuscular: string;
  usaPeso: boolean;
  usaTiempo: boolean;
}

export interface ComandoNuevaFila {
  alumnoId: number;
  ejercicioId: number;
  nroSerie: number;
  reps: number | null;
  pesoKg: number | null;
  unidad: string | null;
  tiempoSeg: number | null;
}

/** Edición = reemplazo completo de los campos (ADR-0007 S2). */
export interface ComandoEdicionFila {
  ejercicioId: number | null;
  ejercicioCrudo: string | null;
  nroSerie: number;
  reps: number | null;
  pesoKg: number | null;
  unidad: string | null;
  tiempoSeg: number | null;
}

export interface ComandoConfirmar {
  alumnoId: number;
}

export interface FilaConfirmadaVista {
  orden: number;
  ejercicioCanonical: string;
  nroSerie: number;
  reps: number | null;
  pesoKg: number | null;
  unidad: string | null;
  tiempoSeg: number | null;
  incompleta: boolean;
}

export interface PlanillaConfirmadaVista {
  id: number;
  alumnoId: number;
  sesionId: number;
  confirmadaUtc: string;
  filas: FilaConfirmadaVista[];
}

// ─── Fallas HTTP (ProblemDetails del borde) ──────────────────────────────────

export interface FallaApi {
  estadoHttp: number;
  titulo: string;
  detalle: string | null;
}

/** Convierte cualquier error HTTP a FallaApi (título/detalle del ProblemDetails). */
export function fallaDe(error: unknown): FallaApi {
  if (error instanceof HttpErrorResponse) {
    const problema = (error.error ?? null) as { title?: unknown; detail?: unknown } | null;
    const tituloDelProblema = typeof problema?.title === 'string' && problema.title.length
      ? problema.title
      : null;
    const detalleDelProblema = typeof problema?.detail === 'string' && problema.detail.length
      ? problema.detail
      : null;

    const titulo = tituloDelProblema
      ?? (error.status === 0 ? 'Sin conexión con el servidor' : `Error ${error.status}`);

    return { estadoHttp: error.status, titulo, detalle: detalleDelProblema };
  }

  return { estadoHttp: 0, titulo: 'Error inesperado', detalle: null };
}

// ─── Servicio ────────────────────────────────────────────────────────────────

/**
 * CRUD de parciales + confirmación de la planilla (PlanillaApi del backend).
 * Devuelve la sesión refrescada en cada mutación (la cuadrícula se repinta con
 * una sola respuesta) y el snapshot congelado al confirmar (ADR-0007).
 */
@Injectable({ providedIn: 'root' })
export class PlanillaApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/planillas';
  private readonly baseUrlCaptura = '/api/capturas';

  sesionActual(alumnoId: number): Observable<RespuestaSesionActual> {
    return this.http.get<RespuestaSesionActual>(`${this.baseUrl}/sesion-actual/${alumnoId}`);
  }

  /** Dictado (texto crudo) del alumno: mismo pipeline NLU, sin WhatsApp (ADR-0009). */
  capturarTexto(alumnoId: number, texto: string): Observable<RespuestaCapturaTexto> {
    return this.http.post<RespuestaCapturaTexto>(
      `${this.baseUrlCaptura}/${alumnoId}/texto`,
      { texto },
    );
  }

  catalogo(): Observable<EjercicioCatalogoVista[]> {
    return this.http.get<EjercicioCatalogoVista[]>(`${this.baseUrl}/catalogo-de-ejercicios`);
  }

  agregarFila(comando: ComandoNuevaFila): Observable<RespuestaSesionActual> {
    return this.http.post<RespuestaSesionActual>(`${this.baseUrl}/filas`, comando);
  }

  editarFila(filaId: number, comando: ComandoEdicionFila): Observable<RespuestaSesionActual> {
    return this.http.put<RespuestaSesionActual>(`${this.baseUrl}/filas/${filaId}`, comando);
  }

  eliminarFila(filaId: number): Observable<RespuestaSesionActual> {
    return this.http.delete<RespuestaSesionActual>(`${this.baseUrl}/filas/${filaId}`);
  }

  confirmar(alumnoId: number): Observable<PlanillaConfirmadaVista> {
    const comando: ComandoConfirmar = { alumnoId };
    return this.http.post<PlanillaConfirmadaVista>(`${this.baseUrl}/confirmar`, comando);
  }
}
