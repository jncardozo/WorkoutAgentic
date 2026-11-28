import { TestBed } from '@angular/core/testing';
import { PlanillaConfirmadaVista } from '../../core/planilla-api';
import { ExerciseLoadChart } from './exercise-load-chart';

type ReportRow = PlanillaConfirmadaVista['filas'][number];
const row = (orden: number, ejercicioCanonical: string, nroSerie: number, pesoKg: number | null,
  unidad: ReportRow['unidad'] = 'Kg'): ReportRow => ({
  orden, ejercicioCanonical, nroSerie, pesoKg, unidad, reps: 10, tiempoSeg: null, incompleta: false,
});

describe('ExerciseLoadChart', () => {
  it('identifica ejercicios, conserva orden de series y usa la escala de cada ejercicio', async () => {
    const fixture = TestBed.createComponent(ExerciseLoadChart);
    fixture.componentRef.setInput('rows', [row(3, 'Remo', 1, 100), row(2, 'Press', 2, 40), row(1, 'Press', 1, 20)]);
    await fixture.whenStable();
    const charts = fixture.nativeElement.querySelectorAll('.exercise-chart') as NodeListOf<HTMLElement>;
    expect([...charts].map(chart => chart.querySelector('h3')?.textContent)).toEqual(['Press', 'Remo']);
    expect([...charts[0].querySelectorAll('.series-heading')].map(series => series.textContent)).toEqual([
      expect.stringContaining('Serie 1'), expect.stringContaining('Serie 2'),
    ]);
    expect([...charts[0].querySelectorAll<HTMLElement>('.load-track > span')].map(bar => bar.style.width)).toEqual(['50%', '100%']);
    expect(charts[0].querySelector('.scale')?.textContent).toContain('40 kg');
    expect(charts[1].querySelector('.scale')?.textContent).toContain('100 kg');
    expect(charts[0].textContent).toContain('10 reps');
  });

  it('no inventa kilos para cargas ausentes, al cuerpo o tiempo ni barras positivas para cero', async () => {
    const fixture = TestBed.createComponent(ExerciseLoadChart);
    fixture.componentRef.setInput('rows', [row(1, 'Press', 1, 0), row(2, 'Press', 2, null),
      row(3, 'Dominadas', 1, null, 'PesoCorporal'), row(4, 'Plancha', 1, null, 'Tiempo')]);
    await fixture.whenStable();
    const host = fixture.nativeElement as HTMLElement;
    expect(host.querySelectorAll('.exercise-chart')).toHaveLength(3);
    expect(host.querySelectorAll('.load-track')).toHaveLength(1);
    expect(host.querySelector<HTMLElement>('.load-track > span')?.style.width).toBe('0%');
    expect(host.textContent).toContain('Sin carga registrada');
    expect(host.textContent).toContain('Al cuerpo');
    expect(host.textContent).toContain('Sin tiempo registrado');
  });
});
