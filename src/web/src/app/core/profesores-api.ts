import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { PlanillaConfirmadaVista } from './planilla-api';

// DTOs del contrato profesores (ADR-0010, en camelCase por JSON). Espejan los
// records de Features/Profesores y Features/Alumnos del backend. Los faltantes
// reales viajan como null (regla madre: nunca inventar datos).

/** Fila del listado de alumnos del profesor, con el resumen de lo confirmado. */
export interface AlumnoProfesorVista {
  id: number;
  nombre: string;
  telefonoWa: string | null;
  planillasConfirmadas: number;
  ultimaConfirmadaUtc: string | null;
}

/** Alta explícita de alumno (ADR-0010): el nombre es la etiqueta de listas. */
export interface ComandoAltaAlumno {
  nombre: string;
  telefonoWa: string | null;
}

/** Encabezado de la cuenta creada (el teléfono es opcional desde el pivot). */
export interface AlumnoCreadoVista {
  id: number;
  nombre: string;
  telefonoWa: string | null;
}

// PlanillasConfirmadaVista y sus filas son el shape del snapshot que ya consume
// la vista Alumno: el profesor ve exactamente lo mismo, solo lectura.

// ─── Servicio ────────────────────────────────────────────────────────────────

/**
 * Lecturas del profesor + alta de alumnos (ADR-0010). Solo lectura de planillas
 * confirmadas (regla de roles: el profesor jamás toca sesiones ni parciales).
 * La identidad del rol es provisional hasta AuthApi (arquitectura S6).
 */
@Injectable({ providedIn: 'root' })
export class ProfesoresApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/profesores';

  listarAlumnos(): Observable<AlumnoProfesorVista[]> {
    return this.http.get<AlumnoProfesorVista[]>(`${this.baseUrl}/alumnos`);
  }

  planillasDeAlumno(alumnoId: number): Observable<PlanillaConfirmadaVista[]> {
    return this.http.get<PlanillaConfirmadaVista[]>(
      `${this.baseUrl}/alumnos/${alumnoId}/planillas`,
    );
  }

  /** Alta explícita: POST /api/alumnos (endpoint de escritura de cuentas). */
  darDeAlta(comando: ComandoAltaAlumno): Observable<AlumnoCreadoVista> {
    return this.http.post<AlumnoCreadoVista>('/api/alumnos', comando);
  }
}
