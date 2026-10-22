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
  /** El pipeline NLU es español rioplatense (docs/domain/nlu-captura.md). */
  private static readonly IDIOMA = 'es-AR';

  private readonly fabrica = inject(FABRICA_RECONOCEDOR);
  private readonly reconocedor: ReconocedorDictado | null = this.fabrica();
  private readonly destructor = inject(DestroyRef);
  private alTextoFinal: ((texto: string) => void) | null = null;

  /** ¿El navegador puede reconocer voz? Se resuelve una vez, al inyectar. */
  readonly soportado: boolean;
  readonly escuchando = signal(false);
  /** Lo dicho pero todavía no confirmado por el reconocedor: solo se muestra. */
  readonly parcial = signal('');
  readonly fallo = signal<string | null>(null);

  constructor() {
    this.soportado = this.reconocedor !== null;
    if (this.reconocedor === null) {
      return;
    }

    const reconocedor = this.reconocedor;
    reconocedor.lang = DictadoVoz.IDIOMA;
    reconocedor.interimResults = true;
    reconocedor.continuous = false; // un dictado por click; lo dicho queda revisable en el borrador

    reconocedor.onresult = (evento) => this.procesar(evento);
    reconocedor.onerror = (evento) => this.procesarError(evento);
    reconocedor.onend = () => this.escuchando.set(false);

    this.destructor.onDestroy(() => this.reconocedor?.abort());
  }

  /** Empieza a escuchar; el transcript final se entrega por `alTextoFinal`. */
  escuchar(alTextoFinal: (texto: string) => void): void {
    if (this.reconocedor === null || this.escuchando()) {
      return;
    }

    this.fallo.set(null);
    this.parcial.set('');
    this.alTextoFinal = alTextoFinal;
    try {
      this.reconocedor.start();
      this.escuchando.set(true);
    } catch {
      this.fallo.set('No se pudo abrir el micrófono; volvé a intentar.');
    }
  }

  /** Para de escuchar: al stop() el reconocedor entrega el último trozo final. */
  detener(): void {
    if (this.escuchando()) {
      this.reconocedor?.stop();
    }
  }

  private procesar(evento: EventoReconocimientos): void {
    let final = '';
    let parcial = '';

    for (let posicion = evento.resultIndex; posicion < evento.results.length; posicion++) {
      const trozo = evento.results[posicion];
      const texto = trozo?.[0]?.transcript ?? '';
      if (trozo?.isFinal) {
        final += `${texto} `;
      } else {
        parcial += texto;
      }
    }

    if (parcial.length > 0) {
      this.parcial.set(parcial);
    }

    const finalLimpio = final.trim();
    if (finalLimpio.length > 0) {
      this.parcial.set('');
      this.alTextoFinal?.(finalLimpio);
    }
  }

  private procesarError(evento: EventoErrorDictado): void {
    const mensajes: Record<string, string> = {
      'not-allowed': 'El micrófono está bloqueado: habilitá el permiso en el navegador y volvé a dictar.',
      'service-not-allowed': 'El navegador no dejó usar el micrófono en este sitio.',
      'no-speech': 'No escuché nada: apretá el micrófono y hablá.',
      'audio-capture': 'No encontramos un micrófono disponible.',
      network: 'Fallo de red del reconocedor: probá otra vez.',
    };
    this.fallo.set(mensajes[evento.error] ?? `El dictado falló (${evento.error}); probá otra vez.`);
  }
}
