import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';

import {
  DictadoVoz,
  EventoReconocimientos,
  FabricaReconocedor,
  FABRICA_RECONOCEDOR,
  ReconocedorDictado,
} from './dictado-voz';

/** Reconocedor falso: registra qué se le pidió y deja dispararle eventos. */
function reconocedorFalso(opciones: { fallaStart?: boolean } = {}): {
  falso: ReconocedorDictado;
  llamadas: string[];
} {
  const llamadas: string[] = [];
  const falso: ReconocedorDictado = {
    lang: '',
    continuous: false,
    interimResults: false,
    start: () => {
      if (opciones.fallaStart) {
        throw new Error('ya arrancado');
      }
      llamadas.push('start');
    },
    stop: () => llamadas.push('stop'),
    abort: () => llamadas.push('abort'),
    onresult: null,
    onerror: null,
    onend: null,
  };
  return { falso, llamadas };
}

/** Evento `onresult` con los trozos [texto, isFinal] en orden de llegada. */
function evento(textos: [string, boolean][]): EventoReconocimientos {
  const results = { length: textos.length } as EventoReconocimientos['results'];
  textos.forEach(([texto, isFinal], posicion) => {
    results[posicion] = { isFinal, 0: { transcript: texto } };
  });
  return { resultIndex: 0, results };
}

