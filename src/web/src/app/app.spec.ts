import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { App } from './app';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([])],
    }).compileComponents();
  });

  it('muestra la marca en el encabezado', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    fixture.detectChanges();

    const marca = (fixture.nativeElement as HTMLElement).querySelector('.marca-nombre');
    expect(marca?.textContent).toContain('WorkoutAgentic');
  });

  it('delega el contenido al router-outlet', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    fixture.detectChanges();

    const contenido = (fixture.nativeElement as HTMLElement).querySelector('main.contenido');
    expect(contenido?.querySelector('router-outlet')).not.toBeNull();
  });
});
