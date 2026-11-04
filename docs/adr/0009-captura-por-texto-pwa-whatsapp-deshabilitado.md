# ADR 0009 — Pivot: captura por texto en la PWA; WhatsApp deshabilitado

- **Estado:** Aceptado (2026-10-21)
- **Vigencia:** WhatsApp se retiró del runtime por ADR-0018; este ADR queda
  como historial del pivot a captura web.
- **Contexto:** ADR-0002 (S1/S2: STT + NLU), ADR-0004 (sesión de captura y
  aclaraciones), ADR-0008 (SPA Alumno), `docs/domain/nlu-captura.md`,
  `docs/conventions/whatsapp.md`

## Decisión (resumen)

El MVP del Voice Tracker deja de depender del canal de WhatsApp: el alumno
**dicta o escribe su entrenamiento en la PWA** y el texto crudo entra al MISMO
pipeline nacido para el chat (NLU → núcleo de sesión → respuesta del bot). El
webhook y los envíos de WhatsApp quedan **deshabilitados en producción**; el
código y su suite se conservan compilando.

Motivos: el onboarding de Meta (cuenta de negocio, verificación, teléfono)
agregaba fricción sin aportar al valor del MVP; el dictado por voz del teclado
del celular produce texto con el mismo resultado que la nota de voz; y una
sola superficie (PWA) concentra capturar, revisar, corregir y confirmar.

## S1 — Endpoint `POST /api/capturas/{alumnoId}/texto`

**Decisión:** nuevo slice `Features/CapturaTexto` con un único endpoint que
recibe `{ texto }` y reutiliza todo lo existente:

- 400 «Texto requerido», 404 «Alumno inexistente» (validado ANTES de consultar
  el NLU: sin sesión creada ni análisis), 503 «No pudimos interpretar la
  captura» cuando una etapa del análisis devuelve null — nada se aplicó.
- La respuesta compone la `RespuestaSesionActual` del PlanillaApi (mismo
  contrato que la SPA ya consume; se lee con `IPlanillaConsulta` tras aplicar)
  más `MensajeAlumno` (la respuesta del bot para el banner).
- Id de mensaje para la traza auditable: `spa-{alumnoId}`.
- El alta de alumno por primer contacto era del flujo WhatsApp: acá el borde
  valida que exista (404) y no se crea nada on-the-fly. El alta/lista de
  alumnos es el pendiente de dominio del siguiente slice (Profesor).

## S2 — Entrada de texto directo en el NLU

**Decisión:** `IMensajesNlu` gana `AnalizarTextoAsync(texto, mensajeId, ct)`;
`WhatsappCapturaPipeline` lo implementa con el mismo núcleo de etapas comparti
do (una llamada al LLM + resolver determinístico sobre el catálogo), mientras
la rama de audio conserva su descarga + STT antes del núcleo. La
interpretación NO se duplica: la rama de texto del webhook ahora también pasa
por `AnalizarTextoAsync`.

## S3 — Núcleo de sesión compartido por canal

**Decisión:** `ISesionCapturaServicio` añade `AplicarTextoAsync(alumnoId,
analisis, ahora, ct)`. Ambas entradas (`AplicarAsync` para WhatsApp y la
nueva) corren un núcleo común `NucleoAsync(alumno, mensaje?, analisis, …)`,
donde `mensaje` aporta solo la auditoría de audio (transcripciones) y el
origen del log («origen PWA/WhatsApp»); el alumno se resuelve por telefono en
el canal WhatsApp y por id en la PWA.

- `MarcarMensajeBotAsync` con id null queda no-op: las aclaraciones nacidas en
  la PWA no tienen id de mensaje de WhatsApp (la columna ya era nullable).
- El flujo de sesión (estados, aclaraciones, «seguir», cierre) queda intacto.

## S4 — Aclaraciones respondidas desde la PWA

**Decisión:** la pregunta viva sigue viajando en `SesionActualVista.Aclaracion`
(ya existente); el alumno responde por el mismo panel de dictado. El texto
entra como un mensaje más y `ResolverPendienteOIntentarAsync` corre igual que
en el chat. Nada del flujo de aclaración cambia.

## S5 — Panel «Dictá tu entrenamiento» en la SPA

**Decisión:** panel persistente encima de la cuadrícula (textarea + Enviar;
Enter envía con `preventDefault`). En respuesta exitosa:
`planillaConfirmada.set(null)` (dictar tras una confirmación abre una carga
nueva), `sesion.set(respuesta.respuesta)`, banner con `mensajeAlumno` y
borrador limpio. La encuesta se pausa mientras la captura está en curso.

- Los errores del dictado usan su propia señal `fallaCaptura` y se muestran
  EN el panel (los de filas siguen en `fallaAccion`, dentro de la tarjeta).
- Los copys de la vista quedan sin referencias a WhatsApp (banners,
  sin-sesión, pie de planilla confirmada, chip de estado).
- [SUPUESTO] Los textos del bot (`TextosBot`) mantienen la redacción del
  slice de chat («Mandame la serie…»): siguen siendo comprensibles en la PWA;
  una revisión de copys específica por canal queda para después.

## S6 — WhatsApp deshabilitado, no borrado

**Decisión:** en `Program.cs` el map del webhook queda tras la bandera
`Whatsapp:WebhookHabilitado`, que NO está configurada en producción
(deshabilitado por defecto). Las pruebas de integración del webhook la
encienden con `UseSetting` para conservar su cobertura de firma, handshake y
cola. `dotnet test` sigue en verde (205 pruebas, ninguna llama a Meta ni a
Gemini) — el canal puede revivirse solo con la bandera + secrets.