describe('DictadoVoz', () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => { TestBed.resetTestingModule(); vi.useRealTimers(); });
  function montarCon(fabrica: FabricaReconocedor): DictadoVoz {
    TestBed.configureTestingModule({
      providers: [{ provide: FABRICA_RECONOCEDOR, useValue: fabrica }],
    });
    return TestBed.inject(DictadoVoz);
  }

  it('sin reconocedor en el navegador queda no soportado y escuchar no arranca', () => {
    const dictado = montarCon(() => null);

    expect(dictado.soportado).toBe(false);
    dictado.escuchar(vi.fn());
    expect(dictado.escuchando()).toBe(false);
  });

  it('dictar marca escuchando y entrega el transcript final limpio a la vista', () => {
    const { falso } = reconocedorFalso();
    const dictado = montarCon(() => falso);
    const finales: string[] = [];
    dictado.escuchar((texto) => finales.push(texto));

    expect(dictado.escuchando()).toBe(true);

    falso.onresult?.(evento([['prensa', false]]));
    expect(dictado.parcial()).toBe('prensa');
    falso.onresult?.(evento([['10 con 40', true]]));
    expect(dictado.parcial()).toBe('');
    expect(finales).toEqual([]);
    falso.onend?.();
    expect(finales).toEqual(['10 con 40']);

    dictado.detener();
    falso.onend?.();
    expect(dictado.escuchando()).toBe(false);
  });

  it('los avisos del micrófono hablan en español y no producen texto', () => {
    const { falso } = reconocedorFalso();
    const dictado = montarCon(() => falso);
    const finales: string[] = [];
    dictado.escuchar((texto) => finales.push(texto));

    falso.onerror?.({ error: 'not-allowed' });
    expect(dictado.fallo()).toContain('micrófono está bloqueado');
    expect(finales).toEqual([]);

    falso.onerror?.({ error: 'algo-raro' });
    expect(dictado.fallo()).toBe('El dictado falló (algo-raro); probá otra vez.');
  });

  it('detener pide stop al reconocedor y el fin apaga el estado', () => {
    const { falso, llamadas } = reconocedorFalso();
    const dictado = montarCon(() => falso);
    dictado.escuchar(vi.fn());

    dictado.detener();
    expect(llamadas).toEqual(['start', 'stop']);

    falso.onend?.();
    expect(dictado.escuchando()).toBe(false);
  });

  it('escuchar de nuevo mientras ya escucha no relanza el reconocedor', () => {
    const { falso, llamadas } = reconocedorFalso();
    const dictado = montarCon(() => falso);
    dictado.escuchar(vi.fn());
    dictado.escuchar(vi.fn());

    expect(llamadas).toEqual(['start']);
  });

  it('mantiene escucha por ciclos durante 60 segundos y acepta el último resultado al detener', () => {
    const { falso, llamadas } = reconocedorFalso();
    const dictado = montarCon(() => falso);
    const finals = vi.fn();
    dictado.escuchar(finals);
    expect(falso.continuous).toBe(false);
    falso.onresult?.(evento([['press de banca 4 series', true]]));
    falso.onend?.();
    vi.advanceTimersByTime(250);
    vi.advanceTimersByTime(58_750);
    expect(llamadas).toEqual(['start', 'start']);
    expect(dictado.remainingSeconds()).toBe(1);
    vi.advanceTimersByTime(1000);
    expect(llamadas).toEqual(['start', 'start', 'stop']);
    falso.onresult?.(evento([['con 10 repeticiones', true]]));
    falso.onend?.();
    expect(finals.mock.calls).toEqual([['press de banca 4 series'], ['con 10 repeticiones']]);
    expect(dictado.escuchando()).toBe(false);
    vi.advanceTimersByTime(60_000);
    expect(llamadas).toEqual(['start', 'start', 'stop']);
  });

  it('reinicia después del silencio sin extender el plazo original', () => {
    const { falso, llamadas } = reconocedorFalso();
    const dictado = montarCon(() => falso);
    dictado.escuchar(vi.fn());
    vi.advanceTimersByTime(20_000);
    falso.onerror?.({ error: 'no-speech' });
    falso.onend?.();
    vi.advanceTimersByTime(250);
    expect(llamadas).toEqual(['start', 'start']);
    expect(dictado.fallo()).toBeNull();
    vi.advanceTimersByTime(39_750);
    expect(llamadas).toEqual(['start', 'start', 'stop']);
  });

  it('confirma una sola revisión final y deja los interinos fuera del borrador', () => {
    const { falso } = reconocedorFalso();
    const dictado = montarCon(() => falso);
    const finals = vi.fn();
    dictado.escuchar(finals);
    falso.onresult?.(evento([['primera serie', false]]));
    falso.onresult?.(evento([['primera serie con 40', false]]));
    expect(finals).not.toHaveBeenCalled();
    expect(dictado.parcial()).toBe('primera serie con 40');
    falso.onresult?.(evento([['primera serie con 40', true]]));
    falso.onresult?.(evento([['primera serie con 40', true]]));
    falso.onend?.(); falso.onend?.();
    expect(finals.mock.calls).toEqual([['primera serie con 40']]);
  });

  it('no concatena las hipótesis crecientes reportadas por el celular', () => {
    const { falso } = reconocedorFalso();
    const dictado = montarCon(() => falso);
    const finals = vi.fn();
    dictado.escuchar(finals);
    const snapshots = ['sentadilla', 'sentadilla hack', 'sentadilla hack', 'sentadilla hack 4', 'sentadilla hack 4 series'];
    snapshots.forEach((text, index) => {
      const event = evento(snapshots.slice(0, index + 1).map(value => [value, true]));
      event.resultIndex = index;
      falso.onresult?.(event);
    });
    expect(finals).not.toHaveBeenCalled();
    falso.onend?.();
    expect(finals.mock.calls).toEqual([['sentadilla hack 4 series']]);
  });

  it('conserva repeticiones intencionales y frases iguales en ciclos distintos', () => {
    const { falso } = reconocedorFalso();
    const dictado = montarCon(() => falso);
    const finals = vi.fn();
    dictado.escuchar(finals);
    falso.onresult?.(evento([['no no sentí dolor', true]])); falso.onend?.();
    vi.advanceTimersByTime(250);
    falso.onresult?.(evento([['no no sentí dolor', true]])); falso.onend?.();
    expect(finals.mock.calls).toEqual([['no no sentí dolor'], ['no no sentí dolor']]);
  });

  it('acumula las frases de sentadilla hack sin sus versiones intermedias', () => {
    const { falso } = reconocedorFalso();
    const dictado = montarCon(() => falso);
    let draft = 'Texto escrito antes';
    dictado.escuchar(text => { draft += ` ${text}`; });
    const phrases = ['sentadilla hack 4 series',
      'primera serie y segunda serie aproximaciones con 40 kg',
      'serie efectiva con 80 kg a 12 repeticiones',
      'última serie efectiva con 180 kg 6 repeticiones'];
    for (const phrase of phrases) {
      const words = phrase.split(' ');
      const revisions = words.map((_, index) => words.slice(0, index + 1).join(' '));
      revisions.forEach((_, index) => {
        const result = evento(revisions.slice(0, index + 1).map(text => [text, true]));
        result.resultIndex = index;
        falso.onresult?.(result);
      });
      falso.onend?.();
      vi.advanceTimersByTime(250);
    }
    expect(draft).toBe(`Texto escrito antes ${phrases.join(' ')}`);
  });

  it('al detener sin evento end conserva el final y descarta el interino', () => {
    const { falso } = reconocedorFalso();
    const dictado = montarCon(() => falso);
    const finals = vi.fn();
    dictado.escuchar(finals);
    falso.onresult?.(evento([['primera serie con 40', true], ['segunda', false]]));
    dictado.detener();
    vi.advanceTimersByTime(1500);
    expect(finals.mock.calls).toEqual([['primera serie con 40']]);
    expect(dictado.escuchando()).toBe(false);
  });

  it('la parada manual entre reinicios y los errores cancelan la recuperación', () => {
    const { falso, llamadas } = reconocedorFalso();
    const dictado = montarCon(() => falso);
    dictado.escuchar(vi.fn());
    falso.onend?.();
    dictado.detener();
    vi.advanceTimersByTime(1000);
    expect(llamadas).toEqual(['start']);
    dictado.escuchar(vi.fn());
    falso.onerror?.({ error: 'network' });
    falso.onend?.();
    vi.advanceTimersByTime(60_000);
    expect(llamadas).toEqual(['start', 'start', 'abort']);
    expect(dictado.escuchando()).toBe(false);
  });

  it('cancelar libera el micrófono e ignora resultados tardíos', () => {
    const { falso, llamadas } = reconocedorFalso();
    const dictado = montarCon(() => falso);
    const finals = vi.fn();
    dictado.escuchar(finals);
    dictado.cancelar();
    falso.onresult?.(evento([['dato tardío', true]]));
    falso.onend?.();
    vi.advanceTimersByTime(60_000);
    expect(llamadas).toEqual(['start', 'abort']);
    expect(finals).not.toHaveBeenCalled();
  });

  it('si el start no se puede, avisa sin marcar escuchando', () => {
    const { falso } = reconocedorFalso({ fallaStart: true });
    const dictado = montarCon(() => falso);

    dictado.escuchar(vi.fn());

    expect(dictado.escuchando()).toBe(false);
    expect(dictado.fallo()).toContain('No se pudo abrir el micrófono');
  });
});
