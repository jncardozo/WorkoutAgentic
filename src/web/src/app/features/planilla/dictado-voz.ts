import { DestroyRef, Injectable, InjectionToken, inject, signal } from '@angular/core';

/** Trozo reconocido por el navegador: un resultado con alternativas indexadas. */
export interface ReconocimientoDictado {
  isFinal: boolean;
  [alternativa: number]: { transcript: string } | undefined;
}

/** Lista de resultados del evento `result` del reconocedor. */
export interface ListaReconocimientos {
  length: number;
  [posicion: number]: ReconocimientoDictado;
}

/** Shape mínimo del evento `onresult` del Web Speech API. */
export interface EventoReconocimientos {
  resultIndex: number;
  results: ListaReconocimientos;
}

/** Shape mínimo del evento `onerror` del Web Speech API. */
export interface EventoErrorDictado {
  error: string;
}

/**
 * Sección mínima del Web Speech API que usa el dictado: los tipos oficiales no
 * viven en lib.dom (el estándar se implementa con prefijo webkit en
 * Chrome/Edge), así que los modelamos de forma estructural acá.
 */
export interface ReconocedorDictado {
  lang: string;
  continuous: boolean;
  interimResults: boolean;
  start(): void;
  stop(): void;
  abort(): void;
  onresult: ((evento: EventoReconocimientos) => void) | null;
  onerror: ((evento: EventoErrorDictado) => void) | null;
  onend: (() => void) | null;
}

type ConstructorReconocedor = new () => ReconocedorDictado;

declare global {
  interface Window {
    SpeechRecognition?: ConstructorReconocedor;
    webkitSpeechRecognition?: ConstructorReconocedor;
  }
}

/** Fabrica del reconocedor: `null` = el navegador no soporta dictado por voz. */
export type FabricaReconocedor = () => ReconocedorDictado | null;

export const FABRICA_RECONOCEDOR = new InjectionToken<FabricaReconocedor>('fabricaReconocedor');

/** Fábrica real del navegador: documento los dos nombres del estándar. */
export function fabricaDelNavegador(): ReconocedorDictado | null {
  const Ctor = window.SpeechRecognition ?? window.webkitSpeechRecognition;
  return Ctor ? new Ctor() : null;
}

/**
 * Dictado por voz en la PWA (ADR-0011): el reconocedor del navegador transcribe
 * lo hablado y el transcript FINAL cae en el borrador de captura, donde el
 * alumno lo revisa antes de enviar. Así la transcripción y la interpretación
 * siguen siendo etapas separadas (regla madre): los intermedios solo se muestran,
 * y si el micrófono falla avisa, sin meter texto inventado.
 */
@Injectable({ providedIn: 'root' })
export class DictadoVoz {
  private static readonly MAX_DURATION_MS = 60_000;
  private readonly reconocedor = inject(FABRICA_RECONOCEDOR)();
  private readonly destructor = inject(DestroyRef);
  private alTextoFinal: ((texto: string) => void) | null = null;
  private continueListening = false;
  private running = false;
  private deadline = 0;
  private cycleFinal = '';
  private acceptingCycle = false;
  private limitTimer: ReturnType<typeof setTimeout> | null = null;
  private restartTimer: ReturnType<typeof setTimeout> | null = null;
  private finishTimer: ReturnType<typeof setTimeout> | null = null;
  private countdownTimer: ReturnType<typeof setInterval> | null = null;

  readonly soportado = this.reconocedor !== null;
  readonly escuchando = signal(false);
  readonly remainingSeconds = signal(60);
  readonly parcial = signal('');
  readonly fallo = signal<string | null>(null);

  constructor() {
    const recognizer = this.reconocedor;
    if (!recognizer) return;
    recognizer.lang = 'es-AR';
    recognizer.interimResults = true;
    // One final utterance per cycle, restarted inside the original 60 s window.
    recognizer.continuous = false;
    recognizer.onresult = (event) => this.procesar(event);
    recognizer.onerror = (event) => this.procesarError(event);
    recognizer.onend = () => this.handleEnd();
    const onVisibility = () => { if (document.hidden) this.cancelar(); };
    document.addEventListener('visibilitychange', onVisibility);
    this.destructor.onDestroy(() => {
      this.cancelar();
      recognizer.onresult = recognizer.onerror = recognizer.onend = null;
      document.removeEventListener('visibilitychange', onVisibility);
    });
  }

