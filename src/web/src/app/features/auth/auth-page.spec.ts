import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { AuthPage } from './auth-page';
import { AuthApi, requireAccount } from '../../core/auth-api';

describe('Cuentas de alumno', () => {
  const auth = {
    account: signal(null), load: vi.fn(), login: vi.fn(), register: vi.fn(),
    forgotPassword: vi.fn(), resetPassword: vi.fn(),
  };
  beforeEach(() => {
    vi.resetAllMocks();
    auth.account.set(null);
    TestBed.configureTestingModule({ providers: [
      { provide: AuthApi, useValue: auth },
      provideRouter([
        { path: 'login', component: AuthPage, data: { mode: 'login' } },
        { path: 'registro', component: AuthPage, data: { mode: 'register' } },
        { path: 'recuperar-clave', component: AuthPage, data: { mode: 'forgot' } },
        { path: 'restablecer-clave', component: AuthPage, data: { mode: 'reset' } },
        { path: 'acuerdo-qa', component: AuthPage, canActivate: [requireAccount], data: { mode: 'login', allowWithoutAgreement: true } },
        { path: '', canActivate: [requireAccount], component: AuthPage, data: { mode: 'login' } },
      ]),
    ] });
  });
  it('muestra el registro accesible y rechaza contraseñas distintas sin enviar', async () => {
    const harness = await RouterTestingHarness.create();
    const page = await harness.navigateByUrl('/registro', AuthPage);
    expect(harness.routeNativeElement?.textContent).toContain('Creá tu cuenta de alumno');
    expect(harness.routeNativeElement?.querySelector('input[autocomplete="name"]')).not.toBeNull();
    page.password = 'una contraseña larga'; page.confirmation = 'otra contraseña larga';
    page.submit(); harness.detectChanges();
    expect(auth.register).not.toHaveBeenCalled();
    expect(harness.routeNativeElement?.querySelector('[role="alert"]')?.textContent).toContain('no coinciden');
  });
  it('conserva los datos y muestra el rechazo del login', async () => {
    auth.login.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 401, error: { title: 'Revisá tus datos.' } })));
    const harness = await RouterTestingHarness.create();
    const page = await harness.navigateByUrl('/login', AuthPage);
    page.email = 'tester@example.test'; page.password = 'clave incorrecta'; page.submit(); harness.detectChanges();
    expect(page.email).toBe('tester@example.test');
    expect(page.busy()).toBe(false);
    expect(harness.routeNativeElement?.textContent).toContain('Revisá tus datos.');
  });
  it('sin sesión lleva al login antes de abrir la planilla', async () => {
    auth.load.mockReturnValue(of(null));
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/');
    expect(TestBed.inject(Router).url).toBe('/login');
  });
  it('una cuenta existente sin aceptación debe pasar por el acuerdo', async () => {
    auth.load.mockReturnValue(of({ studentId: 2, name: 'QA', email: 'qa@example.test', agreementAccepted: false }));
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/');
    expect(TestBed.inject(Router).url).toBe('/acuerdo-qa');
  });
  it('una cuenta con aceptación vigente puede abrir la planilla', async () => {
    auth.load.mockReturnValue(of({ studentId: 2, name: 'QA', email: 'qa@example.test', agreementAccepted: true }));
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/');
    expect(TestBed.inject(Router).url).toBe('/');
  });
  it('solicita recuperación y muestra una respuesta sin revelar cuentas', async () => {
    auth.forgotPassword.mockReturnValue(of({}));
    const harness = await RouterTestingHarness.create();
    const page = await harness.navigateByUrl('/recuperar-clave', AuthPage);
    page.email = 'tester@example.test'; page.submit(); harness.detectChanges();
    expect(auth.forgotPassword).toHaveBeenCalledWith('tester@example.test');
    expect(harness.routeNativeElement?.querySelector('[role="status"]')?.textContent).toContain('Si la cuenta existe');
  });
  it('envía el token recibido y limpia las contraseñas después de restablecer', async () => {
    auth.resetPassword.mockReturnValue(of(null));
    const harness = await RouterTestingHarness.create();
    const page = await harness.navigateByUrl('/restablecer-clave?email=a%40example.test&token=token-prueba', AuthPage);
    page.password = page.confirmation = 'mi nueva frase secreta'; page.submit();
    expect(auth.resetPassword).toHaveBeenCalledWith('a@example.test', 'token-prueba', 'mi nueva frase secreta');
    expect(page.password).toBe('');
  });
});
