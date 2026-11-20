import { ChangeDetectionStrategy, Component, DestroyRef, ElementRef, computed, effect, inject, input, output, viewChild } from '@angular/core';
import { PlanillaConfirmadaVista } from '../../core/planilla-api';
import { DOCUMENT } from '@angular/common';

@Component({
  selector: 'app-training-history',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './training-history.html',
  styleUrl: './training-history.scss',
})
export class TrainingHistory {
  readonly entries = input.required<PlanillaConfirmadaVista[]>();
  readonly isOpen = input(false);
  readonly loading = input(false);
  readonly error = input<string | null>(null);
  readonly opened = output<void>();
  readonly closed = output<void>();
  readonly selected = output<PlanillaConfirmadaVista>();
  readonly retry = output<void>();
  readonly orderedEntries = computed(() => [...this.entries()].sort(
    (a, b) => Date.parse(b.confirmadaUtc) - Date.parse(a.confirmadaUtc) || b.id - a.id,
  ));
  private readonly dialog = viewChild<ElementRef<HTMLDialogElement>>('historyDialog');
  private readonly trigger = viewChild<ElementRef<HTMLButtonElement>>('historyTrigger');
  private readonly document = inject(DOCUMENT);
  private scrollPosition: { left: number; top: number } | null = null;

  constructor() {
    effect(() => {
      const dialog = this.dialog()?.nativeElement;
      if (!dialog) return;
      if (this.isOpen() && !dialog.open) {
        const view = this.document.defaultView;
        this.scrollPosition = view ? { left: view.scrollX, top: view.scrollY } : null;
        dialog.showModal();
        this.restoreScroll();
      }
      else if (!this.isOpen() && dialog.open) dialog.close();
    });
    inject(DestroyRef).onDestroy(() => {
      const dialog = this.dialog()?.nativeElement;
      if (dialog?.open) dialog.close();
    });
  }

  close(event?: Event): void {
    event?.preventDefault();
    this.dialog()?.nativeElement.close();
  }

  onClosed(): void {
    // A queued close event must not close a dialog that has just reopened.
    if (this.dialog()?.nativeElement.open) return;
    this.closed.emit();
    const trigger = this.trigger()?.nativeElement;
    if (trigger?.isConnected) trigger.focus({ preventScroll: true });
    this.restoreScroll();
  }

  private restoreScroll(): void {
    const view = this.document.defaultView;
    const position = this.scrollPosition;
    if (view && position && (view.scrollX !== position.left || view.scrollY !== position.top)) {
      view.scrollTo(position.left, position.top);
    }
  }

  closeOnBackdrop(event: MouseEvent): void {
    const dialog = this.dialog()?.nativeElement;
    if (!dialog || event.target !== dialog) return;
    const bounds = dialog.getBoundingClientRect();
    if (event.clientX < bounds.left || event.clientX > bounds.right
      || event.clientY < bounds.top || event.clientY > bounds.bottom) this.close();
  }

  select(entry: PlanillaConfirmadaVista): void {
    this.close();
    this.selected.emit(entry);
  }

  date(value: string): string {
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? 'Fecha no disponible'
      : new Intl.DateTimeFormat('es-AR', { day: 'numeric', month: 'long', year: 'numeric' }).format(date);
  }

  time(value: string): string {
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? 'Hora no disponible'
      : new Intl.DateTimeFormat('es-AR', { hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }).format(date);
  }
}
