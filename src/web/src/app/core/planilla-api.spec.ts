import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';

import {
  FilaParcialVista,
  PlanillaApi,
  RespuestaCapturaTexto,
  RespuestaSesionActual,
  fallaDe,
} from './planilla-api';

function filaParcial(sobre: Partial<FilaParcialVista> = {}): FilaParcialVista {
  return {
    id: 5,
    ejercicioId: 11,
    ejercicioCanonico: 'Press banca',
    ejercicioCrudo: null,
    descripcionPendiente: false,
    nroSerie: 1,
    reps: 8,
    pesoKg: 45,
    unidad: 'Kg',
    tiempoSeg: null,
    pendienteAclaracion: false,
    creadoUtc: '2026-10-21T15:00:00Z',
    ...sobre,
  };
}

function respuesta(): RespuestaSesionActual {
  return {
    alumno: { id: 1, nombre: 'Lucía Elliott' },
    sesion: {
      id: 3,
      estado: 'EnCurso',
      ejercicioActivoId: 11,
      ejercicioActivoNombre: 'Press banca',
      ordenSerieSiguiente: 2,
      aclaracion: null,
      ultimaActividadUtc: '2026-10-21T15:00:00Z',
      filas: [filaParcial()],
    },
  };
}

describe('PlanillaApi', () => {
  let api: PlanillaApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    api = TestBed.inject(PlanillaApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('consulta si el correo está configurado antes de habilitar el envío', () => {
    let configured: boolean | undefined;
    api.emailConfigurationStatus().subscribe(status => configured = status.configured);
    const request = http.expectOne('/api/planillas/correo/estado');
    expect(request.request.method).toBe('GET');
    request.flush({ configured: false });
    expect(configured).toBe(false);
  });

  it('pide la sesión actual del alumno', () => {
    let respuestaObtenida: RespuestaSesionActual | undefined;
    api.sesionActual(7).subscribe((r) => (respuestaObtenida = r));

    const peticion = http.expectOne('/api/planillas/sesion-actual/7');
    expect(peticion.request.method).toBe('GET');
    peticion.flush(respuesta());

    expect(respuestaObtenida?.alumno.id).toBe(1);
    expect(respuestaObtenida?.sesion?.filas[0].pesoKg).toBe(45);
  });

  it('agrega una fila con POST y el comando completo', () => {
    const comando = {
      alumnoId: 1,
      ejercicioId: 11,
      ejercicioNombre: 'Press banca',
      nroSerie: 2,
      reps: 8,
      pesoKg: 45,
      unidad: 'Kg',
      tiempoSeg: null,
    };
    api.agregarFila(comando).subscribe();

    const peticion = http.expectOne('/api/planillas/filas');
    expect(peticion.request.method).toBe('POST');
    expect(peticion.request.body).toEqual(comando);
    peticion.flush(respuesta());
  });

  it('edita una fila con PUT de reemplazo completo', () => {
    const comando = {
      ejercicioId: null,
      ejercicioCrudo: 'hice quedadas',
      nroSerie: 1,
      reps: 8,
      pesoKg: null,
      unidad: null,
      tiempoSeg: null,
    };
    api.editarFila(12, comando).subscribe();

    const peticion = http.expectOne('/api/planillas/filas/12');
    expect(peticion.request.method).toBe('PUT');
    expect(peticion.request.body).toEqual(comando);
    peticion.flush(respuesta());
  });

  it('elimina una fila con DELETE', () => {
    api.eliminarFila(12).subscribe();

    const peticion = http.expectOne('/api/planillas/filas/12');
    expect(peticion.request.method).toBe('DELETE');
    peticion.flush(respuesta());
  });

  it('confirma con POST y envía el alumno', () => {
    let planilla: { alumnoId?: number } | undefined;
    api.confirmar(1).subscribe((p) => (planilla = p));

    const peticion = http.expectOne('/api/planillas/confirmar');
    expect(peticion.request.method).toBe('POST');
    expect(peticion.request.body).toEqual({ alumnoId: 1 });
    peticion.flush({ id: 9, alumnoId: 1, sesionId: 3, confirmadaUtc: '2026-10-21T16:00:00Z', filas: [] });

    expect(planilla?.alumnoId).toBe(1);
  });

  it('envía el dictado al endpoint de captura por texto', () => {
    let captura: RespuestaCapturaTexto | undefined;
    api.capturarTexto(7, 'prensa segunda serie 10 con 45').subscribe((r) => (captura = r));

    const peticion = http.expectOne('/api/capturas/7/texto');
    expect(peticion.request.method).toBe('POST');
    expect(peticion.request.body).toEqual({ texto: 'prensa segunda serie 10 con 45' });
    peticion.flush({ respuesta: respuesta(), mensajeAlumno: 'Serie 1 de Prensa anotada.' });

    expect(captura?.mensajeAlumno).toBe('Serie 1 de Prensa anotada.');
    expect(captura?.respuesta.alumno.id).toBe(1);
  });

  it('mapea un ProblemDetails a FallaApi con título y detalle', () => {
    let errorCapturado: unknown;
    api.sesionActual(1).subscribe({ error: (error: unknown) => (errorCapturado = error) });

    const peticion = http.expectOne('/api/planillas/sesion-actual/1');
    peticion.flush(
      { title: 'Estado inválido', detail: 'La fila no admite ese cambio.' },
      { status: 409, statusText: 'Conflict' },
    );

    const falla = fallaDe(errorCapturado);
    expect(falla.estadoHttp).toBe(409);
    expect(falla.titulo).toBe('Estado inválido');
    expect(falla.detalle).toBe('La fila no admite ese cambio.');
  });

  it('queda con un título genérico cuando el error no trae ProblemDetails', () => {
    let errorCapturado: unknown;
    api.sesionActual(1).subscribe({ error: (error: unknown) => (errorCapturado = error) });

    const peticion = http.expectOne('/api/planillas/sesion-actual/1');
    peticion.flush(null, { status: 500, statusText: 'Server Error' });

    expect(fallaDe(errorCapturado).titulo).toBe('Error 500');
  });
});
