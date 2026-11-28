import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { OneRepMaxProgressView } from '../../core/planilla-api';

@Component({
  selector: 'app-one-rep-max-progress',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <details [open]="expanded()">
      <summary>
        <span><strong>Comparación con RM histórico</strong><small>{{ summary() }}</small></span>
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><path d="m6 9 6 6 6-6" stroke-linecap="round" stroke-linejoin="round" /></svg>
      </summary>
      <div class="comparison-content">
      @for (item of progress(); track item.exercise) {
        <article><h3>{{ item.exercise }}</h3><p>{{ item.description }}</p></article>
      } @empty {
        <p>Sin referencias de RM guardadas para esta planilla.</p>
      }
      <p class="interpretation">El porcentaje expresa una relación de cargas; por sí solo no indica mejora ni retroceso de fuerza.</p>
      </div>
    </details>
  `,
  styles: `
    :host { display: block; margin: 20px 0; }
    details { border: 1px solid var(--borde, #dce4df); border-radius: 10px; }
    summary { display: flex; align-items: center; justify-content: space-between; gap: 12px; min-height: 60px; padding: 12px 16px; cursor: pointer; list-style: none; }
    summary::-webkit-details-marker { display: none; }
    summary:focus-visible { outline: 2px solid var(--acento-suave); outline-offset: 2px; border-radius: 10px; }
    summary strong { display: block; font-size: 14px; }
    summary small { display: block; margin-top: 4px; font-size: 12px; opacity: .8; }
    summary svg { width: 18px; height: 18px; flex-shrink: 0; }
    details[open] summary svg { transform: rotate(180deg); }
    .comparison-content { padding: 0 16px 16px; }
    h3 { margin: 12px 0 4px; font-size: 14px; }
    p { margin: 4px 0; font-size: 13px; line-height: 1.6; overflow-wrap: anywhere; }
    .interpretation { margin-top: 14px; font-size: 12px; }
    article { break-inside: avoid; }
  `,
})
export class OneRepMaxProgress {
  readonly progress = input<OneRepMaxProgressView[]>([]);
  readonly expanded = input(false);
  readonly summary = computed(() => {
    const count = this.progress().filter(item => item.referenceKg !== null).length;
    return count === 0
      ? 'Sin RM previo registrado · Ver detalle'
      : `${count} de ${this.progress().length} ejercicios con RM previo · Ver detalle`;
  });
}
