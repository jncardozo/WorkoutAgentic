import { Injectable, inject, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, firstValueFrom, map, of, switchMap, tap, throwError } from 'rxjs';

export interface Account { studentId: number; name: string; email: string; agreementAccepted?: boolean }

@Injectable({ providedIn: 'root' })
export class AuthApi {
  private readonly http = inject(HttpClient);
  readonly account = signal<Account | null>(null);
  csrf() { return this.http.get<void>('/api/auth/csrf'); }
  load() {
    return this.csrf().pipe(
      switchMap(() => this.http.get<Account>('/api/auth/me')),
      tap(account => this.account.set(account)),
      catchError((error: unknown) => {
        this.account.set(null);
        return error instanceof HttpErrorResponse && error.status === 401 ? of(null) : throwError(() => error);
      }),
    );
  }
  login(email: string, password: string) {
    return this.csrf().pipe(
      switchMap(() => this.http.post<void>('/api/auth/login', { email, password })),
      switchMap(() => this.load()),
    );
  }
  register(name: string, email: string, password: string) {
    return this.csrf().pipe(
      switchMap(() => this.http.post<Account>('/api/alumnos', { name, email, password })),
      switchMap(() => this.login(email, password)),
    );
  }
  logout() {
    return this.csrf().pipe(
      switchMap(() => this.http.post<void>('/api/auth/logout', {})),
      tap(() => this.account.set(null)), switchMap(() => this.csrf()),
    );
  }
  forgotPassword(email: string) {
    return this.csrf().pipe(switchMap(() => this.http.post('/api/auth/forgot-password', { email })));
  }
  resetPassword(email: string, token: string, password: string) {
    return this.csrf().pipe(switchMap(() => this.http.post('/api/auth/reset-password', { email, token, password })));
  }
}

export const requireAccount: CanActivateFn = route => {
  const auth = inject(AuthApi);
  const router = inject(Router);
  return auth.load().pipe(
    map(account => !account ? router.createUrlTree(['/login'])
      : route.data['allowWithoutAgreement'] || account.agreementAccepted === true
        ? true : router.createUrlTree(['/acuerdo-qa'])),
    catchError(() => of(router.createUrlTree(['/login']))),
  );
};

export const sessionInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthApi);
  const router = inject(Router);
  return next(request).pipe(catchError((error: unknown) => {
    if (error instanceof HttpErrorResponse && error.status === 403
      && error.error?.code === 'agreement_required' && request.url.startsWith('/api/')) {
      void router.navigateByUrl('/acuerdo-qa');
    }
    if (error instanceof HttpErrorResponse && error.status === 401
      && request.url.startsWith('/api/') && !request.url.startsWith('/api/auth/')) {
      auth.account.set(null);
      void router.navigateByUrl('/login');
    }
    return throwError(() => error);
  }));
};

export async function signOut(auth: AuthApi, router: Router): Promise<void> {
  if (await router.navigateByUrl('/login')) await firstValueFrom(auth.logout());
}
