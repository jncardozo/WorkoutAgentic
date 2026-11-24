import { Component, DestroyRef, ElementRef, viewChild, computed, effect, inject, input, output, signal, untracked } from '@angular/core';
import { BulkRowEdit, ComandoEdicionFila, FilaParcialVista, PlanillaApi, RespuestaSesionActual, fallaDe } from '../../core/planilla-api';

interface DraftRow {
  id: number;
  group: string;
  original: ComandoEdicionFila;
  originalName: string;
  name: string;
  number: string;
  reps: string;
  weight: string;
  comments: string;
  observations: string;
  isOneRepMax: boolean;
}
type TextField = 'name' | 'number' | 'reps' | 'weight' | 'comments' | 'observations';
@Component({
  selector: 'app-training-editor',
  templateUrl: './training-editor.html',
  styleUrl: './training-editor.scss',
})
export class TrainingEditor {
  readonly rows = input.required<FilaParcialVista[]>();
  readonly studentId = input.required<number>();
  readonly sessionId = input.required<number>();
  readonly saved = output<RespuestaSesionActual>();
  readonly closed = output<void>();
  private readonly api = inject(PlanillaApi);
  private readonly leaveDialog = viewChild<ElementRef<HTMLDialogElement>>('leaveDialog');
  readonly drafts = signal<DraftRow[]>([]);
  readonly expanded = signal<Set<string>>(new Set());
  readonly saving = signal(false);
  readonly errors = signal<Record<number, string>>({});
  readonly failure = signal<string | null>(null);
  readonly discardPrompt = signal(false);
  readonly viewportHeight = signal(window.visualViewport?.height ?? window.innerHeight);
  private leaveResolver: ((leave: boolean) => void) | null = null;
  private focusId: number | null = null;

  readonly groups = computed(() => {
    const groups = new Map<string, { key: string; name: string; rows: DraftRow[] }>();
    for (const row of this.drafts()) {
      const group = groups.get(row.group) ?? { key: row.group, name: row.originalName, rows: [] };
      group.rows.push(row);
      groups.set(row.group, group);
    }
    return [...groups.values()];
  });
  readonly changes = computed<BulkRowEdit[]>(() => this.drafts()
    .filter(row => (row.reps.trim() !== '' && !positiveInteger(row.reps))
      || (row.weight.trim() !== '' && decimal(row.weight) === null)
      || JSON.stringify(this.values(row)) !== JSON.stringify(row.original))
    .map(row => ({ id: row.id, original: row.original, values: this.values(row) })));

  constructor() {
    effect(() => {
      const rows = this.rows();
      untracked(() => {
        const drafts = rows.map(row => this.draft(row));
        this.drafts.set(drafts);
        this.expanded.set(new Set());
      });
    });
    effect(() => {
      const dialog = this.leaveDialog()?.nativeElement;
      if (this.discardPrompt()) { if (dialog && !dialog.open) dialog.showModal(); }
      else if (dialog?.open) dialog.close();
    });
    const onResize = () => this.viewportHeight.set(window.visualViewport?.height ?? window.innerHeight);
    const beforeUnload = (event: BeforeUnloadEvent) => {
      if (this.changes().length || this.saving()) { event.preventDefault(); event.returnValue = ''; }
    };
    window.visualViewport?.addEventListener('resize', onResize);
    window.addEventListener('beforeunload', beforeUnload);
    inject(DestroyRef).onDestroy(() => {
      window.visualViewport?.removeEventListener('resize', onResize);
      window.removeEventListener('beforeunload', beforeUnload);
      this.leaveResolver?.(false);
    });
  }

  private draft(row: FilaParcialVista): DraftRow {
    const name = row.ejercicioCanonico ?? row.ejercicioCrudo ?? '';
    return { id: row.id, group: name.toLocaleLowerCase('es'), originalName: name, name,
      original: { ejercicioId: row.ejercicioId, ejercicioCrudo: row.ejercicioCrudo, nroSerie: row.nroSerie,
        reps: row.reps, pesoKg: row.pesoKg, unidad: row.unidad, tiempoSeg: row.tiempoSeg,
        comments: row.comments ?? null, observations: row.observations ?? null, isOneRepMax: row.isOneRepMax ?? false },
      number: String(row.nroSerie), reps: row.reps === null ? '' : String(row.reps),
      weight: row.pesoKg === null ? '' : String(row.pesoKg),
      comments: row.comments ?? '', observations: row.observations ?? '', isOneRepMax: row.isOneRepMax ?? false };
  }

