import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';

import {
  ComandoEdicionFila,
  EjercicioCatalogoVista,
  FilaParcialVista,
  PlanillaApi,
  PlanillaConfirmadaVista,
  RespuestaCapturaTexto,
  RespuestaSesionActual,
  SesionActualVista,
} from '../../core/planilla-api';
import { DictadoVoz, FABRICA_RECONOCEDOR } from './dictado-voz';
import { PlanillaPage } from './planilla-page';

const CATALOGO: EjercicioCatalogoVista[] = [
  { id: 11, nombreCanonico: 'Press banca', grupoMuscular: 'Pecho', usaPeso: true, usaTiempo: false },
  { id: 22, nombreCanonico: 'Plancha', grupoMuscular: 'Core', usaPeso: false, usaTiempo: true },
  { id: 33, nombreCanonico: 'Dominadas', grupoMuscular: 'Espalda', usaPeso: false, usaTiempo: false },
];

function fila(sobre: Partial<FilaParcialVista> = {}): FilaParcialVista {
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

function sesion(sobre: Partial<SesionActualVista> = {}): SesionActualVista {
  return {
    id: 3,
    estado: 'EnCurso',
    ejercicioActivoId: null,
    ejercicioActivoNombre: null,
    ordenSerieSiguiente: 2,
    aclaracion: null,
    ultimaActividadUtc: '2026-10-21T15:00:00Z',
    filas: [fila()],
    ...sobre,
  };
}

function respuesta(sesionActual: SesionActualVista | null): RespuestaSesionActual {
  return {
    alumno: { id: 1, nombre: 'Lucía Elliott' },
    sesion: sesionActual,
  };
}

const PLANILLA_CONFIRMADA: PlanillaConfirmadaVista = {
  id: 9,
  alumnoId: 1,
  sesionId: 3,
  confirmadaUtc: '2026-10-21T16:00:00Z',
  filas: [
    {
      orden: 1,
      ejercicioCanonical: 'Press banca',
      nroSerie: 1,
      reps: 8,
      pesoKg: 45,
      unidad: 'Kg',
      tiempoSeg: null,
      incompleta: false,
    },
    {
      orden: 2,
      ejercicioCanonical: 'hice quedadas',
      nroSerie: 2,
      reps: null,
      pesoKg: null,
      unidad: null,
      tiempoSeg: null,
      incompleta: true,
    },
  ],
};

interface StubPlanilla {
  sesionActual: ReturnType<typeof vi.fn>;
  historial: ReturnType<typeof vi.fn>;
  catalogo: ReturnType<typeof vi.fn>;
  capturarTexto: ReturnType<typeof vi.fn>;
  agregarFila: ReturnType<typeof vi.fn>;
  editarFila: ReturnType<typeof vi.fn>;
  eliminarFila: ReturnType<typeof vi.fn>;
  confirmar: ReturnType<typeof vi.fn>;
}

const RESPUESTA_CAPTURA: RespuestaCapturaTexto = {
  respuesta: respuesta(sesion()),
  mensajeAlumno: 'Serie 1 de Press banca anotada: 8 reps con 45 kg. "terminé" cuando cierres.',
};

function stubPlanilla(
  respuestaInicial: RespuestaSesionActual,
  sobres: Partial<StubPlanilla> = {},
): StubPlanilla {
  return {
    sesionActual: vi.fn(() => of(respuestaInicial)),
    historial: vi.fn(() => of([])),
    catalogo: vi.fn(() => of(CATALOGO)),
    capturarTexto: vi.fn(() => of(RESPUESTA_CAPTURA)),
    agregarFila: vi.fn(() => of(respuestaInicial)),
    editarFila: vi.fn(() => of(respuestaInicial)),
    eliminarFila: vi.fn(() => of(respuestaInicial)),
    confirmar: vi.fn(() => of(PLANILLA_CONFIRMADA)),
    ...sobres,
  };
}

/**
 * Monta la página. `dictado` (cuando va) reemplaza el servicio real por un
 * falso con el mismo shape; sin él, la fábrica nula deja el voto sin soporte
 * (jsdom no tiene Web Speech API), igual que un Firefox.
 */
async function montar(
  stub: StubPlanilla,
  dictado: object | null = null,
): Promise<ComponentFixture<PlanillaPage>> {
  TestBed.configureTestingModule({
    imports: [PlanillaPage],
    providers: [
      provideRouter([]),
      { provide: PlanillaApi, useValue: stub },
      { provide: FABRICA_RECONOCEDOR, useValue: () => null },
      ...(dictado ? [{ provide: DictadoVoz, useValue: dictado }] : []),
    ],
  });
  const fixture = TestBed.createComponent(PlanillaPage);
  fixture.componentRef.setInput('alumnoId', 1);
  await fixture.whenStable();
  fixture.detectChanges();
  return fixture;
}

function texto(fixture: ComponentFixture<PlanillaPage>): string {
  return (fixture.nativeElement as HTMLElement).textContent ?? '';
}

function boton(fixture: ComponentFixture<PlanillaPage>, buscado: string): HTMLButtonElement {
  const encontrado = [...(fixture.nativeElement as HTMLElement).querySelectorAll('button')].find(
    (botonActual) => (botonActual.textContent ?? '').includes(buscado),
  );
  if (!encontrado) {
    throw new Error(`No está el botón: ${buscado}`);
  }
  return encontrado;
}

function escribir(fixture: ComponentFixture<PlanillaPage>, selector: string, valor: string): void {
  const entrada = (fixture.nativeElement as HTMLElement).querySelector(selector) as HTMLInputElement;
  entrada.value = valor;
  entrada.dispatchEvent(new Event('input'));
}

describe('PlanillaPage', () => {
  it('sin sesión muestra el aviso de dictar el primer ejercicio y no cuadrícula', async () => {
    const fixture = await montar(stubPlanilla(respuesta(null)));

    expect(texto(fixture)).toContain('No tenés una carga en curso');
    expect(fixture.nativeElement.querySelector('table.cuadricula')).toBeNull();
  });

  it('dictar envía el texto y repinta la cuadrícula con la respuesta del bot', async () => {
    const stub = stubPlanilla(respuesta(null), {
      capturarTexto: vi.fn(() => of(RESPUESTA_CAPTURA)),
    });
    const fixture = await montar(stub);

    escribir(fixture, 'textarea.entrada-captura', 'prensa 8 con 45');
    boton(fixture, 'Enviar').click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(stub.capturarTexto).toHaveBeenCalledWith(1, 'prensa 8 con 45');
    const cuerpo = texto(fixture);
    expect(cuerpo).toContain('Serie 1 de Press banca anotada');
    expect(fixture.nativeElement.querySelector('table.cuadricula')).not.toBeNull();
  });

  it('enviar sin texto no llama al API y explica qué falta', async () => {
    const stub = stubPlanilla(respuesta(null));
    const fixture = await montar(stub);

    boton(fixture, 'Enviar').click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(stub.capturarTexto).not.toHaveBeenCalled();
    expect(texto(fixture)).toContain('Sin texto');
  });

  it('en curso muestra las filas con sus valores', async () => {
    const filas = [
      fila({ id: 5, nroSerie: 1, reps: 8, pesoKg: 45 }),
      // El tiempo dejó de ser dato de la cuadrícula (ADR-0014): una fila por
      // tiempo solo muestra ejercicio/serie/reps.
      fila({
        id: 6,
        nroSerie: 2,
        ejercicioId: 22,
        ejercicioCanonico: 'Plancha',
        reps: null,
        pesoKg: null,
        unidad: 'Tiempo',
        tiempoSeg: 45,
      }),
    ];
    const fixture = await montar(stubPlanilla(respuesta(sesion({ filas }))));

    expect(fixture.nativeElement.querySelectorAll('table.cuadricula tbody tr').length).toBe(2);
    const cuerpo = texto(fixture);
    expect(cuerpo).toContain('Press banca');
    expect(cuerpo).toContain('45.0 kg');
    expect(cuerpo).not.toContain('Tiempo');
    expect(cuerpo).not.toContain('45 s');
  });

  it('con aclaración viva congela la cuadrícula sin banner de pendientes', async () => {
    const fixture = await montar(
      stubPlanilla(respuesta(sesion({
        estado: 'EsperandoAclaracion',
        aclaracion: { campo: 'ejercicio', reintento: 0 },
        filas: [fila({ pendienteAclaracion: true })],
      }))),
    );
    const botones = [...(fixture.nativeElement as HTMLElement).querySelectorAll('button')];

    // La pregunta del bot vive en el panel de dictado y las filas marcan su
    // chip: no hay banner duplicado de "te falta responder" (ADR-0014).
    expect(texto(fixture)).not.toContain('Te falta responder');
    expect(texto(fixture)).toContain('pendiente del bot');
    expect(botones.some((b) => (b.textContent ?? '').includes('Editar'))).toBe(false);
    expect(botones.some((b) => (b.textContent ?? '').includes('Confirmar'))).toBe(false);
  });

  it('confirmar desde Lista congela y muestra la planilla post-sesión', async () => {
    const fixture = await montar(stubPlanilla(respuesta(sesion({ estado: 'Lista' }))));

    boton(fixture, 'Confirmar planilla').click();
    await fixture.whenStable();
    fixture.detectChanges();

    const cuerpo = texto(fixture);
    expect(cuerpo).toContain('Planilla confirmada');
    expect(cuerpo).toContain('incompleta');
    expect(fixture.nativeElement.querySelector('table.cuadricula')).not.toBeNull();
  });

  it('una fila alta incompleta no llama al API y explica qué falta', async () => {
    const stub = stubPlanilla(respuesta(sesion({ estado: 'EnCurso' })));
    const fixture = await montar(stub);

    boton(fixture, '+ Agregar fila').click();
    await fixture.whenStable();
    fixture.detectChanges();

    boton(fixture, 'Guardar fila').click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(stub.agregarFila).not.toHaveBeenCalled();
    expect(texto(fixture)).toContain('La fila necesita las repeticiones.');
    expect(texto(fixture)).toContain('La fila necesita el peso');
  });

  it('editar guarda el reemplazo completo y repinta la cuadrícula', async () => {
    const respuestaEdicion = respuesta(sesion({ filas: [fila({ id: 5, pesoKg: 42.5 })] }));
    const stub = stubPlanilla(respuesta(sesion({ filas: [fila({ id: 5 })] })), {
      editarFila: vi.fn(() => of(respuestaEdicion)),
    });
    const fixture = await montar(stub);

    boton(fixture, 'Editar').click();
    await fixture.whenStable();
    fixture.detectChanges();

    escribir(fixture, 'input[aria-label="Peso en kg"]', '42.5');
    boton(fixture, 'Guardar').click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(stub.editarFila).toHaveBeenCalledTimes(1);
    const [filaId, comando] = stub.editarFila.mock.calls[0] as [number, ComandoEdicionFila];
    expect(filaId).toBe(5);
    expect(comando).toEqual({
      ejercicioId: 11,
      ejercicioCrudo: null,
      nroSerie: 1,
      reps: 8,
      pesoKg: 42.5,
      unidad: 'Kg',
      tiempoSeg: null,
    });
    expect(texto(fixture)).toContain('42.5 kg');
  });

  it('confirmar usa el alumno de la ruta', async () => {
    const stub = stubPlanilla(respuesta(sesion({ estado: 'Lista' })));
    const fixture = await montar(stub);

    boton(fixture, 'Confirmar planilla').click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(stub.confirmar).toHaveBeenCalledWith(1);
  });

  // ─── Dictado por voz (ADR-0011) ───────────────────────────────────────────────

  it('el micrófono vuelca lo dictado al borrador y el envío sigue el pipeline normal', async () => {
    const entregado: ((texto: string) => void)[] = [];
    const dictado = {
      soportado: true,
      escuchando: signal(false),
      parcial: signal(''),
      fallo: signal<string | null>(null),
      escuchar: vi.fn((callback: (texto: string) => void) => entregado.push(callback)),
      detener: vi.fn(),
    };
    const stub = stubPlanilla(respuesta(null), {
      capturarTexto: vi.fn(() => of(RESPUESTA_CAPTURA)),
    });
    const fixture = await montar(stub, dictado);

    const transcript = boton(fixture, 'Dictar');
    transcript.click();
    await fixture.whenStable();
    fixture.detectChanges();

    entregado[0]('prensa 10 con 40'); // el navegador reconoció una frase completa
    fixture.detectChanges();
    const textarea = (fixture.nativeElement as HTMLElement).querySelector(
      'textarea.entrada-captura',
    ) as HTMLTextAreaElement;
    expect(textarea.value).toBe('prensa 10 con 40');

    boton(fixture, 'Enviar').click();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(stub.capturarTexto).toHaveBeenCalledWith(1, 'prensa 10 con 40');
  });

  it('dictar de nuevo mientras escucha pide detener en vez de relanzar', async () => {
    const dictado = {
      soportado: true,
      escuchando: signal(true),
      parcial: signal(''),
      fallo: signal<string | null>(null),
      escuchar: vi.fn(),
      detener: vi.fn(),
    };
    const fixture = await montar(stubPlanilla(respuesta(null)), dictado);

    boton(fixture, 'Escuchando…').click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(dictado.detener).toHaveBeenCalledTimes(1);
    expect(dictado.escuchar).not.toHaveBeenCalled();
  });

  it('un navegador sin dictado por voz enseña la ayuda del teclado y no el botón', async () => {
    const fixture = await montar(stubPlanilla(respuesta(null))); // jsdom: sin Web Speech

    expect(texto(fixture)).toContain('Tu navegador no ofrece dictado por voz');
    const botones = [...(fixture.nativeElement as HTMLElement).querySelectorAll('button')];
    expect(botones.some((b) => (b.textContent ?? '').includes('Dictar'))).toBe(false);
  });
});
