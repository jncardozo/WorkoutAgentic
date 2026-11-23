import { ChangeDetectionStrategy, Component, DestroyRef, ElementRef, computed, effect, inject, input, output, signal, viewChild } from '@angular/core';
import { PlanillaApi, PlanillaConfirmadaVista, fallaDe } from '../../core/planilla-api';
import { DOCUMENT } from '@angular/common';
import { ConfirmationDialog } from '../../shared/confirmation-dialog/confirmation-dialog';

@Component({
  selector: 'app-training-history',
  imports: [ConfirmationDialog],
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
  readonly renamed = output<PlanillaConfirmadaVista>();
  readonly removed = output<number>();
  readonly editingId = signal<number | null>(null);
  readonly draftName = signal('');
  readonly deleting = signal<PlanillaConfirmadaVista | null>(null);
  readonly saving = signal(false);
  readonly actionError = signal<string | null>(null);
  readonly deleteMessage = computed(() => `¿Querés eliminar «${this.title(this.deleting())}» de tu historial? Dejará de estar disponible en la aplicación. Los archivos descargados o enviados previamente seguirán existiendo.`);
  private readonly api = inject(PlanillaApi);
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
    if (this.saving()) return;
    if (this.deleting()) { this.cancelDelete(); return; }
    this.cancelRename();
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
    if (this.saving()) return;
    this.close();
    this.selected.emit(entry);
  }

  title(entry: PlanillaConfirmadaVista | null): string {
    return entry?.name || (entry ? `Entrenamiento #${entry.id}` : 'Entrenamiento');
  }

  editName(entry: PlanillaConfirmadaVista): void {
    if (this.saving()) return;
    this.editingId.set(entry.id);
    this.draftName.set(entry.name ?? '');
    this.actionError.set(null);
  }

  cancelRename(): void {
    if (this.saving()) return;
    this.editingId.set(null);
    this.actionError.set(null);
  }

  saveName(event: Event, entry: PlanillaConfirmadaVista): void {
    event.preventDefault();
    if (this.saving() || this.editingId() !== entry.id) return;
    const name = this.draftName().trim();
    if (name.length > 120 || /[\p{Cc}\p{Zl}\p{Zp}]/u.test(name)) {
      this.actionError.set('Usá un nombre de hasta 120 caracteres y una sola línea.');
      return;
    }
    this.saving.set(true);
    this.actionError.set(null);
    this.api.renameTraining(entry.id, name || null).subscribe({
      next: updated => {
        this.saving.set(false);
        this.editingId.set(null);
        this.renamed.emit(updated);
      },
      error: error => this.failed(error),
    });
  }

  requestDelete(entry: PlanillaConfirmadaVista): void {
    if (this.saving()) return;
    this.cancelRename();
    this.deleting.set(entry);
    this.actionError.set(null);
  }

  cancelDelete(): void {
    if (this.saving()) return;
    this.deleting.set(null);
    this.actionError.set(null);
  }

  deleteTraining(): void {
    const entry = this.deleting();
    if (!entry || this.saving()) return;
    this.saving.set(true);
    this.actionError.set(null);
    this.api.deleteTraining(entry.id).subscribe({
      next: () => {
        this.saving.set(false);
        this.deleting.set(null);
        this.removed.emit(entry.id);
      },
      error: error => this.failed(error),
    });
  }

  private failed(error: unknown): void {
    const problem = fallaDe(error);
    this.actionError.set(problem.detalle ?? problem.titulo);
    this.saving.set(false);
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
