import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';

import { ProfesoresApi } from '../../core/profesores-api';
import { PlanillaConfirmadaVista } from '../../core/planilla-api';
import type { AlumnoProfesorVista } from '../../core/profesores-api';
import { ProfesorPage } from './profesor-page';

const ALUMNOS: AlumnoProfesorVista[] = [
  {
    id: 2,
    nombre: 'Bruno García',
    telefonoWa: null,
    planillasConfirmadas: 1,
    ultimaConfirmadaUtc: '2026-10-22T14:00:00Z',
  },
  {
    id: 1,
    nombre: 'Lucía Fernández',
    telefonoWa: '5491100112233',
    planillasConfirmadas: 0,
    ultimaConfirmadaUtc: null,
  },
];

function planilla(sobre: Partial<PlanillaConfirmadaVista> = {}): PlanillaConfirmadaVista {
  return {
    id: 9,
    alumnoId: 2,
    sesionId: 4,
    confirmadaUtc: '2026-10-22T14:00:00Z',
    filas: [
      {
        orden: 1,
        ejercicioCanonical: 'Prensa de pierna',
        nroSerie: 1,
        reps: 12,
        pesoKg: 45,
        unidad: 'Kg',
        tiempoSeg: null,
        incompleta: false,
      },
      {
        orden: 2,
        ejercicioCanonical: 'plancha',
        nroSerie: 2,
        reps: null,
        pesoKg: null,
        unidad: null,
        tiempoSeg: 45,
        incompleta: true,
      },
    ],
    ...sobre,
  };
}

interface StubProfesores {
  listarAlumnos: ReturnType<typeof vi.fn>;
  planillasDeAlumno: ReturnType<typeof vi.fn>;
  darDeAlta: ReturnType<typeof vi.fn>;
}

function stubApi(stub: Partial<StubProfesores> = {}): StubProfesores {
  return {
    listarAlumnos: vi.fn(() => of(ALUMNOS)),
    planillasDeAlumno: vi.fn(() => of([planilla()])),
    darDeAlta: vi.fn(() =>
      of({ id: 3, nombre: 'Nadia Sosa', telefonoWa: null }),
    ),
    ...stub,
  };
}

async function montar(
  stub: StubProfesores,
): Promise<ComponentFixture<ProfesorPage>> {
  TestBed.configureTestingModule({
    imports: [ProfesorPage],
    providers: [provideRouter([]), { provide: ProfesoresApi, useValue: stub }],
  });
  const fixture = TestBed.createComponent(ProfesorPage);
  await fixture.whenStable();
  fixture.detectChanges();
  return fixture;
}

function texto(fixture: ComponentFixture<ProfesorPage>): string {
  return (fixture.nativeElement as HTMLElement).textContent ?? '';
}

function boton(fixture: ComponentFixture<ProfesorPage>, buscado: string): HTMLButtonElement {
  const encontrado = [...(fixture.nativeElement as HTMLElement).querySelectorAll('button')].find(
    (actual) => (actual.textContent ?? '').includes(buscado),
  );
  if (!encontrado) {
    throw new Error(`No está el botón: ${buscado}`);
  }
  return encontrado;
}

function escribir<
  Entrada extends HTMLInputElement | HTMLTextAreaElement,
>(fixture: ComponentFixture<ProfesorPage>, selector: string, valor: string): void {
  const entrada = (fixture.nativeElement as HTMLElement).querySelector(
    selector,
  ) as Entrada | null;
  if (!entrada) {
    throw new Error(`No está el campo: ${selector}`);
  }
  entrada.value = valor;
  entrada.dispatchEvent(new Event('input'));
}

describe('ProfesorPage', () => {
  it('muestra el listado de alumnos con el resumen confirmado', async () => {
    const fixture = await montar(stubApi());

    const cuerpo = texto(fixture);
    expect(cuerpo).toContain('Bruno García');
    expect(cuerpo).toContain('Lucía Fernández');
    // Sin WhatsApp ya es válido (ADR-0010): teléfono vacío se ve como guion.
    expect(cuerpo).toContain('—');
    const verBruno = boton(fixture, 'Ver planillas');
    expect(verBruno.disabled).toBe(false);
  });

  it('al seleccionar alumno muestra sus planillas confirmadas en SOLO lectura', async () => {
    const fixture = await montar(stubApi());

    boton(fixture, 'Ver planillas').click();
    await fixture.whenStable();
    fixture.detectChanges();

    const cuerpo = texto(fixture);
    expect(cuerpo).toContain('Planillas confirmadas de Bruno García');
    expect(cuerpo).toContain('Prensa de pierna');
    expect(cuerpo).toContain('incompleta');
    const botones = [...(fixture.nativeElement as HTMLElement).querySelectorAll('button')].map(
      (b) => b.textContent ?? '',
    );
    // Regla de roles: nada editable acá (ni Editar, ni Confirmar, ni Agregar).
    expect(botones.some((nombre) => nombre.includes('Editar'))).toBe(false);
    expect(botones.some((nombre) => nombre.includes('Confirmar'))).toBe(false);
    expect(botones.some((nombre) => nombre.includes('Agregar fila'))).toBe(false);
    expect(botones.some((nombre) => nombre.includes('Volver a la lista'))).toBe(true);
  });

  it('da de alta un alumno sin WhatsApp y lo abre', async () => {
    const stub = stubApi();
    const fixture = await montar(stub);

    boton(fixture, 'Dar de alta alumno').click();
    fixture.detectChanges();
    escribir(fixture, 'input[aria-label="Nombre del alumno"]', 'Nadia Sosa');
    boton(fixture, 'Guardar').click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(stub.darDeAlta).toHaveBeenCalledWith({ nombre: 'Nadia Sosa', telefonoWa: null });
    expect(texto(fixture)).toContain('Planillas confirmadas de Nadia Sosa');
  });

  it('alta sin nombre no llama al API y explica qué falta', async () => {
    const stub = stubApi();
    const fixture = await montar(stub);

    boton(fixture, 'Dar de alta alumno').click();
    fixture.detectChanges();
    boton(fixture, 'Guardar').click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(stub.darDeAlta).not.toHaveBeenCalled();
    expect(texto(fixture)).toContain('Escribí el nombre del alumno.');
  });
});
