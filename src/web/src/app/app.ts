import { Component, ElementRef, inject, signal, viewChild } from '@angular/core';
import { Router, RouterLink, RouterOutlet } from '@angular/router';
import { AuthApi, signOut } from './core/auth-api';
import { PwaInstall } from './shared/pwa-install/pwa-install';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, PwaInstall],
  templateUrl: './app.html',
  styleUrl: './app.scss',
  host: {
    '(document:click)': 'closeAccountOnOutsideClick($event)',
    '(document:keydown.escape)': 'closeAccountMenu(true)',
  },
})
export class App {
  readonly auth = inject(AuthApi);
  private readonly router = inject(Router);
  readonly signingOut = signal(false);
  readonly logoutError = signal('');
  private readonly accountMenu = viewChild<ElementRef<HTMLDetailsElement>>('accountMenu');

  closeAccountMenu(restoreFocus = false): void {
    const menu = this.accountMenu()?.nativeElement;
    if (!menu?.open) return;
    menu.open = false;
    if (restoreFocus) menu.querySelector('summary')?.focus();
  }

  closeAccountOnOutsideClick(event: MouseEvent): void {
    const menu = this.accountMenu()?.nativeElement;
    if (menu && event.target instanceof Node && !menu.contains(event.target)) this.closeAccountMenu();
  }

  async logout(): Promise<void> {
    this.closeAccountMenu();
    this.signingOut.set(true); this.logoutError.set('');
    try { await signOut(this.auth, this.router); }
    catch { this.logoutError.set('No pudimos cerrar la sesión. Intentá nuevamente.'); }
    finally { this.signingOut.set(false); }
  }
}
