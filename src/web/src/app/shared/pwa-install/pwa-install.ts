import { DOCUMENT } from '@angular/common';
import { Component, DestroyRef, ElementRef, computed, inject, signal, viewChild } from '@angular/core';

interface InstallPromptEvent extends Event {
  prompt(): Promise<void>;
  readonly userChoice: Promise<{ outcome: 'accepted' | 'dismissed' }>;
}

@Component({
  selector: 'app-pwa-install',
  templateUrl: './pwa-install.html',
  styleUrl: './pwa-install.scss',
})
export class PwaInstall {
  private readonly window = inject(DOCUMENT).defaultView;
  private readonly destroyRef = inject(DestroyRef);
  private readonly help = viewChild<ElementRef<HTMLDialogElement>>('help');
  private readonly installButton = viewChild<ElementRef<HTMLButtonElement>>('installButton');
  private readonly deferredPrompt = signal<InstallPromptEvent | null>(null);
  private readonly dismissed = signal(false);
  private readonly installed = signal(false);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly iosSafari = signal(false);
  readonly platform = signal<'android' | 'ios' | 'mobile' | null>(null);
  readonly availabilityMessage = signal('El navegador todavía no ofrece la instalación directa. Podés intentar desde su menú o seguir usando la app desde acá.');
  readonly visible = computed(() => !this.installed() && !this.dismissed()
    && this.platform() !== null);

  constructor() {
    const browser = this.window;
    if (!browser) return;
    const navigator = browser.navigator as Navigator & { standalone?: boolean; userAgentData?: { platform?: string } };
    const userAgent = navigator.userAgent;
    const android = /Android/i.test(userAgent) || navigator.userAgentData?.platform === 'Android';
    const ios = /iPhone|iPad|iPod/i.test(userAgent)
      || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);
    const touchMobile = browser.matchMedia?.('(pointer: coarse)').matches
      && browser.matchMedia?.('(max-width: 900px)').matches;
    this.platform.set(android ? 'android' : ios ? 'ios' : touchMobile ? 'mobile' : null);
    this.iosSafari.set(ios && /Safari/i.test(userAgent)
      && !/CriOS|FxiOS|EdgiOS|OPiOS|DuckDuckGo|GSA/i.test(userAgent));
    const standalone = browser.matchMedia?.('(display-mode: standalone)');
    const fullscreen = browser.matchMedia?.('(display-mode: fullscreen)');
    const updateDisplayMode = () => {
      if (navigator.standalone || standalone?.matches || fullscreen?.matches) this.markInstalled();
    };
    updateDisplayMode();

    const onPrompt = (event: Event) => {
      if (!this.platform() || this.installed()) return;
      event.preventDefault();
      this.deferredPrompt.set(event as InstallPromptEvent);
      this.error.set('');
    };
    const onInstalled = () => this.markInstalled();
    browser.addEventListener('beforeinstallprompt', onPrompt);
    browser.addEventListener('appinstalled', onInstalled);
    standalone?.addEventListener('change', updateDisplayMode);
    fullscreen?.addEventListener('change', updateDisplayMode);
    this.destroyRef.onDestroy(() => {
      browser.removeEventListener('beforeinstallprompt', onPrompt);
      browser.removeEventListener('appinstalled', onInstalled);
      standalone?.removeEventListener('change', updateDisplayMode);
      fullscreen?.removeEventListener('change', updateDisplayMode);
    });
  }

  async install(): Promise<void> {
    if (this.busy() || this.installed()) return;
    const prompt = this.deferredPrompt();
    if (!prompt) {
      this.help()?.nativeElement.showModal();
      return;
    }
    this.busy.set(true);
    this.error.set('');
    // Each browser event can be used only once, including after cancellation.
    this.deferredPrompt.set(null);
    try {
      await prompt.prompt();
      const choice = await prompt.userChoice;
      if (choice.outcome === 'accepted') this.dismissed.set(true);
      else this.availabilityMessage.set('Cancelaste la instalación. El navegador debe habilitar otro intento. También podés intentar desde su menú.');
    } catch {
      this.error.set('No pudimos abrir la instalación. Tocá Instalar para ver otras opciones.');
    } finally {
      this.busy.set(false);
    }
  }

  dismiss(): void {
    this.dismissed.set(true);
  }

  closeHelp(): void {
    this.help()?.nativeElement.close();
  }

  restoreFocus(): void {
    this.installButton()?.nativeElement.focus();
  }

  private markInstalled(): void {
    this.installed.set(true);
    this.deferredPrompt.set(null);
    if (this.help()?.nativeElement.open) this.closeHelp();
  }
}
