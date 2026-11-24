import { TestBed } from '@angular/core/testing';
import { OneRepMaxProgressView, PlanillaConfirmadaVista } from '../../core/planilla-api';
import { ReportePreview } from './reporte-preview';
import { ReporteWorkspace } from './reporte-workspace';
import { PlanillaApi } from '../../core/planilla-api';
import { of } from 'rxjs';

const comparison: OneRepMaxProgressView = {
  exercise: 'Press de banca', sessionMaxKg: 100, referenceKg: 170,
  referenceConfirmedUtc: '2026-11-24T17:00:00Z', referencePlanillaId: 12,
  referenceRowOrder: 4, percentage: 58.8,
  description: 'Carga máxima de la sesión: 100 kg; 58,8 % del RM de 170 kg (1 repetición), registrado el 24/11/2026, planilla #12, fila 4.',
};
const plan: PlanillaConfirmadaVista = {
  id: 13, alumnoId: 1, sesionId: 20, confirmadaUtc: '2026-12-01T17:00:00Z',
  filas: [{ orden: 1, ejercicioCanonical: 'Press de banca', nroSerie: 1, reps: 1,
    pesoKg: 100, unidad: 'Kg', tiempoSeg: null, incompleta: false, isOneRepMax: true }],
  oneRepMaxProgress: [comparison],
};

describe('Reportes con RM histórico', () => {
  for (const format of ['pdf', 'excel'] as const) {
    it(`muestra el análisis congelado y la marca RM en el workspace ${format}`, async () => {
      TestBed.configureTestingModule({ imports: [ReporteWorkspace], providers: [{ provide: PlanillaApi,
        useValue: { emailConfigurationStatus: () => of({ configured: false }) } }] });
      const fixture = TestBed.createComponent(ReporteWorkspace);
      fixture.componentRef.setInput('planilla', plan);
      fixture.componentRef.setInput('studentId', 1);
      fixture.componentRef.setInput('modo', format);
      await fixture.whenStable();
      const text = (fixture.nativeElement as HTMLElement).textContent;
      expect(text).toContain(comparison.description);
      expect(text).toContain('por sí solo no indica mejora');
      expect(fixture.nativeElement.querySelector('.rm-badge')).not.toBeNull();
    });

    it(`conserva la misma comparación en la vista de impresión ${format}`, async () => {
      TestBed.configureTestingModule({ imports: [ReportePreview], providers: [{ provide: PlanillaApi,
        useValue: { emailConfigurationStatus: () => of({ configured: false }) } }] });
      const fixture = TestBed.createComponent(ReportePreview);
      fixture.componentRef.setInput('planilla', plan);
      fixture.componentRef.setInput('studentId', 1);
      fixture.componentRef.setInput('formato', format);
      await fixture.whenStable();
      expect((fixture.nativeElement as HTMLElement).textContent).toContain(comparison.description);
      expect(fixture.nativeElement.querySelector('.rm-badge')).not.toBeNull();
    });
  }
});
