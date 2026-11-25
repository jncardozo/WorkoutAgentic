import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { Subject, of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { FilaParcialVista, PlanillaApi } from '../../core/planilla-api';
import { TrainingEditor } from './training-editor';

const row = (id: number, exercise = 'Press de banca'): FilaParcialVista => ({
  id, ejercicioId: null, ejercicioCrudo: exercise, ejercicioCanonico: exercise,
  descripcionPendiente: false, nroSerie: id, reps: id === 1 ? null : 12,
  pesoKg: 40, unidad: 'Kg', tiempoSeg: null, pendienteAclaracion: false,
  creadoUtc: '2026-11-10T12:00:00Z', comments: 'aproximación', observations: 'Sin molestias',
});

async function setup(embedded = false) {
  const api = { bulkEdit: vi.fn(() => of({ alumno: { id: 1, nombre: 'Alumno' }, sesion: null })) };
  TestBed.configureTestingModule({ imports: [TrainingEditor], providers: [{ provide: PlanillaApi, useValue: api }] });
  const fixture = TestBed.createComponent(TrainingEditor);
  fixture.componentRef.setInput('rows', [row(1), row(2), row(3, 'Remo')]);
  fixture.componentRef.setInput('studentId', 1);
  fixture.componentRef.setInput('sessionId', 10);
  fixture.componentRef.setInput('embedded', embedded);
  await fixture.whenStable();
  for (const dialog of fixture.nativeElement.querySelectorAll('dialog') as NodeListOf<HTMLDialogElement>) {
    dialog.showModal = vi.fn(() => dialog.setAttribute('open', ''));
    dialog.close = vi.fn(() => dialog.removeAttribute('open'));
  }
  return { fixture, editor: fixture.componentInstance, api };
}

describe('TrainingEditor', () => {
  it('en tarjetas oculta la identidad y conserva ejercicio y ordinal originales al guardar', async () => {
    const { fixture, editor, api } = await setup(true);
    fixture.componentRef.setInput('rows', [{ ...row(2), ejercicioId: 73,
      ejercicioCanonico: 'Press de banca', ejercicioCrudo: 'banco', nroSerie: 4 }]);
    await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('.identity')).toBeNull();
    expect(fixture.nativeElement.textContent).not.toContain('Ejercicio y número de serie');
    expect(fixture.nativeElement.textContent).toContain('Serie 4');
    editor.update(2, 'weight', '50');
    editor.save();
    const [, , edits] = api.bulkEdit.mock.calls[0] as unknown as [number, number,
      { original: { ejercicioId: number; ejercicioCrudo: string; nroSerie: number };
        values: { ejercicioId: number; ejercicioCrudo: string; nroSerie: number; pesoKg: number } }[]];
    expect(edits[0].original).toMatchObject({ ejercicioId: 73, ejercicioCrudo: 'banco', nroSerie: 4 });
    expect(edits[0].values).toMatchObject({ ejercicioId: 73, ejercicioCrudo: 'banco', nroSerie: 4, pesoKg: 50 });
  });

  it('el editor completo mantiene la edición del ejercicio y número de serie', async () => {
    const { fixture, editor } = await setup();
    editor.toggleGroup('press de banca');
    await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('.identity')).not.toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Ejercicio y número de serie');
  });

  it('en tarjetas abre las series y muestra cambios sin persistir hasta guardar', async () => {
    const { fixture, editor, api } = await setup(true);
    expect(fixture.nativeElement.querySelectorAll('.series-card')).toHaveLength(3);
    expect(fixture.nativeElement.querySelector('.editor-footer')).toBeNull();
    const dirty = vi.fn(); editor.dirtyChange.subscribe(dirty);
    editor.update(2, 'weight', '70,5');
    await fixture.whenStable();
    expect(dirty).toHaveBeenLastCalledWith(true);
    expect(fixture.nativeElement.querySelector('.changed .unsaved')?.textContent).toContain('Sin guardar');
    expect(fixture.nativeElement.querySelector('.editor-footer')).not.toBeNull();
    expect(api.bulkEdit).not.toHaveBeenCalled();
  });

  it('en tarjetas descartar conserva la vista y restaura pesos y RM', async () => {
    const { fixture, editor } = await setup(true);
    const closed = vi.fn(); editor.closed.subscribe(closed);
    editor.update(2, 'weight', '80'); editor.markOneRepMax(2, true);
    const cancel = editor.cancel();
    await fixture.whenStable();
    editor.resolveLeave(true); await cancel; await fixture.whenStable();
    expect(closed).not.toHaveBeenCalled();
    expect(editor.changes()).toEqual([]);
    expect(editor.drafts()[1]).toMatchObject({ weight: '40', isOneRepMax: false });
    expect(fixture.nativeElement.querySelectorAll('.series-card')).toHaveLength(3);
  });

  it('en tarjetas conserva la expansión al refrescar e incorpora grupos nuevos abiertos', async () => {
    const { fixture, editor } = await setup(true);
    editor.toggleGroup('press de banca');
    fixture.componentRef.setInput('rows', [row(1), row(2), row(3, 'Remo'), row(4, 'Sentadilla')]);
    await fixture.whenStable();
    expect(editor.expanded().has('press de banca')).toBe(false);
    expect(editor.expanded().has('remo')).toBe(true);
    expect(editor.expanded().has('sentadilla')).toBe(true);
  });

  it('ubica la barra por encima del teclado y la actualiza con VisualViewport', async () => {
    const originalViewport = Object.getOwnPropertyDescriptor(window, 'visualViewport');
    const viewport = Object.assign(new EventTarget(), { height: 400, offsetTop: 20 });
    Object.defineProperty(window, 'visualViewport', { configurable: true, value: viewport });
    try {
      const { fixture, editor } = await setup(true);
      editor.update(2, 'weight', '50'); await fixture.whenStable();
      expect(editor.keyboardInset()).toBe(Math.max(0, window.innerHeight - 420));
      viewport.height = 350; viewport.offsetTop = 50; viewport.dispatchEvent(new Event('resize'));
      await fixture.whenStable();
      const footer = fixture.nativeElement.querySelector('.editor-footer') as HTMLElement;
      expect(footer.style.bottom).toBe(`${Math.max(0, window.innerHeight - 400)}px`);
      fixture.destroy();
    } finally {
      if (originalViewport) Object.defineProperty(window, 'visualViewport', originalViewport);
      else Reflect.deleteProperty(window, 'visualViewport');
    }
  });

  it('la eliminación de una tarjeta exige aceptación y no se ofrece con borradores', async () => {
    const { editor } = await setup(true);
    const removed = vi.fn(); editor.removed.subscribe(removed);
    editor.requestRemoval(editor.drafts()[1]);
    expect(removed).not.toHaveBeenCalled();
    editor.confirmRemoval(); expect(removed).toHaveBeenCalledWith(2);
    editor.update(1, 'reps', '10');
    editor.requestRemoval(editor.drafts()[1]);
    expect(editor.deletionTarget()).toBeNull();
  });

  it('no convierte datos históricos omitidos en cambios al abrir', async () => {
    const { fixture, editor } = await setup();
    fixture.componentRef.setInput('rows', [row(1), { ...row(2), unidad: null, comments: ' nota histórica ' }]);
    await fixture.whenStable();
    expect(editor.changes()).toEqual([]);
    editor.update(2, 'reps', '10');
    expect(editor.changes()[0].values).toMatchObject({ unidad: null, comments: ' nota histórica ', tiempoSeg: null });
  });

  it('abre todos los ejercicios colapsados aunque haya series incompletas', async () => {
    const { fixture, editor } = await setup();
    expect(editor.expanded().size).toBe(0);
    expect(fixture.nativeElement.querySelectorAll('.series-card').length).toBe(0);
  });

  it('guarda solo las filas cambiadas en una petición', async () => {
    const { editor, api } = await setup();
    expect(editor.changes()).toEqual([]);
    editor.update(1, 'reps', '10'); editor.update(2, 'weight', '60,5');
    editor.save();
    expect(api.bulkEdit).toHaveBeenCalledTimes(1);
    const [student, session, rows] = api.bulkEdit.mock.calls[0] as unknown as [number, number, { id: number; values: { reps: number; pesoKg: number } }[]];
    expect([student, session]).toEqual([1, 10]);
    expect(rows.map(row => row.id)).toEqual([1, 2]);
    expect(rows[1].values.pesoKg).toBe(60.5);
  });

  it('no muestra selección compartida ni checkbox corporal y conserva las cargas existentes', async () => {
    const { fixture, editor } = await setup();
    fixture.componentRef.setInput('rows', [{ ...row(1), pesoKg: null, unidad: 'PesoCorporal' }]);
    await fixture.whenStable();
    editor.toggleGroup('press de banca');
    await fixture.whenStable();
    const host = fixture.nativeElement as HTMLElement;
    expect(host.textContent).not.toContain('Aplicar a varias series');
    expect(host.querySelectorAll('input[type="checkbox"]').length).toBe(1);
    expect(host.querySelector('input[type="checkbox"]')?.getAttribute('aria-label')).toBe('RM / 1 repetición');
    editor.update(1, 'reps', '10');
    expect(editor.changes()[0].values).toMatchObject({ reps: 10, pesoKg: null, unidad: 'PesoCorporal' });
    editor.update(1, 'weight', '20');
    expect(editor.changes()[0].values).toMatchObject({ pesoKg: 20, unidad: 'Kg' });
  });

  it('rechaza valores inválidos sin convertirlos en datos vacíos ni enviar', async () => {
    const { editor, api } = await setup();
    editor.update(1, 'reps', 'abc');
    expect(editor.changes().length).toBe(1);
    editor.save();
    expect(editor.errors()[1]).toBeTruthy();
    expect(api.bulkEdit).not.toHaveBeenCalled();
  });

  it('valida el RM y lo envía junto con el original para detectar conflictos', async () => {
    const { editor, api } = await setup();
    editor.markOneRepMax(2, true);
    editor.save();
    expect(api.bulkEdit).not.toHaveBeenCalled();
    expect(editor.errors()[2]).toContain('una repetición');
    editor.update(2, 'reps', '1');
    editor.save();
    const changes = api.bulkEdit.mock.calls[0] as unknown as [number, number, { original: { isOneRepMax: boolean }; values: { isOneRepMax: boolean; reps: number } }[]];
    expect(changes[2][0].original.isOneRepMax).toBe(false);
    expect(changes[2][0].values).toMatchObject({ isOneRepMax: true, reps: 1 });
  });

  it('conserva una marca al abrir y permite quitarla sin cambiar la carga', async () => {
    const { fixture, editor } = await setup();
    fixture.componentRef.setInput('rows', [{ ...row(1), reps: 1, isOneRepMax: true }]);
    await fixture.whenStable();
    expect(editor.changes()).toEqual([]);
    editor.markOneRepMax(1, false);
    expect(editor.changes()[0].values).toMatchObject({ isOneRepMax: false, reps: 1, pesoKg: 40 });
  });

  it('conserva el borrador si el servidor detecta un conflicto', async () => {
    const { editor, api } = await setup();
    api.bulkEdit.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 409, error: { detail: 'La serie cambió' } })));
    editor.update(1, 'weight', '80'); editor.save();
    expect(editor.saving()).toBe(false);
    expect(editor.drafts()[0].weight).toBe('80');
    expect(editor.failure()).toBe('La serie cambió');
  });

  it('pregunta antes de salir y bloquea salida y envíos duplicados durante el guardado', async () => {
    const { editor, api } = await setup();
    editor.update(1, 'reps', '10');
    const leaving = editor.requestLeave();
    expect(editor.discardPrompt()).toBe(true);
    editor.resolveLeave(false); expect(await leaving).toBe(false);
    const response = new Subject<never>(); api.bulkEdit.mockReturnValue(response);
    editor.save(); editor.save();
    expect(api.bulkEdit).toHaveBeenCalledTimes(1);
    expect(await editor.requestLeave()).toBe(false);
    response.complete();
  });
});
