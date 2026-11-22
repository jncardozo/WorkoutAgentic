import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { of, Subject } from 'rxjs';
import { vi } from 'vitest';

import {
  FilaParcialVista,
  PlanillaApi,
  PlanillaConfirmadaVista,
  RespuestaCapturaTexto,
  RespuestaSesionActual,
  SesionActualVista,
} from '../../core/planilla-api';
import { DictadoVoz, FABRICA_RECONOCEDOR } from './dictado-voz';
import { PlanillaPage } from './planilla-page';

const CATALOGO = [
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
  bulkEdit: ReturnType<typeof vi.fn>;
  eliminarFila: ReturnType<typeof vi.fn>;
  confirmar: ReturnType<typeof vi.fn>;
  enviarPlanillaPorCorreo: ReturnType<typeof vi.fn>;
  emailConfigurationStatus: ReturnType<typeof vi.fn>;
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
    bulkEdit: vi.fn(() => of(respuestaInicial)),
    eliminarFila: vi.fn(() => of(respuestaInicial)),
    confirmar: vi.fn(() => of(PLANILLA_CONFIRMADA)),
    enviarPlanillaPorCorreo: vi.fn(() => of({ mensaje: 'Planilla enviada.' })),
    emailConfigurationStatus: vi.fn(() => of({ configured: true })),
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
  it('envía desde el historial sin modificar la sesión o el borrador en curso', async () => {
    const dialogPrototype = HTMLDialogElement.prototype;
    const originalShowModal = Object.getOwnPropertyDescriptor(dialogPrototype, 'showModal');
    Object.defineProperty(dialogPrototype, 'showModal', { configurable: true, value: function (this: HTMLDialogElement) { this.setAttribute('open', ''); } });
    try {
      const stub = stubPlanilla(respuesta(sesion()));
      const fixture = await montar(stub);
      const page = fixture.componentInstance;
      page.borradorCaptura.set('Mi próxima serie');
      const current = page.sesion();
      page.verPlanillaHistorial(PLANILLA_CONFIRMADA);
      await fixture.whenStable();
      boton(fixture, 'Enviar planilla por email').click();
      await fixture.whenStable();
      escribir(fixture, 'app-report-email input', 'qa@example.test');
      fixture.nativeElement.querySelector('app-report-email form').dispatchEvent(new Event('submit'));
      await fixture.whenStable();
      expect(stub.enviarPlanillaPorCorreo).toHaveBeenCalledExactlyOnceWith(9, {
        alumnoId: 1, correo: 'qa@example.test', formato: 'pdf',
      });
      expect(page.sesion()).toBe(current);
      page.cerrarReportes();
      await fixture.whenStable();
      expect(page.borradorCaptura()).toBe('Mi próxima serie');
      expect(page.historialAbierto()).toBe(true);
      expect(stub.capturarTexto).not.toHaveBeenCalled();
      const dialog = fixture.nativeElement.querySelector('dialog.training-history-dialog') as HTMLDialogElement;
      dialog.close = vi.fn(() => dialog.removeAttribute('open'));
      fixture.destroy();
    } finally {
      if (originalShowModal) Object.defineProperty(dialogPrototype, 'showModal', originalShowModal);
      else Reflect.deleteProperty(dialogPrototype, 'showModal');
    }
  });
  it('abre el historial modal sin desplazar ni reemplazar el borrador de captura', async () => {
    const stub = stubPlanilla(respuesta(sesion()), { historial: vi.fn(() => of([PLANILLA_CONFIRMADA])) });
    const fixture = await montar(stub);
    const host = fixture.nativeElement as HTMLElement;
    escribir(fixture, 'textarea.entrada-captura', 'Mi entrenamiento pendiente');
    const capture = host.querySelector('.panel-captura');
    const dialog = host.querySelector<HTMLDialogElement>('dialog.training-history-dialog')!;
    dialog.showModal = vi.fn(() => dialog.setAttribute('open', ''));
    dialog.close = vi.fn(() => { dialog.removeAttribute('open'); dialog.dispatchEvent(new Event('close')); });
    boton(fixture, 'Mis entrenamientos').click();
    await fixture.whenStable();
    expect(dialog.open).toBe(true);
    expect(host.querySelector('.panel-historial')).toBeNull();
    expect(host.querySelector('.panel-captura')).toBe(capture);
    dialog.querySelector<HTMLButtonElement>('.history-close')!.click();
    await fixture.whenStable();
    expect(fixture.componentInstance.historialAbierto()).toBe(false);
    expect(fixture.componentInstance.borradorCaptura()).toBe('Mi entrenamiento pendiente');
    expect(stub.capturarTexto).not.toHaveBeenCalled();
  });

  it('la ayuda del comando no reemplaza el borrador ni envía una captura', async () => {
    const stub = stubPlanilla(respuesta(sesion()));
    const fixture = await montar(stub);
    const host = fixture.nativeElement as HTMLElement;
    escribir(fixture, 'textarea.entrada-captura', 'Mi entrenamiento pendiente');
    const dialog = host.querySelector('dialog.command-example-dialog') as HTMLDialogElement;
    // JSDOM no implementa los métodos del diálogo; el comportamiento nativo se
    // comprueba además en navegador con X, Escape y clic fuera.
    const show = vi.fn(() => dialog.setAttribute('open', ''));
    dialog.showModal = show;
    dialog.close = vi.fn(() => dialog.removeAttribute('open'));

    expect(host.querySelectorAll('.ejemplo-rapido')).toHaveLength(1);
    boton(fixture, 'Comando de ejemplo').click();
    expect(show).toHaveBeenCalledOnce();
    expect(dialog.open).toBe(true);
    expect(dialog.textContent).toContain('Sillón de cuádriceps, 4 series');
    expect(dialog.textContent).toContain('Cuarta serie efectiva con 12 repeticiones y 75 kg');
    expect(dialog.textContent).toContain('100%');
    (dialog.querySelector('button') as HTMLButtonElement).click();
    expect(dialog.open).toBe(false);
    expect(fixture.componentInstance.borradorCaptura()).toBe('Mi entrenamiento pendiente');
    expect(stub.capturarTexto).not.toHaveBeenCalled();
  });

  it('actualiza el progreso real de las series y distingue completitud de confirmación', async () => {
    const rows = [fila(), fila({ id: 6, nroSerie: 2 }), fila({ id: 7, nroSerie: 3 }),
      fila({ id: 8, nroSerie: 4, pesoKg: null })];
    const fixture = await montar(stubPlanilla(respuesta(sesion({ filas: rows }))));
    const host = fixture.nativeElement as HTMLElement;
    const bar = () => host.querySelector('[role="progressbar"]')!;
    expect(bar().getAttribute('aria-valuenow')).toBe('75');
    expect(bar().getAttribute('aria-valuetext')).toBe('3 de 4 series completadas');
    expect(host.querySelector('.progress-fill')?.getAttribute('style')).toContain('75%');
    expect(texto(fixture)).not.toContain('Todas las series completas');

    fixture.componentInstance.sesion.set(respuesta(sesion({ filas: rows.map(row => ({ ...row, pesoKg: 45 })) })));
    await fixture.whenStable();
    fixture.detectChanges();
    expect(bar().getAttribute('aria-valuenow')).toBe('100');
    expect(texto(fixture)).toContain('Todas las series completas');
    expect(fixture.componentInstance.planillaConfirmada()).toBeNull();
  });

  it.each(['timeout', 'click'])('descarta la confirmación por %s sin cambiar la cuadrícula', async (mode) => {
    const fixture = await montar(stubPlanilla(respuesta(null)));
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] });
    try {
      fixture.componentInstance.borradorCaptura.set('Press de banca 4 series');
      fixture.componentInstance.enviarCaptura();
      fixture.detectChanges();
      expect(fixture.nativeElement.querySelector('.dismissible-notice')).not.toBeNull();
      if (mode === 'timeout') {
        vi.advanceTimersByTime(7999);
        expect(fixture.componentInstance.ultimoMensajeBot()).not.toBeNull();
        vi.advanceTimersByTime(1);
      } else (fixture.nativeElement.querySelector('.dismissible-notice') as HTMLButtonElement).click();
      fixture.detectChanges();
      expect(fixture.componentInstance.ultimoMensajeBot()).toBeNull();
      expect(fixture.nativeElement.querySelector('.dismissible-notice')).toBeNull();
      expect(fixture.nativeElement.querySelector('table.cuadricula')).not.toBeNull();
    } finally { fixture.destroy(); vi.useRealTimers(); }
  });

  it('reinicia el plazo con un mensaje nuevo y limpia el temporizador al salir', async () => {
    const fixture = await montar(stubPlanilla(respuesta(null)));
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] });
    try {
      fixture.componentInstance.borradorCaptura.set('Primera serie 12 con 20');
      fixture.componentInstance.enviarCaptura();
      vi.advanceTimersByTime(4000);
      fixture.componentInstance.borradorCaptura.set('Segunda serie 12 con 30');
      fixture.componentInstance.enviarCaptura();
      vi.advanceTimersByTime(4000);
      expect(fixture.componentInstance.ultimoMensajeBot()).not.toBeNull();
      vi.advanceTimersByTime(4000);
      expect(fixture.componentInstance.ultimoMensajeBot()).toBeNull();
      fixture.componentInstance.borradorCaptura.set('Tercera serie 12 con 40');
      fixture.componentInstance.enviarCaptura();
      fixture.destroy();
      expect(vi.getTimerCount()).toBe(0);
    } finally { vi.useRealTimers(); }
  });

  it.each(['question', 'clarification'])('mantiene visibles los mensajes de %s', async (mode) => {
    const message = mode === 'question' ? '¿La serie fue con 40 o 60 kg?' : 'Me falta la variante del ejercicio.';
    const capture = { ...RESPUESTA_CAPTURA, mensajeAlumno: message,
      respuesta: respuesta(sesion({ aclaracion: mode === 'clarification' ? { campo: 'ejercicio', reintento: 0 } : null })) };
    const fixture = await montar(stubPlanilla(respuesta(null), { capturarTexto: vi.fn(() => of(capture)) }));
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] });
    try {
      fixture.componentInstance.borradorCaptura.set('Press 4 series');
      fixture.componentInstance.enviarCaptura();
      vi.advanceTimersByTime(20_000);
      fixture.detectChanges();
      expect(fixture.componentInstance.ultimoMensajeBot()).toBe(message);
      expect(fixture.nativeElement.querySelector('.dismissible-notice')).toBeNull();
    } finally { fixture.destroy(); vi.useRealTimers(); }
  });

  it.each(['success', 'error'])('muestra carga durante el envío y la retira con %s', async (outcome) => {
    const pending = new Subject<RespuestaCapturaTexto>();
    const stub = stubPlanilla(respuesta(null), { capturarTexto: vi.fn(() => pending) });
    const fixture = await montar(stub);
    escribir(fixture, 'textarea.entrada-captura', 'Press de banca 4 series');
    boton(fixture, 'Enviar').click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(boton(fixture, 'Cargando').disabled).toBe(true);
    expect(fixture.nativeElement.querySelector('.capture-spinner')).not.toBeNull();
    expect(texto(fixture)).toContain('Cargando tus series en la cuadrícula');
    expect((fixture.nativeElement.querySelector('textarea.entrada-captura') as HTMLTextAreaElement).readOnly).toBe(true);
    fixture.componentInstance.enviarCaptura();
    expect(stub.capturarTexto).toHaveBeenCalledTimes(1);

    if (outcome === 'success') { pending.next(RESPUESTA_CAPTURA); pending.complete(); }
    else pending.error(new HttpErrorResponse({ status: 503, error: { title: 'Servicio temporalmente no disponible' } }));
    await fixture.whenStable();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.capture-spinner')).toBeNull();
    expect(boton(fixture, 'Enviar').disabled).toBe(false);
    if (outcome === 'error') expect(fixture.componentInstance.borradorCaptura()).toBe('Press de banca 4 series');
    else expect(fixture.nativeElement.querySelector('table.cuadricula')).not.toBeNull();
  });

  it('el botón Editar de la cuadrícula abre campos inline sin abrir el editor múltiple', async () => {
    const stub = stubPlanilla(respuesta(sesion()));
    const fixture = await montar(stub);
    const editButton = [...(fixture.nativeElement as HTMLElement).querySelectorAll('.cuadricula button')]
      .find(button => button.textContent?.trim() === 'Editar') as HTMLButtonElement;
    editButton.click();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(fixture.componentInstance.trainingEditorOpen()).toBe(false);
    expect(fixture.nativeElement.querySelector('input[aria-label="Peso en kg"]')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('app-training-editor')).toBeNull();
    expect(stub.bulkEdit).not.toHaveBeenCalled();
  });

  it('muestra una cabecera por ejercicio y conserva todas sus series y notas', async () => {
    const rows = [fila({ comments: 'aproximación' }),
      fila({ id: 6, ejercicioCanonico: 'Sentadilla', observations: 'Molestia leve' }),
      fila({ id: 7, nroSerie: 2 })];
    const fixture = await montar(stubPlanilla(respuesta(sesion({ filas: rows }))));
    const host = fixture.nativeElement as HTMLElement;
    const cards = [...host.querySelectorAll('.exercise-card')];
    expect(cards).toHaveLength(2);
    expect(cards[0].querySelector('h3')?.textContent).toBe('Press banca');
    expect(cards[0].querySelectorAll('tbody tr')).toHaveLength(2);
    expect(cards[0].querySelector('tbody')?.textContent).not.toContain('Press banca');
    expect(cards[0].textContent).toContain('aproximación');
    expect(cards[1].textContent).toContain('Molestia leve');
    expect(host.querySelectorAll('tbody tr')).toHaveLength(3);
    expect([...host.querySelectorAll('.cuadricula th')].some(th => th.textContent === 'Ejercicio')).toBe(false);
    expect(fixture.componentInstance.filas()).toEqual(rows);
  });

  it('agrupa también la planilla confirmada sin mostrar acciones de edición', async () => {
    const fixture = await montar(stubPlanilla(respuesta(null)));
    fixture.componentInstance.planillaConfirmada.set({ ...PLANILLA_CONFIRMADA,
      filas: [PLANILLA_CONFIRMADA.filas[0], { ...PLANILLA_CONFIRMADA.filas[0], orden: 2, nroSerie: 2 }] });
    await fixture.whenStable();
    fixture.detectChanges();
    const host = fixture.nativeElement as HTMLElement;
    expect(host.querySelectorAll('.exercise-card')).toHaveLength(1);
    expect(host.querySelectorAll('.cuadricula tbody tr')).toHaveLength(2);
    expect(host.querySelector('.cuadricula tbody')?.textContent).not.toContain('Press banca');
    expect(host.querySelector('.cuadricula button')).toBeNull();
  });

  it('ignora una recarga en vuelo mientras se edita el borrador', async () => {
    const stub = stubPlanilla(respuesta(sesion()));
    const fixture = await montar(stub);
    const inFlight = new Subject<RespuestaSesionActual>();
    stub.sesionActual.mockReturnValueOnce(inFlight);
    fixture.componentInstance.recargar();
    fixture.componentInstance.openTrainingEditor();
    await fixture.whenStable();
    inFlight.next(respuesta(sesion({ filas: [fila({ pesoKg: 999 })] })));
    fixture.detectChanges();
    expect(fixture.componentInstance.filas()[0].pesoKg).toBe(45);
    inFlight.complete();
  });

  it('muestra y edita comentarios y observaciones como campos separados', async () => {
    const row = fila({ comments: 'aproximación', observations: 'Sentí un tirón en el pectoral izquierdo' });
    const stub = stubPlanilla(respuesta(sesion({ filas: [row] })));
    const fixture = await montar(stub);
    expect(texto(fixture)).toContain('Comentarios');
    expect(texto(fixture)).toContain('Observaciones');
    expect(texto(fixture)).toContain(row.observations!);
    (fixture.nativeElement.querySelector('.row-edit') as HTMLButtonElement).click();
    await fixture.whenStable();
    fixture.detectChanges();
    escribir(fixture, 'textarea[aria-label="Comentarios"]', 'efectiva');
    escribir(fixture, 'textarea[aria-label="Observaciones"]', '');
    await fixture.whenStable();
    fixture.detectChanges();
    boton(fixture, 'Guardar').click();
    await fixture.whenStable();
    expect(stub.editarFila.mock.calls[0][1]).toMatchObject({ comments: 'efectiva', observations: null, reps: 8, pesoKg: 45 });
  });

  it('no envía mientras escucha y libera el micrófono al salir de la pantalla', async () => {
    const dictado = {
      soportado: true, escuchando: signal(true), remainingSeconds: signal(32),
      parcial: signal('última frase'), fallo: signal(null),
      escuchar: vi.fn(), detener: vi.fn(), cancelar: vi.fn(),
    };
    const stub = stubPlanilla(respuesta(null));
    const fixture = await montar(stub, dictado);
    fixture.componentInstance.borradorCaptura.set('Press de banca 4 series');
    fixture.detectChanges();
    expect(boton(fixture, 'Enviar').disabled).toBe(true);
    fixture.componentInstance.enviarCaptura();
    expect(stub.capturarTexto).not.toHaveBeenCalled();
    expect(texto(fixture)).toContain('32 s');
    fixture.destroy();
    expect(dictado.cancelar).toHaveBeenCalled();
  });

  it('sin sesión muestra el aviso de dictar el primer ejercicio y no cuadrícula', async () => {
    const fixture = await montar(stubPlanilla(respuesta(null)));

    expect(texto(fixture)).toContain('Sin sesión activa');
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

    boton(fixture, 'Volver a la planilla').click();
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

    boton(fixture, 'Agregar fila').click();
    await fixture.whenStable();
    fixture.detectChanges();

    boton(fixture, 'Guardar fila').click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(stub.agregarFila).not.toHaveBeenCalled();
    expect(texto(fixture)).toContain('La fila necesita las repeticiones antes de guardarse.');
    expect(texto(fixture)).toContain('La fila necesita el peso');
  });

  it('editar guarda el reemplazo completo y repinta la cuadrícula', async () => {
    const respuestaEdicion = respuesta(sesion({ filas: [fila({ id: 5, pesoKg: 42.5 })] }));
    const stub = stubPlanilla(respuesta(sesion({ filas: [fila({ id: 5 })] })), {
      editarFila: vi.fn(() => of(respuestaEdicion)),
    });
    const fixture = await montar(stub);

    (fixture.nativeElement.querySelector('.row-edit') as HTMLButtonElement).click();
    await fixture.whenStable();
    fixture.detectChanges();

    escribir(fixture, 'input[aria-label="Peso en kg"]', '42.5');
    await fixture.whenStable();
    fixture.detectChanges();
    boton(fixture, 'Guardar').click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(stub.editarFila).toHaveBeenCalledTimes(1);
    expect(stub.editarFila.mock.calls[0]).toMatchObject([5, { pesoKg: 42.5, ejercicioId: null, ejercicioCrudo: 'Press banca' }]);
    expect(stub.bulkEdit).not.toHaveBeenCalled();
    expect(fixture.nativeElement.querySelector('app-training-editor')).toBeNull();
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
      cancelar: vi.fn(),
      remainingSeconds: signal(60),
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
      cancelar: vi.fn(),
      remainingSeconds: signal(60),
    };
    const fixture = await montar(stubPlanilla(respuesta(null)), dictado);

    boton(fixture, 'Detener').click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(dictado.detener).toHaveBeenCalledTimes(1);
    expect(dictado.escuchar).not.toHaveBeenCalled();
  });

  it('un navegador sin dictado por voz enseña la ayuda del teclado y no el botón', async () => {
    const fixture = await montar(stubPlanilla(respuesta(null))); // jsdom: sin Web Speech

    expect(texto(fixture)).toContain('Dictado por voz no disponible');
    const botones = [...(fixture.nativeElement as HTMLElement).querySelectorAll('button')];
    expect(botones.some((b) => (b.textContent ?? '').includes('Dictar'))).toBe(false);
  });
});
