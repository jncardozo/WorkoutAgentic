# WorkoutAgentic — Instrucciones del proyecto

**Voice Tracker MVP**: el alumno carga su entrenamiento por WhatsApp (notas de voz o
texto en español), el sistema interpreta el contenido y genera una **planilla en
cuadrícula** en la SPA. El alumno la revisa/corrige, la **confirma**, y desde ese
momento queda visible en **solo lectura** para su profesor.

## Stack

| Capa | Tecnología |
|---|---|
| Backend | .NET (API REST, C#) — `src/api` |
| Frontend | Angular 21 (SPA standalone) — `src/web` |
| Mensajería | WhatsApp Cloud API (Meta) — webhooks + descarga de notas de voz (audio/ogg) |
| IA audio | Transcripción es-419: **Gemini API** (audio-in) — ADR-0002/S1 |
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
   **nunca inventar datos** — si falta o es ambiguo, pedir aclaración por WhatsApp.
4. **Decisiones**: consultar `docs/adr/`; toda decisión nueva crea un ADR numerado.

## Estructura planeada

```
src/api     → API .NET: webhook WhatsApp, pipeline (transcripción → NLU → estado de
              sesión), persistencia, APIs para la SPA
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
- **Webhooks**: toda entrada valida firma `X-Hub-Signature-256`. Mensajes de tipo
  `audio` descargan el archivo vía Graph API (ver convención WhatsApp).
- **Secretos**: `dotnet user-secrets` en dev / variables de entorno en prod.
  Nada de tokens en código ni en `appsettings.json` (ver `.env.example`).
- **Migraciones**: nunca editar una ya aplicada (skill `dotnet-migrations`).
- **Alcance**: no crear carpetas fuera de `src/` y `docs/` sin consultarlo.
