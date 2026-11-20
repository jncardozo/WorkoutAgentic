import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AuthApi } from '../../core/auth-api';
import { AgreementApi } from './agreement-api';

describe('AgreementApi', () => {
  it('envía versión y huella con CSRF y vuelve a consultar la aceptación persistida', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const http = TestBed.inject(HttpTestingController);
    const document = { version: 'v1', hash: 'hash', title: 'Acuerdo', text: 'Texto', available: true };
    TestBed.inject(AgreementApi).accept(document).subscribe();
    http.expectOne('/api/auth/csrf').flush(null);
    const request = http.expectOne('/api/legal/accept');
    expect(request.request.body).toEqual({ version: 'v1', hash: 'hash', accepted: true });
    request.flush(null);
    http.expectOne('/api/auth/csrf').flush(null);
    http.expectOne('/api/auth/me').flush({ studentId: 1, name: 'QA', email: 'qa@example.test', agreementAccepted: true });
    expect(TestBed.inject(AuthApi).account()?.agreementAccepted).toBe(true);
    http.verify();
  });
});
