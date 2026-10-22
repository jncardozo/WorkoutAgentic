# WorkoutAgentic — Instrucciones del proyecto

**Voice Tracker MVP** (pivot ADR-0009): el alumno **dicta o escribe su
entrenamiento en la PWA** (texto crudo, español), el sistema lo interpreta con
el mismo pipeline NLU del chat y genera una **planilla en cuadrícula** en la
SPA. El alumno la revisa/corrige, la **confirma**, y desde ese momento queda
visible en **solo lectura** para su profesor. El canal de WhatsApp queda
**deshabilitado** (código y suite conservados).

## Stack

| Capa | Tecnología |
|---|---|
| Backend | .NET (API REST, C#) — `src/api` |
| Frontend | Angular 21 (SPA standalone) — `src/web` |
| Captura | **PWA**: `POST /api/capturas/{alumnoId}/texto` (dictado/texto) — WhatsApp deshabilitado por ADR-0009 |
| IA texto | NLU: **LLM JSON Schema + `CatalogResolver` determinístico** — ADR-0002/S2 |
| Datos | **SQLite (WAL)** con EF Core → Postgres al escalar — ADR-0002/S3 |
| Diseño | Figma (MCP `figma`) y prototipo Stitch (MCP `stitch`) — dark + Space Grotesk/Geist/JetBrains Mono |

## Antes de escribir código

1. **Convenciones**: `docs/conventions/dotnet.md`, `docs/conventions/angular21.md`
   y `docs/conventions/whatsapp.md` (obligatoria para mensajería).
2. **NLU**: toda lógica de interpretación DEBE seguir `docs/domain/nlu-captura.md`
   (intents, entidades, expresiones coloquiales). Ante duda o caso no cubierto:
   preguntar/sumar el caso al spec, nunca improvisar.
3. **Dominio**: validar contra `docs/domain/business-rules.md`. Regla madre:
   **nunca inventar datos** — si falta o es ambiguo, pedir aclaración al alumno
   en la misma superficie de captura (PWA o chat si el canal vuelviera).
4. **Decisiones**: consultar `docs/adr/`; toda decisión nueva crea un ADR numerado.

## Estructura planeada

```
src/api     → API .NET: captura por texto (PWA), pipeline (NLU → estado de
              sesión), persistencia, APIs para la SPA; webhook WhatsApp
              deshabilitado tras `Whatsapp:WebhookHabilitado` (ADR-0009)
src/web     → SPA Angular: vista Alumno (cuadrícula editable) y vista Profesor
              (solo lectura), login con roles
docs/       → convenciones, dominio (glosario, reglas, spec NLU) y ADRs
```

## Comandos

```sh
# Backend (desde src/api)
dotnet build
dotnet test

# Frontend (desde src/web)
npm run lint
npm test
npm run build
```

## Reglas inquebrantables

- **Idioma**: conversación y UI en español (variantes regionales). Identificadores
  de código y commits en inglés; comentarios y mensajes de UI en español.
- **No inventar datos**: transcripción, intención, entidades y aclaraciones son etapas
  separadas; si una etapa falla, el flujo deriva a aclaración explícita, jamás a un
  valor inventado.
- **Estados separados**: registros parciales (`RegistroParcial`) ≠ planilla confirmada
  (`PlanillaConfirmada`). Nunca mezclar tablas ni permisos.
- **Roles**: endpoints de alumno ≠ endpoints de profesor. El profesor solo puede leer
  planillas confirmadas.
- **Captura**: el texto crudo entra por `POST /api/capturas/{alumnoId}/texto`
  (ver ADR-0009); aclaraciones respondidas desde la misma PWA.
- **Webhook WhatsApp (deshabilitado)**: si se reviviera el canal, toda entrada
  valida firma `X-Hub-Signature-256` y el audio se descarga vía Graph API
  (convención whatsapp.md, deprecada).
- **Secretos**: `dotnet user-secrets` en dev / variables de entorno en prod.
  Nada de tokens en código ni en `appsettings.json` (ver `.env.example`).
- **Migraciones**: nunca editar una ya aplicada (skill `dotnet-migrations`).
- **Alcance**: no crear carpetas fuera de `src/` y `docs/` sin consultarlo.
