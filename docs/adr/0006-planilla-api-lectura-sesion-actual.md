# ADR 0006 — PlanillaApi (lectura): sesión hablante para la SPA del alumno

- **Estado:** Aceptado (2026-10-21)
- **Contexto:** arquitectura.md §2/§5/§6, ADR-0004 (S1/S4), `docs/domain/business-rules.md`

## S1 — Single endpoint de lectura: `GET /api/planillas/sesion-actual/{alumnoId}`

**Decisión:** el slice de lectura de `PlanillaApi` arranca con UN endpoint que
expone la sesión hablante del alumno (la última con estado distinto de
`Confirmada`, igual que el chat) con su cuadrícula de `RegistroParcial`
ordenada por creación. Semántica HTTP:

- `404` (ProblemDetails) SOLO si el alumno no existe.
- Sin sesión hablante (sin sesiones o todas confirmadas) → **200 con
  `Sesion = null`**: es un estado normal de la cuenta, no un error.
- La edición de filas, el alta y la confirmación (snapshot POST) entran en
  slices siguientes — acá el alumno solo ve lo capturado por WhatsApp.

**Identificación provisional:** el alumno viene por ruta (`{alumnoId:int}`)
hasta `AuthApi` (arquitectura S6: cookie + roles por email). Es el punto a
reemplazar; el árbol de rutas de alumno nunca se mezcla con el de profesor
(`ProfesorApi` vendrá después).

## S2 — Contrato: faltantes reales como null, marcas del dominio

**Decisión:** las filas son la proyección fiel de `RegistroParcial`:

- Datos faltantes → `null` (regla madre: nunca inventar). La SPA decide
  cómo pedirlos, la API no rellena.
- `PendienteAclaracion` viaja por fila: es `true` con la pregunta VIVA (se
  contesta por WhatsApp) y SOBREVIVE al abandono tras los 2 reintentos —
  en ambos casos la fila está incompleta en la planilla.
- La aclaración viva se expone como `{campo, reintento}` (solo para mostrarla;
  la respuesta siempre es por chat).
- `Estado` de la sesión y `Unidad` se serializan como texto del enum en el
  mapper (JSON estable, sin depender de opciones globales de serialización):
  `"EnCurso"`, `"Lista"`, `"Expirada"`; `"Kg"`, `"Tiempo"`, etc.

## S3 — Fixtures de integración: `UseSetting` y BD efímera real

**Decisión:** hallazgo de este slice: en un host minimal-API (`WebApplicationBuilder`)
corrido con `WebApplicationFactory`, los `ConfigureAppConfiguration` del
factory **no** alcanzan a `Program.cs` (el connection string se resuelve
eager en `AddCapturasDb(builder.Configuration)`). Los tests debían usar
`builder.UseSetting(...)`, que sí gana sobre `appsettings.json`. Se corrigió
el fixture del webhook también, que desde su origen corría contra la BD dev
(`workoutagentic.db`) porque su override no aplicaba — el suite pasó verde
solo porque la creación de alumno es idempotente.

## Consecuencias

- La SPA del alumno nace con datos reales del chat (polling al enfocar,
  arquitectura S7); ninguna vista duplica la lógica de estados del diálogo.
- La vista muestra `Expirada`, `Lista` (invitación a confirmar) y filas
  incompletas sin interpretar: la mutación y la confirmación son el siguiente
  slice, y quedan en el mismo árbol `/api/planillas`.
- El mapeo estado↔texto del enum queda en un solo lugar (el mapper del
  servicio de consulta); si AuthApi cambia la identidad del alumno, solo
  cambia el borde del endpoint, no el contrato.
