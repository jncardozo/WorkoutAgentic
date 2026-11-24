import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ConfirmationDialog } from './confirmation-dialog';

describe('Confirmación de acciones', () => {
  let fixture: ComponentFixture<ConfirmationDialog>;
  let dialog: HTMLDialogElement;

  beforeEach(async () => {
    TestBed.configureTestingModule({ imports: [ConfirmationDialog] });
    fixture = TestBed.createComponent(ConfirmationDialog);
    fixture.componentRef.setInput('dialogId', 'test-confirmation');
    fixture.componentRef.setInput('title', '¿Terminaste tu entrenamiento?');
    fixture.componentRef.setInput('message', 'Una vez confirmada, no podrás modificar sus series.');
    fixture.componentRef.setInput('confirmLabel', 'Sí, confirmar planilla');
    await fixture.whenStable();
    dialog = fixture.nativeElement.querySelector('dialog');
    dialog.showModal = vi.fn(() => {
      dialog.setAttribute('open', '');
      dialog.querySelector<HTMLButtonElement>('.cancel')!.focus();
    });
    dialog.close = vi.fn(() => dialog.removeAttribute('open'));
  });

  afterEach(() => fixture.destroy());

  it('incluye la lista de pendientes en la descripción accesible y permite personalizar la salida', async () => {
    fixture.componentRef.setInput('details', ['Sentadilla, serie 2: peso.']);
    fixture.componentRef.setInput('cancelLabel', 'Volver a editar');
    fixture.componentRef.setInput('isOpen', true);
    await fixture.whenStable();
    expect(dialog.querySelector('li')?.textContent).toBe('Sentadilla, serie 2: peso.');
    expect(dialog.getAttribute('aria-describedby')).toContain('test-confirmation-details');
    expect(dialog.querySelector('.cancel')?.textContent).toContain('Volver a editar');
  });

  it('abre con foco en Cancelar y devuelve el foco al cerrar', async () => {
    const trigger = document.createElement('button');
    document.body.append(trigger);
    trigger.focus();
    fixture.componentRef.setInput('isOpen', true);
    await fixture.whenStable();
    expect(dialog.open).toBe(true);
    expect(document.activeElement).toBe(dialog.querySelector('.cancel'));
    fixture.componentRef.setInput('isOpen', false);
    await fixture.whenStable();
    expect(document.activeElement).toBe(trigger);
    trigger.remove();
  });

  it('Escape cancela sin confirmar y el estado ocupado bloquea ambas acciones', async () => {
    const cancelled = vi.fn();
    const confirmed = vi.fn();
    fixture.componentInstance.cancelled.subscribe(cancelled);
    fixture.componentInstance.confirmed.subscribe(confirmed);
    fixture.componentRef.setInput('isOpen', true);
    await fixture.whenStable();
    const escape = new Event('cancel', { cancelable: true });
    dialog.dispatchEvent(escape);
    expect(escape.defaultPrevented).toBe(true);
    expect(cancelled).toHaveBeenCalledOnce();
    expect(confirmed).not.toHaveBeenCalled();
    fixture.componentRef.setInput('busy', true);
    await fixture.whenStable();
    dialog.dispatchEvent(new Event('cancel', { cancelable: true }));
    fixture.componentInstance.confirm();
    expect(cancelled).toHaveBeenCalledOnce();
    expect(confirmed).not.toHaveBeenCalled();
    expect([...dialog.querySelectorAll('button')].every(button => button.disabled)).toBe(true);
  });
});
