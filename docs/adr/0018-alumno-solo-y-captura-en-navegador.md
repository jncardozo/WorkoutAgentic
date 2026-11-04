# ADR 0018 — Sprint de alumno: captura desde navegador y sin módulo de profesor

- **Estado:** Aceptado (2026-10-24)
- **Contexto:** el reconocimiento de voz ya se ejecuta en el navegador y la SPA
  entrega texto a la API. El webhook, el envío y la descarga de audio de
  WhatsApp ya no tienen un flujo de producto. En este sprint se trabajará solo
  en la experiencia del alumno.

## Decisión

- El backend recibe texto reconocido o escrito. Se eliminan el webhook, las
  colas y workers de WhatsApp, Graph API, envío de mensajes, descarga de medios,
  STT del servidor y la persistencia de transcripciones de audio.
- La SPA usa el reconocimiento de voz del navegador y conserva el texto final
  en el borrador para que el alumno lo revise antes de enviarlo.
- Se eliminan las rutas y los servicios de API del módulo de profesor, la
  pantalla `/profesor` y su cliente Angular. La SPA y las APIs se enfocan en el
  alumno; sigue vigente el alta explícita de cuentas de alumno.
- No se agrega autenticación ni asignación profesor-alumno en este sprint.
- Se preservan las migraciones históricas y las tablas/columnas que ya puedan
  existir en las bases de datos. El runtime deja de mapearlas o consultarlas;
  este cambio no borra datos existentes.
- Los ADR anteriores sobre WhatsApp y profesor se mantienen como historial; su
  implementación de runtime queda supersedida por esta decisión.

## Consecuencias

- La API no necesita secretos de WhatsApp, firma de webhook, workers, medios ni
  credenciales de STT. Gemini se usa únicamente para interpretar texto.
- No se sube ni persiste audio en el backend. La disponibilidad del dictado
  depende del soporte del navegador.
- Las planillas confirmadas siguen guardándose como snapshot, pero no hay una
  vista de profesor ni endpoints para consultarlas desde ese módulo.
- Las migraciones viejas todavía pueden crear estructuras de compatibilidad en
  una base nueva; no son entidades activas del modelo ni rutas accesibles.
