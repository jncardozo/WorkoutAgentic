# ADR 0028 — Dictado por ciclos sin concatenar revisiones

- **Estado:** Aceptado (2026-11-11).
- **Contexto:** en el celular, el borrador acumuló «sentadilla», «sentadilla
  hack», «sentadilla hack 4» y sus sucesivas revisiones. La protección por índice
  de ADR-0025 no cubre revisiones notificadas bajo índices diferentes. El texto
  observado es compatible con ese mecanismo; no disponemos del registro de los
  eventos del dispositivo para atribuir el fallo a una implementación concreta.

## Decisión

- Mantener una sesión de dictado de hasta 60 segundos, pero usar ciclos del
  reconocedor con `continuous=false`, igual en todos los navegadores. El contrato
  Web Speech devuelve como máximo una transcripción final por ciclo en este modo:
  https://webaudio.github.io/web-speech-api/#dom-speechrecognition-continuous.
- Dentro de un ciclo, conservar la última transcripción marcada como final,
  reemplazando las anteriores. No agregar cada evento al borrador. Los interinos
  solo alimentan la vista previa; nunca pasan a la API.
- Al recibir `end`, entregar el final una sola vez y reiniciar tras 250 ms si
  queda tiempo. Ignorar eventos de fin duplicados. Parada manual, error o salida
  conservan el final ya recibido y cancelan reinicios; un interino no se convierte
  en final. El mecanismo de cierre de respaldo también conserva el final.
- No eliminar palabras repetidas ni aplicar filtros por similitud/prefijo. Una
  repetición dicha por el alumno, incluso en dos ciclos consecutivos, es válida.
  La corrección del texto continúa a cargo del alumno, antes del envío al NLU.

## Consecuencias y verificación

El alumno ve cada frase en el borrador al finalizar su ciclo, normalmente después
de una pausa o al detener. Se mantiene el plazo original entre reinicios. Puede
haber una breve interrupción entre ciclos, dependiente del navegador, y debe
verificarse nuevamente en un celular físico. Se prueban revisiones crecientes con
índices nuevos, finales repetidos, interinos, fin duplicado, repeticiones reales,
reinicios, parada manual, errores, cancelación y límite de 60 segundos.

Esta decisión sustituye el uso de `continuous=true` de ADR-0025; no modifica el NLU.
