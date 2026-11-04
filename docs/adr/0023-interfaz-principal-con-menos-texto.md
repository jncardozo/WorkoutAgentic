# ADR 0023 — Interfaz principal con menos texto

- **Estado:** Aceptado (2026-10-27)
- **Contexto:** la pantalla principal acumulaba instrucciones repetidas en la
  captura, la sesión y la confirmación, lo que dificultaba escanear la planilla.

## Decisión

- Usar una instrucción breve junto al campo de captura y un ejemplo conciso en
  el placeholder.
- Quitar los mensajes de estado que duplican el badge de la sesión y acortar
  los estados vacíos, la alerta de datos faltantes y la confirmación.
- Conservar los mensajes de aclaración y error cuando la persona debe actuar.
- No agregar menciones a WhatsApp ni afirmaciones sobre integraciones que ya no
  forman parte del runtime (ADR-0018).

## Consecuencias

- La pantalla presenta primero la captura y la cuadrícula, con menos texto
  permanente y mensajes más breves para los casos que requieren atención.
