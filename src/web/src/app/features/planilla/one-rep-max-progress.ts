import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { OneRepMaxProgressView } from '../../core/planilla-api';

@Component({
  selector: 'app-one-rep-max-progress',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section aria-label="Comparación con RM histórico">
      <h2>Comparación con RM histórico</h2>
      @for (item of progress(); track item.exercise) {
        <article><h3>{{ item.exercise }}</h3><p>{{ item.description }}</p></article>
      } @empty {
        <p>Sin referencias de RM guardadas para esta planilla.</p>
      }
      <p class="interpretation">El porcentaje expresa una relación de cargas; por sí solo no indica mejora ni retroceso de fuerza.</p>
    </section>
  `,
  styles: `
    :host { display: block; margin: 20px 0; }
    section { padding: 16px; border: 1px solid currentColor; border-radius: 8px; }
    h2 { margin: 0 0 12px; font-size: 18px; }
    h3 { margin: 12px 0 4px; font-size: 14px; }
    p { margin: 4px 0; font-size: 13px; line-height: 1.6; overflow-wrap: anywhere; }
    .interpretation { margin-top: 14px; font-size: 12px; }
    article { break-inside: avoid; }
  `,
})
export class OneRepMaxProgress {
  readonly progress = input<OneRepMaxProgressView[]>([]);
}
