# ADR 0011 — Dictado por voz en la PWA (Web Speech API)

- **Estado:** Aceptado (2026-10-22)
- **Contexto:** ADR-0009 (pivot PWA: la captura es texto en la SPA), regla madre
  de `business-rules.md` (nunca inventar datos; transcripción e interpretación
  son etapas separadas), spec NLU en `docs/domain/nlu-captura.md`

## S1 — La voz se transcribe EN el navegador, el pipeline no cambia

**Decisión:** el panel «Dictá tu entrenamiento» suma un botón de micrófono que
usa el **Web Speech API del navegador** (`SpeechRecognition` / `webkitSpeechRecognition`,
Chrome/Edge): lo dicho queda como **texto en el borrador del textarea**, donde el
alumno lo revisa antes de enviar. Del textarea en adelante, todo es exactamente
igual que la captura por teclado: `POST /api/capturas/{alumnoId}/texto` →
NLU (`gemini-3-flash-preview`) → `CatalogResolver` → sesión.

- El transcript **final** del reconocedor se agrega al borrador (con espacio si
  ya había texto); el alumno lo corrige a mano si el STT erró y luego envía.
- Los resultados **interinos** (parciales) solo se muestran como ayuda viva
  (`.parcial-dictado`, `aria-live="polite"`): nunca entran al borrador ni
  viajan a la API.
- Así se mantienen separadas **transcripción** (navegador) e **interpretación**
  (pipeline NLU): ninguna etapa inventa en nombre del alumno.
- No se suma una etapa de STT en backend para la PWA: `GeminiSpeechToText`
  sigue siendo código del canal WhatsApp (deshabilitado), solo para su
  eventual reactivación.
- Idioma del reconocedor: `es-AR` fijo (el bot es español rioplatense;
  `docs/domain/nlu-captura.md`). Cambiarlo sería config, no heurística.

## S2 — Sin soporte y con permiso denegado: captura posible, texto no escrito

**Decisión:** el micrófono es un agregado, nunca un requisito:

- Navegador sin soporte (Firefox, Safari parcial): el botón no se renderiza y
  aparece la ayuda del dictado alternativo (Win+H en Windows, micrófono del
  teclado en móvil). El envío por teclado sigue igual.
- Errores (`not-allowed`, `no-speech`, `audio-capture`, `network`, desconocidos)
  se mapean a mensajes en español en un banner del panel. Ningún error escribe
  texto en el borrador: si el dictado falla, no hay dato.
- El servicio `DictadoVoz` es el único punto que toca el Web Speech API: la
  fábrica del reconocedor viaja por un `InjectionToken` (`FABRICA_RECONOCEDOR`),
  lo que permite specs con falso y reemplazar el motor sin tocar la vista.

## S3 — Alcance y no-alcance

- En el MVP: un dictado por click del botón (`continuous = false`), granular
  por mensaje (mismo registro «un mensaje por serie» que la captura escrita).
- No se guarda audio, no se envía audio a la API y no hay copia del transcript
  más allá del borrador local (privacidad por diseño; el reconocedor vive en
  el navegador).
- [SUPUESTO] El alumno prueba con Chrome/Edge (desktop o Android): los demás
  navegadores quedan con la captura por teclado, sin degradar el resto del
  flujo.
