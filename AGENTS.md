# WorkoutAgentic — Instrucciones del proyecto

**Voice Tracker MVP**: el alumno escribe o dicta su entrenamiento en la SPA.
El navegador reconoce la voz y la API recibe texto; el sistema interpreta ese
texto con NLU y genera una planilla en cuadrícula. El alumno la revisa, corrige
y confirma. El alcance actual es solo el módulo alumno. WhatsApp y el módulo de
profesor se retiraron del runtime por ADR-0018; los ADR y migraciones anteriores
se conservan como historial.

## Stack

| Capa | Tecnología |
|---|---|
| Backend | .NET (API REST, C#) — `src/api` |
| Frontend | Angular 21 (SPA standalone) — `src/web` |
| Captura | SPA: `POST /api/capturas/{alumnoId}/texto` (texto o voz reconocida por el navegador) |
| IA | NLU: LLM JSON Schema + normalización de ejercicios a texto libre (ADR-0016) |
| Datos | SQLite (WAL) con EF Core → Postgres al escalar |
| Diseño | Prototipo Stitch; dark + Space Grotesk/Geist/JetBrains Mono |

## Antes de escribir código

1. **Convenciones**: `docs/conventions/dotnet.md` y
   `docs/conventions/angular21.md`. `docs/conventions/whatsapp.md` es histórica
   y no aplica al runtime actual.
2. **NLU**: toda lógica de interpretación DEBE seguir
   `docs/domain/nlu-captura.md` (intents, entidades, expresiones coloquiales).
   Ante duda o caso no cubierto, sumar el caso al spec.
3. **Dominio**: validar contra `docs/domain/business-rules.md`. Regla madre:
   nunca inventar datos; ante ambigüedad, pedir aclaración en la SPA.
4. **Decisiones**: consultar `docs/adr/`; toda decisión nueva crea un ADR numerado.

## Estructura

```
src/api     → API .NET: NLU de texto, estado de sesión, persistencia y APIs de planilla/alumno
src/web     → SPA Angular del alumno: entrada, dictado por navegador, captura y cuadrícula
docs/       → convenciones, dominio, arquitectura y ADRs
```

Las migraciones históricas pueden conservar estructuras de profesor/WhatsApp en
la base. El código actual no las mapea ni las consulta y no las elimina para
preservar datos existentes.

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

## Reglas del proyecto

- Conversación y UI en español. Identificadores de código en inglés.
- No inventar datos: texto reconocido, intención, entidades y aclaraciones son
  etapas separadas; si una etapa falla, no se aplican valores inventados.
- Estados separados: `RegistroParcial` ≠ `PlanillaConfirmada`.
- Las cuentas de alumno se crean de forma explícita por `POST /api/alumnos`.
- Las aclaraciones y respuestas ocurren en la misma SPA.
- Secretos en `dotnet user-secrets` para dev / variables de entorno en prod.
- Nunca editar migraciones ya aplicadas; agregar una nueva si cambia el esquema.
- No crear carpetas fuera de `src/` y `docs/` sin consultarlo.
