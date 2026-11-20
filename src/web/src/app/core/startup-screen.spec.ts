import { startStartupScreen, StartupScreen } from './startup-screen';

describe('Presentación de arranque', () => {
  let fixture: HTMLDivElement;
  let startup: StartupScreen;
  let hidden: boolean;
  let motion: EventTarget & { matches: boolean };

  beforeEach(() => {
    vi.useFakeTimers();
    const startedAt = Date.now();
    vi.spyOn(window.performance, 'now').mockImplementation(() => Date.now() - startedAt);
    hidden = false;
    vi.spyOn(document, 'hidden', 'get').mockImplementation(() => hidden);
    motion = Object.assign(new EventTarget(), { matches: false });
    vi.stubGlobal('matchMedia', vi.fn(() => motion));
    fixture = document.createElement('div');
    fixture.innerHTML = `
      <section id="startup-screen" aria-busy="true">
        <p id="startup-status" role="status">Preparando…</p>
        <button id="startup-continue" type="button" hidden>Empezar entrenamiento</button>
        <a href="">Volver a cargar</a>
      </section>
      <app-root id="app-content" inert aria-hidden="true"></app-root>`;
    document.body.append(fixture);
    document.body.classList.add('startup-active');
  });

  afterEach(() => {
    startup?.fail();
    fixture.remove();
    document.body.classList.remove('startup-active');
    vi.clearAllTimers();
    vi.useRealTimers();
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  const screen = () => document.querySelector<HTMLElement>('#startup-screen');
  const content = () => document.querySelector<HTMLElement>('#app-content')!;
  const button = () => document.querySelector<HTMLButtonElement>('#startup-continue')!;

  it('permite ver la animación 2,2 segundos aunque la app esté lista inmediatamente', () => {
    startup = startStartupScreen(document, window);
    startup.ready();
    vi.advanceTimersByTime(2199);
    expect(screen()).not.toBeNull();
    expect(content().hasAttribute('inert')).toBe(true);
    vi.advanceTimersByTime(241);
    expect(screen()).toBeNull();
    expect(content().hasAttribute('inert')).toBe(false);
    expect(content().hasAttribute('aria-hidden')).toBe(false);
    expect(document.body.classList.contains('startup-active')).toBe(false);
  });

  it('espera la carga real y permite continuar sin otra demora cuando ya pasó la presentación', () => {
    startup = startStartupScreen(document, window);
    vi.advanceTimersByTime(4000);
    expect(screen()).not.toBeNull();
    expect(button().hidden).toBe(true);
    startup.ready();
    vi.advanceTimersByTime(240);
    expect(screen()).toBeNull();
  });

  it('solo permite omitir la presentación cuando la app está lista y devuelve el foco', () => {
    startup = startStartupScreen(document, window);
    button().click();
    vi.advanceTimersByTime(300);
    expect(screen()).not.toBeNull();
    startup.ready();
    button().focus();
    button().click();
    vi.advanceTimersByTime(240);
    expect(screen()).toBeNull();
    expect(document.activeElement).toBe(content());
  });

  it('no cuenta como presentación el tiempo con la app en segundo plano', () => {
    startup = startStartupScreen(document, window);
    vi.advanceTimersByTime(500);
    hidden = true;
    document.dispatchEvent(new Event('visibilitychange'));
    vi.advanceTimersByTime(10000);
    startup.ready();
    expect(screen()).not.toBeNull();
    hidden = false;
    document.dispatchEvent(new Event('visibilitychange'));
    vi.advanceTimersByTime(1699);
    expect(screen()?.classList.contains('startup-leaving')).toBe(false);
    vi.advanceTimersByTime(241);
    expect(screen()).toBeNull();
  });

  it('omite el tiempo de presentación con movimiento reducido', () => {
    motion.matches = true;
    startup = startStartupScreen(document, window);
    startup.ready();
    vi.runAllTimers();
    expect(screen()).toBeNull();
  });

  it('muestra una alternativa de recarga si falla el arranque, sin exponer la app incompleta', () => {
    startup = startStartupScreen(document, window);
    startup.fail();
    startup.ready();
    vi.advanceTimersByTime(15000);
    expect(screen()?.querySelector('[role="alert"]')?.textContent).toContain('No pudimos abrir la app');
    expect(screen()?.querySelector('a')).not.toBeNull();
    expect(content().hasAttribute('inert')).toBe(true);
    expect(button().hidden).toBe(true);
  });

  it('informa una carga lenta sin inventar progreso y sigue esperando la app', () => {
    startup = startStartupScreen(document, window);
    vi.advanceTimersByTime(12000);
    expect(screen()?.textContent).toContain('más de lo habitual');
    startup.ready();
    vi.advanceTimersByTime(240);
    expect(screen()).toBeNull();
  });
});
