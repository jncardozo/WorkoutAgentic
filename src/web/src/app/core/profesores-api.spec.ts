import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';

import { ProfesoresApi, type AlumnoProfesorVista } from './profesores-api';

function alumno(sobre: Partial<AlumnoProfesorVista> = {}): AlumnoProfesorVista {
  return {
    id: 2,
    nombre: 'Bruno García',
    telefonoWa: null,
    planillasConfirmadas: 1,
    ultimaConfirmadaUtc: '2026-10-22T14:00:00Z',
    ...sobre,
  };
}

describe('ProfesoresApi', () => {
  let api: ProfesoresApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    api = TestBed.inject(ProfesoresApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lista los alumnos del profesor (GET /api/profesores/alumnos)', () => {
    let alumnos: AlumnoProfesorVista[] | undefined;
    api.listarAlumnos().subscribe((r) => (alumnos = r));

    const peticion = http.expectOne('/api/profesores/alumnos');
    expect(peticion.request.method).toBe('GET');
    peticion.flush([alumno(), alumno({ id: 1, telefonoWa: '5491100112233', planillasConfirmadas: 0 })]);

    expect(alumnos?.length).toBe(2);
    expect(alumnos?.[0].planillasConfirmadas).toBe(1);
  });

  it('trae el historial confirmado de un alumno (GET planillas del alumno)', () => {
    let planillas: unknown[] | undefined;
    api.planillasDeAlumno(7).subscribe((r) => (planillas = r));

    const peticion = http.expectOne('/api/profesores/alumnos/7/planillas');
    expect(peticion.request.method).toBe('GET');
    peticion.flush([]);

    expect(planillas).toEqual([]);
  });

  it('da de alta a un alumno con teléfono opcional (POST /api/alumnos)', () => {
    let creado: { id: number; nombre: string; telefonoWa: string | null } | undefined;
    api.darDeAlta({ nombre: 'Nadia Sosa', telefonoWa: null }).subscribe((r) => (creado = r));

    const peticion = http.expectOne('/api/alumnos');
    expect(peticion.request.method).toBe('POST');
    expect(peticion.request.body).toEqual({ nombre: 'Nadia Sosa', telefonoWa: null });
    peticion.flush({ id: 3, nombre: 'Nadia Sosa', telefonoWa: null });

    expect(creado?.telefonoWa).toBeNull();
  });
});
