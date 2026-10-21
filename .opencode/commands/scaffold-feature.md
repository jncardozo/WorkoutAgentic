---
description: Crea una feature vertical completa (API .NET + SPA Angular) siguiendo las convenciones del Voice Tracker
---

Crea la feature "$ARGUMENTS" como slice vertical completa, en este orden:

1. **Contexto**: lee `docs/conventions/dotnet.md` y `docs/conventions/angular21.md`.
   Si la feature toca interpretación/NLU, también `docs/domain/nlu-captura.md`; si toca
   reglas del flujo, `docs/domain/business-rules.md`.
2. **Plan**: propón el plan (endpoints por rol alumno/profesor, DTOs, componentes,
   pantallas, estados de sesión afectados) y espera mi OK antes de escribir código.
3. **Backend** (`src/api`): endpoint(s), DTOs con validación, servicios, pruebas xUnit
   de los casos felices y de error. Respeta la separación parcial/confirmado y los
   permisos por rol.
4. **Frontend** (`src/web`): componente(s) standalone con signals, servicio HTTP tipado,
   prueba de componente. Todos los textos de la vista en español.
5. **Verificación**: corre `dotnet test` y `npm test`; arregla lo que rompa tu código.
6. **Documentación**: si la feature implicó una decisión de arquitectura, crea un ADR
   con el siguiente número disponible en `docs/adr/`.
