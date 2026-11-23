import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TrainingHistory } from './training-history';
import { PlanillaApi, PlanillaConfirmadaVista } from '../../core/planilla-api';
import { of, Subject } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';

const older: PlanillaConfirmadaVista = { id: 3, alumnoId: 1, sesionId: 1, confirmadaUtc: '2026-11-18T12:00:00Z', filas: [] };
const newer: PlanillaConfirmadaVista = { ...older, id: 4, confirmadaUtc: '2026-11-19T12:00:00Z' };

describe('Mis entrenamientos', () => {
  const originalShowModal = Object.getOwnPropertyDescriptor(HTMLDialogElement.prototype, 'showModal');
  const originalClose = Object.getOwnPropertyDescriptor(HTMLDialogElement.prototype, 'close');
  let fixture: ComponentFixture<TrainingHistory>;
  let dialog: HTMLDialogElement;
  let trigger: HTMLButtonElement;
  let api: { renameTraining: ReturnType<typeof vi.fn>; deleteTraining: ReturnType<typeof vi.fn> };

  beforeEach(async () => {
    Object.defineProperty(HTMLDialogElement.prototype, 'showModal', { configurable: true, writable: true, value: vi.fn(function (this: HTMLDialogElement) { this.setAttribute('open', ''); }) });
    Object.defineProperty(HTMLDialogElement.prototype, 'close', { configurable: true, writable: true, value: vi.fn(function (this: HTMLDialogElement) { this.removeAttribute('open'); }) });
    api = { renameTraining: vi.fn(() => of({ ...newer, name: 'Entrenamiento de piernas' })), deleteTraining: vi.fn(() => of(undefined)) };
    TestBed.configureTestingModule({ imports: [TrainingHistory], providers: [{ provide: PlanillaApi, useValue: api }] });
    fixture = TestBed.createComponent(TrainingHistory);
    fixture.componentRef.setInput('entries', [older, newer]);
    await fixture.whenStable();
    const host = fixture.nativeElement as HTMLElement;
    dialog = host.querySelector('dialog')!;
    trigger = host.querySelector('.history-trigger')!;
    // JSDOM does not implement native dialog behavior; browser QA covers focus containment.
    dialog.showModal = vi.fn(() => {
      dialog.setAttribute('open', '');
      dialog.querySelector<HTMLButtonElement>('.history-close')!.focus();
    });
    dialog.close = vi.fn(() => {
      dialog.removeAttribute('open');
      dialog.dispatchEvent(new Event('close'));
    });
    fixture.componentInstance.opened.subscribe(() => fixture.componentRef.setInput('isOpen', true));
    fixture.componentInstance.closed.subscribe(() => fixture.componentRef.setInput('isOpen', false));
  });

  afterEach(() => {
    fixture.destroy();
    if (originalShowModal) Object.defineProperty(HTMLDialogElement.prototype, 'showModal', originalShowModal);
    else Reflect.deleteProperty(HTMLDialogElement.prototype, 'showModal');
    if (originalClose) Object.defineProperty(HTMLDialogElement.prototype, 'close', originalClose);
    else Reflect.deleteProperty(HTMLDialogElement.prototype, 'close');
    vi.restoreAllMocks();
  });

  async function open() {
    trigger.focus();
    trigger.click();
    await fixture.whenStable();
    expect(dialog.open).toBe(true);
  }

  it('muestra el conteo real y ordena por fecha sin modificar la entrada', async () => {
    expect(trigger.textContent).toContain('2');
    const rows = [...dialog.querySelectorAll('.history-entry')];
    expect(rows[0].textContent).toContain('Planilla #4');
    expect(rows[1].textContent).toContain('Planilla #3');
    expect(fixture.componentInstance.entries()).toEqual([older, newer]);
    await open();
    expect(trigger.getAttribute('aria-expanded')).toBe('true');
  });

  it.each(['X', 'Escape', 'fondo'])('cierra con %s y devuelve el foco al acceso', async method => {
    await open();
    if (method === 'X') dialog.querySelector<HTMLButtonElement>('.history-close')!.click();
    else if (method === 'Escape') {
      const cancel = new Event('cancel', { cancelable: true });
      dialog.dispatchEvent(cancel);
      expect(cancel.defaultPrevented).toBe(true);
    } else {
      vi.spyOn(dialog, 'getBoundingClientRect').mockReturnValue(new DOMRect(50, 50, 300, 400));
      dialog.dispatchEvent(new MouseEvent('click', { clientX: 100, clientY: 100 }));
      expect(dialog.open).toBe(true);
      dialog.dispatchEvent(new MouseEvent('click', { clientX: 10, clientY: 10 }));
    }
    await fixture.whenStable();
    expect(dialog.open).toBe(false);
    expect(trigger.getAttribute('aria-expanded')).toBe('false');
    expect(document.activeElement).toBe(trigger);
  });

  it('selecciona el snapshot exacto y cierra la consulta', async () => {
    const selected = vi.fn();
    fixture.componentInstance.selected.subscribe(selected);
    await open();
    dialog.querySelector<HTMLButtonElement>('.history-entry')!.click();
    await fixture.whenStable();
    expect(selected).toHaveBeenCalledExactlyOnceWith(newer);
    expect(dialog.open).toBe(false);
  });

  it('guarda el nombre normalizado desde el historial y conserva el número y la fecha', async () => {
    const component = fixture.componentInstance;
    component.renamed.subscribe(updated => fixture.componentRef.setInput('entries', [older, updated]));
    await open();
    dialog.querySelector<HTMLButtonElement>('[aria-label="Cambiar nombre de Entrenamiento #4"]')!.click();
    await fixture.whenStable();
    const input = dialog.querySelector<HTMLInputElement>('input')!;
    input.value = '  Entrenamiento de piernas  ';
    input.dispatchEvent(new Event('input'));
    dialog.querySelector('form')!.dispatchEvent(new Event('submit', { cancelable: true }));
    await fixture.whenStable();
    expect(api.renameTraining).toHaveBeenCalledExactlyOnceWith(4, 'Entrenamiento de piernas');
    const row = dialog.querySelector('.history-entry')!;
    expect(row.textContent).toContain('Entrenamiento de piernas');
    expect(row.textContent).toContain('19 de noviembre de 2026');
    expect(row.textContent).toContain('Planilla #4');
    expect(api.deleteTraining).not.toHaveBeenCalled();
  });

  it('conserva el borrador del nombre cuando falla el guardado y evita envíos simultáneos', async () => {
    const pending = new Subject<PlanillaConfirmadaVista>();
    api.renameTraining.mockReturnValue(pending);
    await open();
    const component = fixture.componentInstance;
    component.editName(newer);
    component.draftName.set('Piernas');
    component.saveName(new Event('submit'), newer);
    component.saveName(new Event('submit'), newer);
    expect(api.renameTraining).toHaveBeenCalledTimes(1);
    pending.error(new HttpErrorResponse({ status: 503, error: { title: 'No pudimos guardar' } }));
    await fixture.whenStable();
    expect(component.editingId()).toBe(4);
    expect(component.draftName()).toBe('Piernas');
    expect(dialog.querySelector('[role="alert"]')!.textContent).toContain('No pudimos guardar');
  });

  it('cancelar la eliminación conserva el registro y confirmar lo quita del contador', async () => {
    const component = fixture.componentInstance;
    component.removed.subscribe(id => fixture.componentRef.setInput('entries', component.entries().filter(entry => entry.id !== id)));
    await open();
    dialog.querySelector<HTMLButtonElement>('[aria-label="Eliminar Entrenamiento #4"]')!.click();
    await fixture.whenStable();
    const confirmation = dialog.querySelector<HTMLDialogElement>('#delete-training-dialog')!;
    expect(confirmation.open).toBe(true);
    confirmation.querySelector<HTMLButtonElement>('.cancel')!.click();
    await fixture.whenStable();
    expect(api.deleteTraining).not.toHaveBeenCalled();
    expect(component.entries()).toHaveLength(2);
    dialog.querySelector<HTMLButtonElement>('[aria-label="Eliminar Entrenamiento #4"]')!.focus();
    component.requestDelete(newer);
    await fixture.whenStable();
    confirmation.querySelector<HTMLButtonElement>('.confirm')!.click();
    await fixture.whenStable();
    expect(api.deleteTraining).toHaveBeenCalledExactlyOnceWith(4);
    expect(dialog.open).toBe(true);
    expect(dialog.querySelectorAll('.history-entry')).toHaveLength(1);
    expect(trigger.querySelector('.history-count')!.textContent).toContain('1');
    expect(document.activeElement).toBe(dialog.querySelector('.history-close'));
  });

  it('un error de eliminación conserva la entrada y permite reintentar sin doble envío', async () => {
    const pending = new Subject<void>();
    api.deleteTraining.mockReturnValue(pending);
    await open();
    const component = fixture.componentInstance;
    component.requestDelete(newer);
    await fixture.whenStable();
    component.deleteTraining();
    component.deleteTraining();
    expect(api.deleteTraining).toHaveBeenCalledTimes(1);
    pending.error(new HttpErrorResponse({ status: 503, error: { title: 'No pudimos eliminar' } }));
    await fixture.whenStable();
    expect(component.entries()).toHaveLength(2);
    expect(component.deleting()?.id).toBe(4);
    expect(dialog.querySelector('#delete-training-dialog [role="alert"]')!.textContent).toContain('No pudimos eliminar');
  });

  it('conserva el desplazamiento del fondo aunque el navegador lo cambie al enfocar el diálogo', async () => {
    let scrollTop = 180;
    vi.spyOn(window, 'scrollY', 'get').mockImplementation(() => scrollTop);
    const scroll = vi.spyOn(window, 'scrollTo').mockImplementation((_left, top) => {
      if (typeof top === 'number') scrollTop = top;
    });
    dialog.showModal = vi.fn(() => { dialog.setAttribute('open', ''); scrollTop = 0; });
    await open();
    expect(scrollTop).toBe(180);
    expect(scroll).toHaveBeenCalledWith(0, 180);
    scrollTop = 0;
    dialog.querySelector<HTMLButtonElement>('.history-close')!.click();
    await fixture.whenStable();
    expect(scrollTop).toBe(180);
  });

  it('distingue carga, error con reintento y lista vacía sin anunciar datos incorrectos', async () => {
    fixture.componentRef.setInput('loading', true);
    await fixture.whenStable();
    expect(dialog.querySelector('[role="status"]')?.textContent).toContain('Cargando');
    expect(trigger.querySelector('.history-count')).toBeNull();
    fixture.componentRef.setInput('loading', false);
    fixture.componentRef.setInput('error', 'No pudimos cargar tus entrenamientos.');
    await fixture.whenStable();
    expect(dialog.querySelector('[role="alert"]')?.textContent).toContain('No pudimos cargar');
    expect(trigger.querySelector('.history-count')).toBeNull();
    const retry = vi.fn();
    fixture.componentInstance.retry.subscribe(retry);
    dialog.querySelector<HTMLButtonElement>('.history-retry')!.click();
    expect(retry).toHaveBeenCalledOnce();
    fixture.componentRef.setInput('error', null);
    fixture.componentRef.setInput('entries', []);
    await fixture.whenStable();
    expect(trigger.querySelector('.history-count')?.textContent).toContain('0');
    expect(dialog.textContent).toContain('Cuando confirmes una planilla');
    expect(dialog.querySelectorAll('.history-entry')).toHaveLength(0);
  });
});
