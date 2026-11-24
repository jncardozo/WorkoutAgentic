import { DOCUMENT } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, ElementRef, effect, inject, input, output, viewChild } from '@angular/core';

@Component({
  selector: 'app-confirmation-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './confirmation-dialog.html',
  styleUrl: './confirmation-dialog.scss',
})
export class ConfirmationDialog {
  readonly dialogId = input.required<string>();
  readonly isOpen = input(false);
  readonly title = input.required<string>();
  readonly message = input.required<string>();
  readonly confirmLabel = input.required<string>();
  readonly cancelLabel = input('Cancelar');
  readonly details = input<string[]>([]);
  readonly busy = input(false);
  readonly error = input<string | null>(null);
  readonly confirmed = output<void>();
  readonly cancelled = output<void>();
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  private readonly document = inject(DOCUMENT);
  private returnFocus: HTMLElement | null = null;

  constructor() {
    effect(() => {
      const dialog = this.dialog().nativeElement;
      if (this.isOpen() && !dialog.open) {
        this.returnFocus = this.document.activeElement as HTMLElement | null;
        dialog.showModal();
      } else if (!this.isOpen() && dialog.open) {
        dialog.close();
        // Esperar a que la vista quite la fila eliminada antes de restaurar foco.
        queueMicrotask(() => {
          if (dialog.open || !dialog.isConnected) return;
          const target = this.returnFocus?.isConnected ? this.returnFocus
            : dialog.parentElement?.closest('dialog')?.querySelector<HTMLElement>('[autofocus]');
          target?.focus({ preventScroll: true });
        });
      }
    });
    inject(DestroyRef).onDestroy(() => {
      const dialog = this.dialog().nativeElement;
      if (dialog.open) dialog.close();
    });
  }

  cancel(event?: Event): void {
    event?.preventDefault();
    event?.stopPropagation();
    if (!this.busy()) this.cancelled.emit();
  }

  confirm(): void {
    if (this.isOpen() && !this.busy()) this.confirmed.emit();
  }
}
