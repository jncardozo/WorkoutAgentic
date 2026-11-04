import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { FormatoPlanillaExportable, PlanillaConfirmadaVista } from '../../core/planilla-api';

export type ModoReporte = 'seleccion' | 'pdf' | 'excel';

@Component({
  selector: 'app-reporte-workspace',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './reporte-workspace.html',
  styleUrl: './reporte-workspace.scss',
})
export class ReporteWorkspace {
  readonly planilla = input.required<PlanillaConfirmadaVista>();
  readonly modo = input.required<ModoReporte>();
  readonly cerrar = output<void>();
  readonly seleccionar = output<ModoReporte>();
  readonly imprimir = output<'pdf' | 'excel'>();
  readonly descargar = output<FormatoPlanillaExportable>();

  readonly filas = computed(() => [...this.planilla().filas].sort((a, b) => a.orden - b.orden));
  readonly grupos = computed(() => {
    const porEjercicio = new Map<string, PlanillaConfirmadaVista['filas']>();
    for (const fila of this.filas()) {
      const filas = porEjercicio.get(fila.ejercicioCanonical) ?? [];
      filas.push(fila);
      porEjercicio.set(fila.ejercicioCanonical, filas);
    }
    return [...porEjercicio].map(([nombre, filas]) => ({ nombre, filas }));
  });
  readonly volumen = computed(() => {
    const calculables = this.filas().filter((fila) => this.tieneCarga(fila) && fila.reps !== null);
    return calculables.length
      ? calculables.reduce((total, fila) => total + fila.pesoKg! * fila.reps!, 0)
      : null;
  });
  readonly filasConCarga = computed(() => this.filas().filter((fila) => this.tieneCarga(fila)));
  readonly cargaMaxima = computed(() => {
    const pesos = this.filasConCarga().map((fila) => fila.pesoKg).filter((peso): peso is number => peso !== null);
    return pesos.length ? Math.max(...pesos) : null;
  });
  readonly ejercicios = computed(() => this.grupos().length);

  fechaConfirmacion(): string {
    const fecha = new Date(this.planilla().confirmadaUtc);
    return Number.isNaN(fecha.getTime())
      ? 'Fecha no disponible'
      : new Intl.DateTimeFormat('es-AR', { dateStyle: 'short', timeStyle: 'short' }).format(fecha);
  }

  numero(valor: number | null): string {
    return valor === null
      ? 'Sin datos'
      : new Intl.NumberFormat('es-AR', { maximumFractionDigits: 2 }).format(valor);
  }

  pesoTexto(fila: PlanillaConfirmadaVista['filas'][number]): string {
    if (fila.unidad === 'PesoCorporal') return 'Al cuerpo';
    if (fila.unidad === 'Tiempo') return fila.tiempoSeg === null ? '—' : `${fila.tiempoSeg} s`;
    return fila.pesoKg === null ? '—' : `${this.numero(fila.pesoKg)} kg`;
  }

  volumenFila(fila: PlanillaConfirmadaVista['filas'][number]): string {
    return this.tieneCarga(fila) && fila.reps !== null
      ? `${this.numero(fila.pesoKg! * fila.reps)} kg·reps`
      : '—';
  }

  anchoBarra(fila: PlanillaConfirmadaVista['filas'][number]): string {
    const maximo = this.cargaMaxima();
    return maximo && this.tieneCarga(fila) ? `${Math.max(4, fila.pesoKg! / maximo * 100)}%` : '0%';
  }

  nombreArchivo(formato: 'pdf' | 'excel'): string {
    return `planilla-${this.planilla().id}.${formato === 'excel' ? 'xlsx' : 'pdf'}`;
  }

  private tieneCarga(fila: PlanillaConfirmadaVista['filas'][number]): boolean {
    return fila.pesoKg !== null && fila.unidad !== 'PesoCorporal' && fila.unidad !== 'Tiempo';
  }
}
