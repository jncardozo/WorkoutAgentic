import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AuthApi, signOut } from '../../core/auth-api';
import { AgreementApi, AgreementDocument } from './agreement-api';

@Component({
  selector: 'app-agreement-page',
  imports: [FormsModule, RouterLink],
  templateUrl: './agreement-page.html',
  styleUrl: './agreement-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AgreementPage {
  readonly auth = inject(AuthApi);
  private readonly api = inject(AgreementApi);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  readonly preview = inject(ActivatedRoute).snapshot.data['preview'] === true;
  readonly document = signal<AgreementDocument | null>(null);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly loading = signal(true);
  consent = false;

  constructor() {
    this.reload();
  }

  reload(): void {
    if (this.busy()) return;
    this.consent = false;
    this.loading.set(true); this.error.set(''); this.document.set(null);
    this.api.load().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: document => { this.document.set(document); this.loading.set(false); },
      error: () => { this.error.set('No pudimos cargar el acuerdo. Revisá tu conexión y volvé a intentarlo.'); this.loading.set(false); },
    });
  }

  accept(): void {
    const document = this.document();
    if (!document?.available || !this.consent || this.busy()) return;
    this.busy.set(true); this.error.set('');
    this.api.accept(document).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: account => {
        this.busy.set(false);
        if (account?.agreementAccepted) void this.router.navigateByUrl('/');
        else this.error.set('No pudimos verificar la aceptación. Volvé a iniciar sesión.');
      },
      error: (error: unknown) => {
        this.busy.set(false);
        this.error.set(error instanceof HttpErrorResponse && typeof error.error?.title === 'string'
          ? error.error.title : 'No pudimos guardar la aceptación. Intentá nuevamente.');
        if (error instanceof HttpErrorResponse && error.status === 409) {
          this.consent = false;
          this.document.set(null);
        }
      },
    });
  }

  async leave(): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true); this.error.set('');
    try { await signOut(this.auth, this.router); }
    catch { this.error.set('No pudimos cerrar la sesión. Intentá nuevamente.'); }
    finally { this.busy.set(false); }
  }

  download(): void {
    const document = this.document();
    if (!document) return;
    const url = URL.createObjectURL(new Blob([document.text], { type: 'text/plain;charset=utf-8' }));
    const anchor = window.document.createElement('a');
    anchor.href = url;
    anchor.download = `WorkoutAgentic-acuerdo-${document.version}.txt`;
    anchor.click();
    window.setTimeout(() => URL.revokeObjectURL(url), 1000);
  }
}
