import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AuthApi } from './auth-api';

describe('AuthApi', () => {
  let api: AuthApi;
  let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(AuthApi); http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());
  it('renueva CSRF tras login antes de obtener el alumno autenticado', () => {
    const account = { studentId: 23, name: 'Tester', email: 'a@example.test' };
    api.login(account.email, 'mi frase de prueba').subscribe(result => expect(result).toEqual(account));
    http.expectOne('/api/auth/csrf').flush(null);
    const login = http.expectOne('/api/auth/login');
    expect(login.request.body).toEqual({ email: account.email, password: 'mi frase de prueba' });
    login.flush(null);
    http.expectOne('/api/auth/csrf').flush(null);
    http.expectOne('/api/auth/me').flush(account);
    expect(api.account()).toEqual(account);
  });
  it('el registro crea una cuenta con nombre correo y contraseña antes de iniciar sesión', () => {
    api.register('Tester', 'a@example.test', 'mi frase de prueba').subscribe();
    http.expectOne('/api/auth/csrf').flush(null);
    const registration = http.expectOne('/api/alumnos');
    expect(registration.request.body).toEqual({ name: 'Tester', email: 'a@example.test', password: 'mi frase de prueba' });
    registration.flush({ studentId: 7 });
    http.expectOne('/api/auth/csrf').flush(null);
    http.expectOne('/api/auth/login').flush(null);
    http.expectOne('/api/auth/csrf').flush(null);
    http.expectOne('/api/auth/me').flush({ studentId: 7, name: 'Tester', email: 'a@example.test' });
  });
  it('una sesión vencida limpia la identidad local', () => {
    api.account.set({ studentId: 7, name: 'Anterior', email: 'a@example.test' });
    api.load().subscribe(result => expect(result).toBeNull());
    http.expectOne('/api/auth/csrf').flush(null);
    http.expectOne('/api/auth/me').flush({}, { status: 401, statusText: 'Unauthorized' });
    expect(api.account()).toBeNull();
  });
});
