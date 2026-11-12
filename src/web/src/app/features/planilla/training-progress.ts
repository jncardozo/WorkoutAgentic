import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'app-training-progress',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './training-progress.html',
  styleUrl: './training-progress.scss',
})
export class TrainingProgress {
  readonly progress = input.required<{ completas: number; total: number; porcentaje: number }>();
}