  escuchar(alTextoFinal: (texto: string) => void): void {
    if (!this.reconocedor || this.escuchando()) return;
    this.fallo.set(null);
    this.parcial.set('');
    this.remainingSeconds.set(60);
    this.alTextoFinal = alTextoFinal;
    this.continueListening = true;
    this.deadline = Date.now() + DictadoVoz.MAX_DURATION_MS;
    this.escuchando.set(true);
    this.limitTimer = setTimeout(() => this.detener(), DictadoVoz.MAX_DURATION_MS);
    this.countdownTimer = setInterval(() => {
      this.remainingSeconds.set(Math.max(0, Math.ceil((this.deadline - Date.now()) / 1000)));
    }, 250);
    this.startRecognition();
  }

  private startRecognition(): void {
    if (!this.continueListening || Date.now() >= this.deadline) {
      this.finish();
      return;
    }
    this.cycleFinal = '';
    this.acceptingCycle = true;
    try {
      this.running = true;
      this.reconocedor?.start();
    } catch {
      this.fallo.set('No se pudo abrir el micrófono; volvé a intentar.');
      this.cancelar();
    }
  }

  /** Conserva el último resultado final de stop; Enviar espera al evento end. */
  detener(): void {
    if (!this.escuchando() || !this.continueListening) return;
    this.continueListening = false;
    this.clearTimers();
    this.remainingSeconds.set(0);
    if (!this.running) {
      this.finish();
      return;
    }
    this.finishTimer = setTimeout(() => this.cancelar(), 1500);
    try {
      this.reconocedor?.stop();
    } catch {
      this.cancelar();
    }
  }

  /** Salida de pantalla: no deja callbacks ni micrófono activos. */
  cancelar(): void {
    const wasRunning = this.running;
    this.finish();
    if (wasRunning) this.reconocedor?.abort();
  }

  private handleEnd(): void {
    if (!this.acceptingCycle) return;
    this.acceptingCycle = false;
    this.commitFinal();
    this.running = false;
    this.parcial.set('');
    if (this.continueListening && Date.now() < this.deadline) {
      if (this.restartTimer !== null) clearTimeout(this.restartTimer);
      this.restartTimer = setTimeout(() => {
        this.restartTimer = null;
        this.startRecognition();
      }, 250);
    } else {
      this.finish();
    }
  }

  private clearTimers(): void {
    if (this.limitTimer !== null) clearTimeout(this.limitTimer);
    if (this.restartTimer !== null) clearTimeout(this.restartTimer);
    if (this.finishTimer !== null) clearTimeout(this.finishTimer);
    if (this.countdownTimer !== null) clearInterval(this.countdownTimer);
    this.limitTimer = this.restartTimer = this.finishTimer = this.countdownTimer = null;
  }

  private commitFinal(): void {
    const text = this.cycleFinal;
    this.cycleFinal = '';
    if (text) this.alTextoFinal?.(text);
  }

  private finish(): void {
    this.acceptingCycle = false;
    this.commitFinal();
    this.continueListening = false;
    this.running = false;
    this.clearTimers();
    this.escuchando.set(false);
    this.parcial.set('');
    this.alTextoFinal = null;
  }

  private procesar(evento: EventoReconocimientos): void {
    if (!this.escuchando() || !this.running) return;
    const interim: string[] = [];
    // Revisions are not deltas. Non-continuous recognition produces one final
    // utterance per start. Keep its latest snapshot until end, even if a mobile
    // engine re-emits it under a different result index.
    for (let index = evento.resultIndex; index < evento.results.length; index++) {
      const result = evento.results[index];
      const text = result?.[0]?.transcript?.trim() ?? '';
      if (result?.isFinal) {
        if (text) this.cycleFinal = text;
      } else if (text) {
        interim.push(text);
      }
    }
    this.parcial.set(interim.join(' '));
  }

  private procesarError(evento: EventoErrorDictado): void {
    // Silence is recoverable only inside the original one-minute window.
    if (evento.error === 'no-speech' && this.continueListening && Date.now() < this.deadline) return;
    if (evento.error === 'aborted' && !this.continueListening) return;
    const mensajes: Record<string, string> = {
      'not-allowed': 'El micrófono está bloqueado: habilitá el permiso en el navegador y volvé a dictar.',
      'service-not-allowed': 'El navegador no dejó usar el micrófono en este sitio.',
      'no-speech': 'No escuché nada: apretá el micrófono y hablá.',
      'audio-capture': 'No encontramos un micrófono disponible.',
      network: 'Fallo de red del reconocedor: probá otra vez.',
    };
    this.fallo.set(mensajes[evento.error] ?? `El dictado falló (${evento.error}); probá otra vez.`);
    this.cancelar();
  }
}
