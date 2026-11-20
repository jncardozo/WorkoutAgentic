import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter, Router } from '@angular/router';
import { signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { of, Subject, throwError } from 'rxjs';
import { Account, AuthApi } from '../../core/auth-api';
import { AgreementApi, AgreementDocument } from './agreement-api';
import { AgreementPage } from './agreement-page';

describe('Acuerdo de la prueba', () => {
  const account: Account = { studentId: 2, name: 'QA', email: 'qa@example.test', agreementAccepted: false };
  const document: AgreementDocument = {
    version: 'v1', hash: 'hash-v1', title: 'Acuerdo', text: 'Texto completo del acuerdo de prueba.', available: true,
  };
  const auth = { account: signal<Account | null>(account), logout: vi.fn() };
  const api = { load: vi.fn(), accept: vi.fn() };

  beforeEach(() => {
    vi.resetAllMocks();
    auth.account.set(account);
    api.load.mockReturnValue(of(document));
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: AuthApi, useValue: auth },
        { provide: AgreementApi, useValue: api },
        { provide: ActivatedRoute, useValue: { snapshot: { data: {} } } }],
    });
  });

  it('muestra el texto completo sin aceptar al leer y exige una casilla explícita', async () => {
    const fixture = TestBed.createComponent(AgreementPage);
    await fixture.whenStable();
    const root = fixture.nativeElement as HTMLElement;
    expect(root.querySelector('article')?.textContent).toContain(document.text);
    const checkbox = root.querySelector<HTMLInputElement>('input[type="checkbox"]')!;
    const submit = root.querySelector<HTMLButtonElement>('button[type="submit"]')!;
    expect(checkbox.checked).toBe(false);
    expect(submit.disabled).toBe(true);
    fixture.componentInstance.accept();
    expect(api.accept).not.toHaveBeenCalled();
    checkbox.checked = true;
    checkbox.dispatchEvent(new Event('change', { bubbles: true }));
    fixture.detectChanges();
    expect(submit.disabled).toBe(false);
  });

  it('espera la confirmación del servidor antes de continuar y evita doble envío', async () => {
    const pending = new Subject<Account>();
    api.accept.mockReturnValue(pending);
    const router = TestBed.inject(Router);
    const navigate = vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);
    const fixture = TestBed.createComponent(AgreementPage);
    await fixture.whenStable();
    fixture.componentInstance.consent = true;
    fixture.componentInstance.accept();
    fixture.componentInstance.accept();
    expect(api.accept).toHaveBeenCalledExactlyOnceWith(document);
    expect(navigate).not.toHaveBeenCalled();
    pending.next({ ...account, agreementAccepted: true });
    pending.complete();
    expect(navigate).toHaveBeenCalledWith('/');
  });

  it('conserva el bloqueo si falla el guardado y permite reintentar', async () => {
    api.accept.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 500 })));
    const fixture = TestBed.createComponent(AgreementPage);
    await fixture.whenStable();
    fixture.componentInstance.consent = true;
    fixture.componentInstance.accept();
    fixture.detectChanges();
    expect(fixture.componentInstance.busy()).toBe(false);
    expect(fixture.nativeElement.querySelector('[role="alert"]').textContent).toContain('No pudimos guardar');
    expect(auth.account()?.agreementAccepted).toBe(false);
  });

  it('una versión vencida exige cargar y revisar otra vez sin reutilizar la casilla', async () => {
    api.accept.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 409, error: { title: 'El acuerdo cambió.' } })));
    const fixture = TestBed.createComponent(AgreementPage);
    await fixture.whenStable();
    fixture.componentInstance.consent = true;
    fixture.componentInstance.accept();
    expect(fixture.componentInstance.consent).toBe(false);
    expect(fixture.componentInstance.document()).toBeNull();
    api.load.mockReturnValue(of({ ...document, version: 'v2', hash: 'hash-v2' }));
    fixture.componentInstance.reload();
    expect(fixture.componentInstance.document()?.version).toBe('v2');
    expect(fixture.componentInstance.consent).toBe(false);
  });

  it('al rechazar cierra sesión sin registrar una aceptación', async () => {
    auth.logout.mockReturnValue(of(null));
    vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    const fixture = TestBed.createComponent(AgreementPage);
    await fixture.whenStable();
    await fixture.componentInstance.leave();
    expect(auth.logout).toHaveBeenCalledOnce();
    expect(api.accept).not.toHaveBeenCalled();
  });

  it('permite consultar y descargar la constancia después de aceptar', async () => {
    auth.account.set({ ...account, agreementAccepted: true });
    const fixture = TestBed.createComponent(AgreementPage);
    await fixture.whenStable();
    const root = fixture.nativeElement as HTMLElement;
    expect(root.querySelector('input[type="checkbox"]')).toBeNull();
    expect(root.querySelector('a[href="/api/legal/receipt"]')).not.toBeNull();
    expect(root.textContent).toContain('Ya aceptaste');
  });

  it('permite leer el documento sin sesión desde el enlace público', async () => {
    auth.account.set(null);
    TestBed.inject(ActivatedRoute).snapshot.data['preview'] = true;
    const fixture = TestBed.createComponent(AgreementPage);
    await fixture.whenStable();
    const root = fixture.nativeElement as HTMLElement;
    expect(root.querySelector('article')?.textContent).toContain(document.text);
    expect(root.querySelector('input[type="checkbox"]')).toBeNull();
  });
});
