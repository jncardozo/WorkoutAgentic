import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { PlanillaConfirmadaVista } from '../../core/planilla-api';

type ReportRow = PlanillaConfirmadaVista['filas'][number];

@Component({
  selector: 'app-exercise-load-chart',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './exercise-load-chart.html',
  styleUrl: './exercise-load-chart.scss',
})
export class ExerciseLoadChart {
  readonly rows = input.required<ReportRow[]>();
  readonly paper = input(false);
  readonly groups = computed(() => {
    const groups = new Map<string, ReportRow[]>();
    for (const row of [...this.rows()].sort((a, b) => a.orden - b.orden)) {
      const rows = groups.get(row.ejercicioCanonical) ?? [];
      rows.push(row);
      groups.set(row.ejercicioCanonical, rows);
    }
    return [...groups].map(([exercise, rows]) => {
      const loads = rows.filter(row => this.hasLoad(row)).map(row => row.pesoKg!);
      return { exercise, rows, maxKg: loads.length ? Math.max(...loads) : null };
    });
  });

  hasLoad(row: ReportRow): boolean {
    return row.pesoKg !== null && row.unidad !== 'PesoCorporal' && row.unidad !== 'Tiempo';
  }

  width(row: ReportRow, maxKg: number | null): string {
    return this.hasLoad(row) && maxKg ? `${row.pesoKg! / maxKg * 100}%` : '0%';
  }

  loadLabel(row: ReportRow): string {
    if (row.unidad === 'PesoCorporal') return 'Al cuerpo';
    if (row.unidad === 'Tiempo') return row.tiempoSeg === null ? 'Sin tiempo registrado' : `${row.tiempoSeg} s`;
    return row.pesoKg === null ? 'Sin carga registrada' : `${this.number(row.pesoKg)} kg`;
  }

  number(value: number): string {
    return new Intl.NumberFormat('es-AR', { maximumFractionDigits: 2 }).format(value);
  }
}
