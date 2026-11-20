import { TestBed } from '@angular/core/testing';
import { PwaInstall } from './pwa-install';

describe('PwaInstall', () => {
  let standalone: EventTarget & { matches: boolean };

  beforeEach(() => {
    standalone = Object.assign(new EventTarget(), { matches: false });
    vi.stubGlobal('matchMedia', vi.fn(() => standalone));
    vi.spyOn(window.navigator, 'userAgent', 'get').mockReturnValue('Mozilla/5.0 (Linux; Android 14) Chrome/130 Mobile Safari/537.36');
    TestBed.configureTestingModule({ imports: [PwaInstall] });
  });

  afterEach(() => {
    TestBed.resetTestingModule();
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  function create() {
    const fixture = TestBed.createComponent(PwaInstall);
    fixture.detectChanges();
    const dialog = (fixture.nativeElement as HTMLElement).querySelector('dialog')!;
    dialog.showModal = vi.fn(() => { dialog.open = true; });
    dialog.close = vi.fn(() => { dialog.open = false; dialog.dispatchEvent(new Event('close')); });
    return fixture;
  }

  function offer(outcome: 'accepted' | 'dismissed' = 'accepted') {
    const prompt = vi.fn().mockResolvedValue(undefined);
    const event = Object.assign(new Event('beforeinstallprompt', { cancelable: true }), {
      prompt, userChoice: Promise.resolve({ outcome }),
    });
    window.dispatchEvent(event);
    return { event, prompt };
  }

  it('espera la disponibilidad real del navegador y abre la instalación solo al tocar el botón', async () => {
    const fixture = create();
    const root = fixture.nativeElement as HTMLElement;
    const button = root.querySelector<HTMLButtonElement>('.install-trigger')!;
    expect(button).not.toBeNull();
    expect(button.disabled).toBe(false);
    button.click();
    expect(root.querySelector('dialog')!.open).toBe(true);
    fixture.componentInstance.closeHelp();
    expect(fixture.componentInstance.busy()).toBe(false);
    const { event, prompt } = offer();
    fixture.detectChanges();
    expect(event.defaultPrevented).toBe(true);
    expect(prompt).not.toHaveBeenCalled();
    expect(button.disabled).toBe(false);
    root.querySelector<HTMLButtonElement>('.install-trigger')!.click();
    expect(prompt).toHaveBeenCalledOnce();
    await fixture.whenStable();
    expect(root.querySelector('.install-hint')).toBeNull();
  });

  it('no reutiliza un evento cancelado y permite otra oferta del navegador', async () => {
    const fixture = create();
    const { prompt } = offer('dismissed');
    await fixture.componentInstance.install();
    await fixture.componentInstance.install();
    expect(prompt).toHaveBeenCalledOnce();
    expect(fixture.componentInstance.visible()).toBe(true);
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('[role="status"]')?.textContent).toContain('Cancelaste');
    expect((fixture.nativeElement as HTMLElement).querySelector('dialog')!.open).toBe(true);
    offer();
    expect(fixture.componentInstance.visible()).toBe(true);
    fixture.componentInstance.closeHelp();
    await fixture.componentInstance.install();
    expect(fixture.componentInstance.visible()).toBe(false);
  });

  it('bloquea dobles toques mientras el diálogo nativo está abierto', async () => {
    const fixture = create();
    let choose!: (result: { outcome: 'dismissed' }) => void;
    const prompt = vi.fn().mockResolvedValue(undefined);
    window.dispatchEvent(Object.assign(new Event('beforeinstallprompt'), {
      prompt, userChoice: new Promise(resolve => { choose = resolve; }),
    }));
    const first = fixture.componentInstance.install();
    await fixture.componentInstance.install();
    expect(prompt).toHaveBeenCalledOnce();
    expect(fixture.componentInstance.busy()).toBe(true);
    choose({ outcome: 'dismissed' });
    await first;
    expect(fixture.componentInstance.busy()).toBe(false);
  });

  it('informa errores sin fingir una instalación y acepta una nueva oferta', async () => {
    const fixture = create();
    offer().prompt.mockRejectedValue(new Error('No disponible'));
    await fixture.componentInstance.install();
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('[role="alert"]')?.textContent).toContain('No pudimos');
    await fixture.componentInstance.install();
    expect((fixture.nativeElement as HTMLElement).querySelector('dialog')!.open).toBe(true);
    offer();
    expect(fixture.componentInstance.error()).toBe('');
  });

  it('permite ocultar el globo durante la navegación actual', () => {
    const fixture = create();
    offer();
    fixture.detectChanges();
    (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('.dismiss-hint')!.click();
    offer();
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('.install-hint')).toBeNull();
  });

  it('oculta la promoción al instalar desde el navegador o cambiar a standalone', () => {
    const fixture = create();
    offer();
    window.dispatchEvent(new Event('appinstalled'));
    expect(fixture.componentInstance.visible()).toBe(false);
    fixture.destroy();
    const other = create();
    offer();
    standalone.matches = true;
    standalone.dispatchEvent(new Event('change'));
    expect(other.componentInstance.visible()).toBe(false);
  });

  it('no ofrece instalar al abrir la PWA instalada', () => {
    standalone.matches = true;
    const fixture = create();
    offer();
    expect(fixture.componentInstance.visible()).toBe(false);
  });

  it('en Safari de iPhone abre una guía modal y devuelve el foco al cerrarla', async () => {
    vi.spyOn(window.navigator, 'userAgent', 'get').mockReturnValue('Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) Version/18.0 Mobile Safari/604.1');
    const fixture = create();
    const root = fixture.nativeElement as HTMLElement;
    const dialog = root.querySelector('dialog')!;
    const show = vi.fn(() => { dialog.open = true; });
    dialog.showModal = show;
    dialog.close = vi.fn(() => { dialog.open = false; dialog.dispatchEvent(new Event('close')); });
    const focus = vi.spyOn(root.querySelector<HTMLButtonElement>('.install-trigger')!, 'focus');
    await fixture.componentInstance.install();
    expect(show).toHaveBeenCalledOnce();
    expect(dialog.textContent).toContain('Agregar a pantalla de inicio');
    dialog.querySelector('button')!.click();
    expect(dialog.open).toBe(false);
    expect(focus).toHaveBeenCalled();
  });

  it('respeta standalone de iOS aunque la media query no lo informe', () => {
    Object.defineProperty(window.navigator, 'standalone', { configurable: true, value: true });
    try {
      const fixture = create();
      offer();
      expect(fixture.componentInstance.visible()).toBe(false);
    } finally {
      Reflect.deleteProperty(window.navigator, 'standalone');
    }
  });

  it('en Chrome de iOS ofrece abrir Safari y excluye escritorio', async () => {
    vi.spyOn(window.navigator, 'userAgent', 'get').mockReturnValue('iPhone CriOS/130 Mobile Safari/604.1');
    const fixture = create();
    expect(fixture.componentInstance.visible()).toBe(true);
    await fixture.componentInstance.install();
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('dialog')!.textContent).toContain('Safari');
    fixture.destroy();
    vi.spyOn(window.navigator, 'userAgent', 'get').mockReturnValue('Mozilla/5.0 Windows NT Chrome/130 Safari/537.36');
    const desktop = create();
    const { event } = offer();
    expect(event.defaultPrevented).toBe(false);
    expect(desktop.componentInstance.visible()).toBe(false);
  });

  it.each([
    'Mozilla/5.0 (Linux; Android 14) SamsungBrowser/25.0 Chrome/121.0 Mobile Safari/537.36',
    'Mozilla/5.0 (Linux; Android 14; wv) Version/4.0 Chrome/130.0 Mobile Safari/537.36',
    'Mozilla/5.0 (Linux; Android 14) Firefox/130',
  ])('muestra el globo sin depender de Chrome ni de su evento: %s', async userAgent => {
    vi.spyOn(window.navigator, 'userAgent', 'get').mockReturnValue(userAgent);
    const fixture = create();
    const root = fixture.nativeElement as HTMLElement;
    const trigger = root.querySelector<HTMLButtonElement>('.install-trigger')!;
    expect(trigger.textContent).toContain('Instalar');
    expect(trigger.disabled).toBe(false);
    trigger.click();
    await fixture.whenStable();
    expect(root.querySelector('dialog')!.open).toBe(true);
    expect(root.querySelector('dialog')!.textContent).toContain('tres puntos');
  });

  it('reconoce iPad con identificación de escritorio', async () => {
    vi.spyOn(window.navigator, 'userAgent', 'get').mockReturnValue('Mozilla/5.0 (Macintosh) Version/18.0 Safari/605.1.15');
    vi.spyOn(window.navigator, 'platform', 'get').mockReturnValue('MacIntel');
    const original = Object.getOwnPropertyDescriptor(window.navigator, 'maxTouchPoints');
    Object.defineProperty(window.navigator, 'maxTouchPoints', { configurable: true, value: 5 });
    try {
      const fixture = create();
      expect(fixture.componentInstance.platform()).toBe('ios');
      await fixture.componentInstance.install();
      expect((fixture.nativeElement as HTMLElement).querySelector('dialog')!.textContent).toContain('Compartir');
    } finally {
      if (original) Object.defineProperty(window.navigator, 'maxTouchPoints', original);
      else Reflect.deleteProperty(window.navigator, 'maxTouchPoints');
    }
  });

  it('ofrece ayuda genérica en pantalla táctil pequeña con identificación reducida', () => {
    vi.spyOn(window.navigator, 'userAgent', 'get').mockReturnValue('Mozilla/5.0');
    vi.stubGlobal('matchMedia', vi.fn((query: string) =>
      Object.assign(new EventTarget(), { matches: query === '(pointer: coarse)' || query === '(max-width: 900px)' })));
    const fixture = create();
    expect(fixture.componentInstance.platform()).toBe('mobile');
    expect(fixture.componentInstance.visible()).toBe(true);
  });

  it('retira los listeners al destruir el componente', () => {
    const fixture = create();
    fixture.destroy();
    expect(offer().event.defaultPrevented).toBe(false);
  });
});
