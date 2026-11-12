# ADR 0034 — Ayuda con un comando completo

- **Estado:** Aceptado (2026-11-11), propuesta aprobada por el usuario.

## Contexto

Los ejemplos rápidos de frases parciales quedaron obsoletos y no enseñaban a
dictar un ejercicio con todas sus series y cargas.

## Decisión

- Se reemplazan las tres tarjetas por una única card «💡 Comando de ejemplo»,
  con el estilo de los ejemplos rápidos y un área táctil de al menos 44 px.
- La card abre un diálogo modal nativo, sin escribir en el borrador ni enviar
  texto a la API. Conserva el bloqueo durante dictado o envío de captura.
- Se muestra el ejemplo de Sillón de cuádriceps con cuatro series: aproximación
  de 12 reps/40 kg, aproximación de 10 reps/50 kg, efectiva de 12 reps/70 kg y
  efectiva de 12 reps/75 kg. Cada serie ocupa un párrafo para facilitar la lectura.
- El aviso explica que se deben completar todas las series antes de pasar al
  siguiente ejercicio y que el progreso llega al 100% al completar sus datos.
  Es contenido de ayuda; no modifica las reglas de completitud ni confirma datos.
- Se cierra mediante la X, un toque/clic fuera del área del diálogo o Escape.
  Tocar el contenido no lo cierra. El diálogo nativo mantiene el foco dentro del
  modal y lo devuelve a la card al cerrar. El fondo queda inerte.
- Texto de 16 px, ancho ajustado al móvil y scroll vertical interno cuando no
  alcanza la altura. El fondo de la página no se desplaza mientras está abierto.

## Consecuencias

Solo cambia la ayuda de captura en la SPA. No hay nuevas rutas, endpoints,
datos persistidos ni cambios en NLU. No se concede acceso al micrófono al abrirla.