  private values(row: DraftRow): ComandoEdicionFila {
    const weight = decimal(row.weight);
    const sameName = row.name.trim() === row.originalName;
    const sameLoad = row.weight === (row.original.pesoKg === null ? '' : String(row.original.pesoKg));
    return { ...row.original, isOneRepMax: row.isOneRepMax, ejercicioId: sameName ? row.original.ejercicioId : null,
      ejercicioCrudo: sameName ? row.original.ejercicioCrudo : row.name.trim(),
      nroSerie: Number(row.number), reps: row.reps.trim() ? Number(row.reps) : null,
      pesoKg: sameLoad ? row.original.pesoKg : weight, unidad: sameLoad ? row.original.unidad : weight !== null ? 'Kg' : row.original.unidad === 'Tiempo' ? 'Tiempo' : null,
      comments: row.comments === (row.original.comments ?? '') ? row.original.comments : row.comments.trim() || null,
      observations: row.observations === (row.original.observations ?? '') ? row.original.observations : row.observations.trim() || null };
  }

  update(id: number, field: TextField, value: string): void {
    this.drafts.update(rows => rows.map(row => row.id === id ? { ...row, [field]: value } : row));
    this.errors.update(errors => { const copy = { ...errors }; delete copy[id]; return copy; });
  }
  markOneRepMax(id: number, checked: boolean): void {
    this.drafts.update(rows => rows.map(row => row.id === id ? { ...row, isOneRepMax: checked } : row));
    this.errors.update(errors => { const copy = { ...errors }; delete copy[id]; return copy; });
  }
  toggleGroup(key: string): void {
    this.expanded.update(previous => { const next = new Set(previous); if (next.has(key)) next.delete(key); else next.add(key); return next; });
  }
  save(): void {
    if (this.saving() || !this.changes().length) return;
    const errors: Record<number, string> = {};
    const changed = new Set(this.changes().map(row => row.id));
    for (const row of this.drafts().filter(row => changed.has(row.id))) {
      if (!row.name.trim() || !positiveInteger(row.number) || (row.reps.trim() && !positiveInteger(row.reps))
        || (row.weight.trim() && decimal(row.weight) === null))
        errors[row.id] = 'Revisá el ejercicio, número de serie, repeticiones y peso. Usá números válidos; los datos pendientes pueden quedar vacíos.';
      if (row.isOneRepMax && (Number(row.reps) !== 1 || !(decimal(row.weight)! > 0) || this.values(row).unidad !== 'Kg' || row.original.tiempoSeg !== null))
        errors[row.id] = 'El RM requiere una repetición y un peso mayor que cero en kg.';
    }
    this.errors.set(errors);
    if (Object.keys(errors).length) {
      const target = this.drafts().find(row => errors[row.id]);
      if (target) { this.expanded.update(previous => new Set([...previous, target.group])); this.focusId = target.id; requestAnimationFrame(() => this.focusRow()); }
      return;
    }
    this.saving.set(true); this.failure.set(null);
    this.api.bulkEdit(this.studentId(), this.sessionId(), this.changes()).subscribe({
      next: response => { this.saving.set(false); this.saved.emit(response); },
      error: error => { this.saving.set(false); const failure = fallaDe(error); this.failure.set(failure.detalle ?? failure.titulo); },
    });
  }

  requestLeave(): Promise<boolean> {
    if (this.saving()) return Promise.resolve(false);
    if (!this.changes().length) return Promise.resolve(true);
    if (this.leaveResolver) return Promise.resolve(false);
    this.discardPrompt.set(true);
    return new Promise(resolve => { this.leaveResolver = resolve; });
  }
  resolveLeave(discard: boolean): void {
    this.discardPrompt.set(false); this.leaveResolver?.(discard); this.leaveResolver = null;
  }
  async cancel(): Promise<void> { if (await this.requestLeave()) this.closed.emit(); }
  private focusRow(): void { document.getElementById(`edit-row-${this.focusId}`)?.scrollIntoView?.({ block: 'center' }); }
}

function positiveInteger(value: string): boolean { return value.trim() !== '' && Number.isSafeInteger(Number(value)) && Number(value) > 0 && Number(value) <= 2_147_483_647; }
function decimal(value: string): number | null {
  if (!value.trim()) return null;
  const number = Number(value.replace(',', '.'));
  return Number.isFinite(number) && number >= 0 ? number : null;
}
