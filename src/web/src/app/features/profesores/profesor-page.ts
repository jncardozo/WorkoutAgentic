import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import {
  AlumnoProfesorVista,
  ComandoAltaAlumno,
  ProfesoresApi,
} from '../../core/profesores-api';
import { PlanillaConfirmadaVista, fallaDe } from '../../core/planilla-api';

/**
 * Vista Profesor (arquitectura §7, ADR-0010): SOLO LECTURA de planillas
 * confirmadas. Da de alta alumnos (sin WhatsApp hace falta: el teléfono es
 * opcional) y recorre el historial confirmado de cada uno. La identidad del
 * rol es provisional hasta AuthApi (arquitectura S6).
 */
@Component({
  selector: 'app-profesor-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './profesor-page.html',
  styleUrl: './profesor-page.scss',
  imports: [DatePipe],
})
export class ProfesorPage {
  private readonly profesoresApi = inject(ProfesoresApi);

  // ─── Listado de alumnos ──────────────────────────────────────────────────────
  readonly cargando = signal(true);
  readonly fallaCarga = signal<string | null>(null);
  readonly alumnos = signal<AlumnoProfesorVista[]>([]);

  // ─── Planillas confirmadas de un alumno ──────────────────────────────────────
  readonly alumnoSeleccionado = signal<AlumnoProfesorVista | null>(null);
  readonly planillas = signal<PlanillaConfirmadaVista[]>([]);
  readonly cargandoPlanillas = signal(false);
  readonly fallaPlanillas = signal<string | null>(null);

  // ─── Alta de alumno (POST /api/alumnos) ──────────────────────────────────────
  readonly altaAbierta = signal(false);
  readonly altaNombre = signal('');
  readonly altaTelefono = signal('');
  readonly altaEnCurso = signal(false);
  readonly fallaAlta = signal<string | null>(null);

  constructor() {
    this.cargarAlumnos();
  }

  cargarAlumnos(): void {
    this.cargando.set(true);
    this.fallaCarga.set(null);
    this.profesoresApi.listarAlumnos().subscribe({
      next: (alumnos) => {
        this.alumnos.set(alumnos);
        this.cargando.set(false);
      },
      error: (error: unknown) => {
        this.fallaCarga.set(fallaDe(error).titulo);
        this.cargando.set(false);
      },
    });
  }

  /** Selecciona un alumno y trae su historial confirmado (solo lectura). */
  seleccionar(alumno: AlumnoProfesorVista): void {
    this.alumnoSeleccionado.set(alumno);
    this.planillas.set([]);
    this.fallaPlanillas.set(null);
    this.cargandoPlanillas.set(true);
    this.profesoresApi.planillasDeAlumno(alumno.id).subscribe({
      next: (planillas) => {
        this.planillas.set(planillas);
        this.cargandoPlanillas.set(false);
      },
      error: (error: unknown) => {
        this.fallaPlanillas.set(fallaDe(error).titulo);
        this.cargandoPlanillas.set(false);
      },
    });
  }

  volverALista(): void {
    this.alumnoSeleccionado.set(null);
    this.planillas.set([]);
    this.fallaPlanillas.set(null);
  }

  // ─── Alta ────────────────────────────────────────────────────────────────────

  abrirAlta(): void {
    this.altaAbierta.set(true);
    this.altaNombre.set('');
    this.altaTelefono.set('');
    this.fallaAlta.set(null);
  }

  cerrarAlta(): void {
    this.altaAbierta.set(false);
    this.fallaAlta.set(null);
  }

  /** Nombre obligatorio (etiqueta de listas); teléfono opcional y validable. */
  darDeAlta(): void {
    const nombre = this.altaNombre().trim();
    if (nombre.length === 0) {
      this.fallaAlta.set('Escribí el nombre del alumno.');
      return;
    }

    const telefono = this.altaTelefono().trim();
    if (telefono.length > 0 && !/^[\d\s()+-]+$/.test(telefono)) {
      this.fallaAlta.set('El teléfono admite dígitos, espacios, +, ( ) y -.');
      return;
    }

    const comando: ComandoAltaAlumno = {
      nombre,
      telefonoWa: telefono.length > 0 ? telefono : null,
    };
    this.altaEnCurso.set(true);
    this.fallaAlta.set(null);
    this.profesoresApi.darDeAlta(comando).subscribe({
      next: (creado) => {
        this.altaEnCurso.set(false);
        this.cerrarAlta();
        // La lista nueva entra en orden por nombre; que aparezca de una vez.
        this.alumnos.update((actuales) =>
          [...actuales, this.aVistaLista(creado)].sort((a, b) => a.nombre.localeCompare(b.nombre)),
        );
        this.seleccionar(this.aVistaLista(creado));
      },
      error: (error: unknown) => {
        this.altaEnCurso.set(false);
        this.fallaAlta.set(fallaDe(error).titulo);
      },
    });
  }

  private aVistaLista(creado: { id: number; nombre: string; telefonoWa: string | null }): AlumnoProfesorVista {
    return {
      id: creado.id,
      nombre: creado.nombre,
      telefonoWa: creado.telefonoWa,
      planillasConfirmadas: 0,
      ultimaConfirmadaUtc: null,
    };
  }
}
