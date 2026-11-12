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

async function setup() {
  const api = { bulkEdit: vi.fn(() => of({ alumno: { id: 1, nombre: 'Alumno' }, sesion: null })) };
  TestBed.configureTestingModule({ imports: [TrainingEditor], providers: [{ provide: PlanillaApi, useValue: api }] });
  const fixture = TestBed.createComponent(TrainingEditor);
  fixture.componentRef.setInput('rows', [row(1), row(2), row(3, 'Remo')]);
  fixture.componentRef.setInput('studentId', 1);
  fixture.componentRef.setInput('sessionId', 10);
  await fixture.whenStable();
  return { fixture, editor: fixture.componentInstance, api };
}

describe('TrainingEditor', () => {
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
    expect(host.querySelector('input[type="checkbox"]')).toBeNull();
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
