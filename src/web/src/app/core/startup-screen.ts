export interface StartupScreen {
  ready(): void;
  fail(): void;
}

/** Controls the HTML splash that is already visible before Angular bootstraps. */
export function startStartupScreen(doc: Document, browser: Window): StartupScreen {
  const screen = doc.querySelector<HTMLElement>('#startup-screen');
  const content = doc.querySelector<HTMLElement>('#app-content');
  const status = doc.querySelector<HTMLElement>('#startup-status');
  const continueButton = doc.querySelector<HTMLButtonElement>('#startup-continue');
  if (!screen || !content || !status || !continueButton) return { ready: () => undefined, fail: () => undefined };

  const reducedMotion = browser.matchMedia?.('(prefers-reduced-motion: reduce)');
  let remaining = reducedMotion?.matches ? 0 : 2200;
  let visibleSince = browser.performance.now();
  let appReady = false;
  let finished = false;
  let presentationTimer: number | undefined;
  let exitTimer: number | undefined;
  const slowTimer = browser.setTimeout(() => {
    if (!appReady && !finished) {
      status.textContent = 'La carga está tardando más de lo habitual. Podés volver a cargar la app.';
    }
  }, 12000);

  function cleanup(): void {
    browser.clearTimeout(presentationTimer);
    browser.clearTimeout(slowTimer);
    doc.removeEventListener('visibilitychange', onVisibilityChange);
    reducedMotion?.removeEventListener('change', onMotionChange);
    continueButton!.removeEventListener('click', finish);
  }

  function finish(): void {
    if (!appReady || finished) return;
    finished = true;
    cleanup();
    screen!.classList.add('startup-leaving');
    exitTimer = browser.setTimeout(() => {
      const restoreFocus = screen!.contains(doc.activeElement);
      screen!.remove();
      content!.inert = false;
      content!.removeAttribute('inert');
      content!.removeAttribute('aria-hidden');
      doc.body.classList.remove('startup-active');
      if (restoreFocus) {
        content!.setAttribute('tabindex', '-1');
        content!.focus({ preventScroll: true });
      }
    }, reducedMotion?.matches ? 0 : 240);
  }

  function scheduleExit(): void {
    browser.clearTimeout(presentationTimer);
    if (appReady && !finished && !doc.hidden) {
      const elapsed = browser.performance.now() - visibleSince;
      presentationTimer = browser.setTimeout(finish, Math.max(0, remaining - elapsed));
    }
  }

  function onVisibilityChange(): void {
    if (doc.hidden) {
      remaining = Math.max(0, remaining - (browser.performance.now() - visibleSince));
      browser.clearTimeout(presentationTimer);
    } else {
      visibleSince = browser.performance.now();
      scheduleExit();
    }
  }

  function onMotionChange(): void {
    if (reducedMotion?.matches) {
      remaining = 0;
      scheduleExit();
    }
  }

  doc.addEventListener('visibilitychange', onVisibilityChange);
  reducedMotion?.addEventListener('change', onMotionChange);
  continueButton.addEventListener('click', finish);

  return {
    ready(): void {
      if (finished || appReady) return;
      appReady = true;
      browser.clearTimeout(slowTimer);
      screen.setAttribute('aria-busy', 'false');
      screen.classList.add('startup-ready');
      status.textContent = 'Todo listo. Vamos a entrenar.';
      continueButton.hidden = false;
      scheduleExit();
    },
    fail(): void {
      if (finished) return;
      finished = true;
      cleanup();
      browser.clearTimeout(exitTimer);
      screen.setAttribute('aria-busy', 'false');
      screen.classList.add('startup-failed');
      continueButton.hidden = true;
      status.setAttribute('role', 'alert');
      status.textContent = 'No pudimos abrir la app. Revisá tu conexión y volvé a cargarla.';
    },
  };
}
