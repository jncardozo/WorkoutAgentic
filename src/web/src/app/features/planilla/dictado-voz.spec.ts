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
    expect(finales).toEqual(['10 con 40']);

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

  it('si el start no se puede, avisa sin marcar escuchando', () => {
    const { falso } = reconocedorFalso({ fallaStart: true });
    const dictado = montarCon(() => falso);

    dictado.escuchar(vi.fn());

    expect(dictado.escuchando()).toBe(false);
    expect(dictado.fallo()).toContain('No se pudo abrir el micrófono');
  });
});
