# ADR 0030 — Indicador de carga al enviar una captura

- **Estado:** Aceptado (2026-11-11), solicitado por el usuario.

## Decisión

Al enviar texto dictado o escrito, la SPA usa `capturaEnCurso` para mostrar un
spinner y «Cargando…» en el botón, junto con «Cargando tus series en la
cuadrícula…» anunciado como estado accesible. El indicador es indeterminado:
no simula porcentajes ni promete filas antes de recibir la respuesta.

Mientras espera, el botón permanece deshabilitado, la entrada es de solo lectura
y se oculta la respuesta anterior del bot. El micrófono y ejemplos siguen
deshabilitados. La preferencia de movimiento reducido desactiva el giro.

Al responder la API, se actualiza la cuadrícula y se retira el indicador. Si
falla, también se retira, se muestra el error y se conserva el texto para reintentar.
No cambia el contrato ni las reglas de interpretación o persistencia.
