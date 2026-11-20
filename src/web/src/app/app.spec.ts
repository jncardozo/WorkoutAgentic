import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';

import { App } from './app';
import { AuthApi } from './core/auth-api';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([]), provideHttpClient()],
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

  it('agrupa la identidad y el cierre de sesión dentro de Mi cuenta', async () => {
    TestBed.inject(AuthApi).account.set({ studentId: 2, name: 'Alumno de prueba', email: 'alumno@example.test' });
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const root = fixture.nativeElement as HTMLElement;
    const menu = root.querySelector<HTMLDetailsElement>('details.account-menu')!;
    expect(menu.open).toBe(false);
    expect(menu.querySelector('summary')?.textContent).toContain('Mi cuenta');
    expect(menu.querySelector('.account-name')?.textContent).toBe('Alumno de prueba');
    expect(menu.querySelector('button')?.textContent).toContain('Cerrar sesión');
  });

  it('cierra Mi cuenta con Escape y devuelve el foco al control', async () => {
    TestBed.inject(AuthApi).account.set({ studentId: 2, name: 'Alumno', email: 'alumno@example.test' });
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const menu = (fixture.nativeElement as HTMLElement).querySelector<HTMLDetailsElement>('details')!;
    menu.open = true;
    const focus = vi.spyOn(menu.querySelector('summary')!, 'focus');
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    expect(menu.open).toBe(false);
    expect(focus).toHaveBeenCalled();
  });

  it('cierra Mi cuenta al tocar fuera del menú', async () => {
    TestBed.inject(AuthApi).account.set({ studentId: 2, name: 'Alumno', email: 'alumno@example.test' });
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const menu = (fixture.nativeElement as HTMLElement).querySelector<HTMLDetailsElement>('details')!;
    menu.open = true;
    document.body.click();
    expect(menu.open).toBe(false);
  });
});
