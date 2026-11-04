import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { FormatoPlanillaExportable, PlanillaConfirmadaVista } from '../../core/planilla-api';

export type FormatoVistaPrevia = Extract<FormatoPlanillaExportable, 'pdf' | 'excel'>;

@Component({
  selector: 'app-reporte-preview',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './reporte-preview.html',
  styleUrl: './reporte-preview.scss',
})
export class ReportePreview {
  readonly planilla = input.required<PlanillaConfirmadaVista>();
  readonly formato = input.required<FormatoVistaPrevia>();
  readonly cerrar = output<void>();
  readonly descargar = output<FormatoPlanillaExportable>();
  readonly cambiarFormato = output<FormatoVistaPrevia>();

  readonly filas = computed(() => [...this.planilla().filas].sort((a, b) => a.orden - b.orden));
  readonly filasConCarga = computed(() => this.filas().filter((fila) => this.tieneCarga(fila)));
  readonly cargaMaxima = computed(() => {
    const valores = this.filasConCarga().map((fila) => fila.pesoKg).filter((peso): peso is number => peso !== null);
    return valores.length ? Math.max(...valores) : null;
  });
  readonly volumen = computed(() => {
    const calculables = this.filas().filter((fila) => this.tieneCarga(fila) && fila.reps !== null);
    return calculables.length
      ? calculables.reduce((total, fila) => total + fila.pesoKg! * fila.reps!, 0)
      : null;
  });
  readonly ejercicios = computed(() => new Set(this.filas().map((fila) => fila.ejercicioCanonical)).size);

  nombreArchivo(formato: FormatoVistaPrevia = this.formato()): string {
    return `planilla-${this.planilla().id}.${formato === 'excel' ? 'xlsx' : 'pdf'}`;
  }

  imprimir(): void {
    globalThis.print();
  }

  fechaConfirmacion(): string {
    const fecha = new Date(this.planilla().confirmadaUtc);
    return Number.isNaN(fecha.getTime())
      ? 'Fecha no disponible'
      : new Intl.DateTimeFormat('es-AR', { dateStyle: 'medium', timeStyle: 'short' }).format(fecha);
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

  tonelaje(fila: PlanillaConfirmadaVista['filas'][number]): string {
    return this.tieneCarga(fila) && fila.reps !== null
      ? `${this.numero(fila.pesoKg! * fila.reps)} kg·reps`
      : '—';
  }

  anchoBarra(fila: PlanillaConfirmadaVista['filas'][number]): string {
    const maximo = this.cargaMaxima();
    return maximo && this.tieneCarga(fila) ? `${Math.max(4, (fila.pesoKg! / maximo) * 100)}%` : '0%';
  }

  private tieneCarga(fila: PlanillaConfirmadaVista['filas'][number]): boolean {
    return fila.pesoKg !== null && fila.unidad !== 'PesoCorporal' && fila.unidad !== 'Tiempo';
  }
}
