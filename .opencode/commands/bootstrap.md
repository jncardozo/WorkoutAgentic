---
description: Bootstrap del repo — ADR-0002 a Aceptado, git init + commit base, y scaffold de src/api y src/web según docs/arquitectura.md
---

Bootstrap del proyecto WorkoutAgentic "Voice Tracker MVP". Ejecuta en este orden y
con confirmación antes de cada bloque mayor:

1. **Contexto**: lee `docs/arquitectura.md`, `docs/adr/0002-decisiones-mvp.md` y
   `docs/domain/business-rules.md`. No re-diseñes nada: este bootstrap implementa lo
   decidido. Si encuentras contradicción, pregunta en vez de improvisar.
2. **ADR-0002 → Aceptado**: cambia el estado del ADR a "Aceptado (2026-10-21)".
3. **Git**: en la raíz del repo: `git init -b main`, y un primer commit con TODO lo
   existente (harness + docs), mensaje: `chore: harness + arquitectura + ADRs`.4. **Scaffold backend** (`src/api`):
   - `dotnet new sln -n WorkoutAgentic` → `dotnet new webapi -n WorkoutAgentic.Api`
     y `dotnet new xunit -n WorkoutAgentic.Api.Tests`, agregados a la solución.
   - Ajusta `WorkoutAgentic.Api.csproj` a .NET 10 (LTS) si el template trae otra versión,
     y `UseWAL` en la connection string de SQLite — NO instala EF Core providers aún:
     deja el scaffold limpio (EF Core entra con el primer slice según ADR S3).
   - `dotnet build` en verde. `dotnet new editorconfig` NO: ya existe uno en el repo.
5. **Scaffold frontend** (`src/web`):
   - Verifica Node/npm disponibles; si faltan, ABORTA este paso con instrucciones.
   - `ng new web --style=scss --skip-git --defaults` dentro de `src/`.
   - Verifica que NO agregue `provideZoneChangeDetection` (zoneless es default en v21)
     y que el runner de tests sea Vitest; si el CLI pregunta otra cosa, respeta la
     convención `docs/conventions/angular21.md`.
   - Ajusta la dependencia dev de Angular a ~21.x si hace falta según CLI actual.
   - `ng build` en verde (sin tocar la estética del prototipo todavía).
6. **Verificación y commit**:
   - Corre `/run-checks` mentalmente aquí: `dotnet build/test` + `npm test/build`.
   - Segundo commit: `chore: scaffold src/api y src/web (ADR-0002 aceptado)`.
   - Resumen final de lo creado + próximos pasos sugeridos (primer slice: webhook + firma).
