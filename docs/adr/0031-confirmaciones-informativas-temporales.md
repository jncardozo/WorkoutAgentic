# ADR 0031 — Confirmaciones informativas temporales

- **Estado:** Aceptado (2026-11-11), solicitado por el usuario.

## Decisión

Las respuestas informativas de captura se ocultan a los 8 segundos de recibirse
o al tocar/hacer clic sobre el mensaje. Se presentan como un botón de cierre
dentro de un estado accesible y también se descartan con Enter/Espacio.

No se descartan automáticamente preguntas (signos ¿ o ?) ni mensajes asociados
a una aclaración viva de la sesión. Esta distinción es únicamente de presentación;
no clasifica intención ni modifica datos de entrenamiento. Los errores HTTP y las
alertas de datos faltantes conservan sus flujos existentes (ADR-0023).

Cada mensaje nuevo reinicia el plazo. Una nueva captura, cambio de alumno,
nuevo entrenamiento o salida de la pantalla limpian el mensaje y su temporizador.
Ocultarlo no cambia la grilla. No hay una copia adicional ni historial de avisos.
