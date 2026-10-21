---
description: Ejecuta build, tests y lint de backend y frontend, y resume los errores
---

Ejecuta las verificaciones del proyecto en este orden (salta lo que aún no exista):

1. `dotnet build` en `src/api`
2. `dotnet test` en `src/api`
3. `npm run lint` en `src/web`
4. `npm test` en `src/web`
5. `npm run build` en `src/web`

Reporta una tabla final: paso → ✅/❌ → resumen del error (archivo y línea si aplica).

Importante: si algo falla, NO lo corrijas en esta ejecución. Solo reporta. La corrección
se hace en otra conversación o con instrucciones explícitas mías.
