import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthApi } from '../../core/auth-api';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable } from 'rxjs';

@Component({
  selector: 'app-auth-page', imports: [FormsModule, RouterLink],
  templateUrl: './auth-page.html', styleUrl: './auth-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuthPage {
  private readonly auth = inject(AuthApi);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  readonly mode = this.route.snapshot.data['mode'] as 'login' | 'register' | 'forgot' | 'reset';
  readonly title = computed(() => ({ login: 'Iniciá sesión', register: 'Creá tu cuenta de alumno',
    forgot: 'Recuperá tu cuenta', reset: 'Elegí una nueva contraseña' })[this.mode]);
  name = '';
  email = this.mode === 'reset' ? this.route.snapshot.queryParamMap.get('email') ?? '' : '';
  password = '';
  confirmation = '';
  readonly busy = signal(false);
  readonly error = signal('');
  readonly message = signal('');
  readonly passwordVisible = signal(false);
  readonly motionPaused = signal(false);

  submit(): void {
    if (this.busy()) return;
    this.error.set(''); this.message.set('');
    if ((this.mode === 'register' || this.mode === 'reset') && this.password !== this.confirmation) {
      this.error.set('Las contraseñas no coinciden.'); return;
    }
    this.busy.set(true);
    const operation: Observable<unknown> = this.mode === 'register' ? this.auth.register(this.name, this.email, this.password)
      : this.mode === 'forgot' ? this.auth.forgotPassword(this.email)
        : this.mode === 'reset' ? this.auth.resetPassword(this.email, this.route.snapshot.queryParamMap.get('token') ?? '', this.password)
          : this.auth.login(this.email, this.password);
    operation.subscribe({
      next: () => {
        this.busy.set(false); this.password = this.confirmation = '';
        if (this.mode === 'forgot') this.message.set('Si la cuenta existe, recibirás un enlace para restablecer tu contraseña. Revisá también correo no deseado.');
        else if (this.mode === 'reset') {
          this.message.set('Contraseña actualizada. Ya podés iniciar sesión.');
          void this.router.navigate([], { relativeTo: this.route, queryParams: {}, replaceUrl: true });
        } else void this.router.navigateByUrl('/');
      },
      error: (error: unknown) => {
        this.busy.set(false);
        this.error.set(error instanceof HttpErrorResponse && error.status === 429
          ? 'Demasiados intentos. Esperá un minuto antes de volver a probar.'
          : error instanceof HttpErrorResponse && typeof error.error?.title === 'string'
            ? error.error.title : 'No pudimos completar la solicitud. Revisá tu conexión e intentá nuevamente.');
      },
    });
  }
}
