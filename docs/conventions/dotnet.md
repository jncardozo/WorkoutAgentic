# Convenciones .NET — `src/api`

> Fuente de verdad para el backend. Ante conflicto, gana este documento.

## Proyecto y estructura

- Vertical slices: cada feature vive en una carpeta `Features/<Nombre>/` con sus
  endpoints, DTOs, handlers y tests. No controllers gigantes por capa técnica.
- `Program.cs` mínimo: solo registro de servicios y pipeline; la configuración por
  feature vive en su carpeta.
- Nombres de namespaces con file-scoped syntax: `namespace WorkoutAgentic.Api.Features.Routines;`

## Estilo C#

- Indentación 4 espacios (ver `.editorconfig`).
- DTOs como `record` inmutables: `public record CreateRoutineRequest(string Name, ...)`.
- `async/await` hasta el final: nunca `.Result`, `.Wait()` ni `.GetAwaiter().GetResult()`.
- Endpoints por Minimal APIs con grupos (`RouteGroupBuilder`) o controllers — decidir
  por feature y ser consistente dentro de la feature.
- Validación de entrada en el borde de la API: DataAnnotations o FluentValidation,
  siempre devolviendo `ProblemDetails` (400/409/etc.), nunca excepciones crudas al cliente.
- Errores: excepciones para casos excepcionales; resultados esperados del dominio como
  tipos de resultado, no como excepciones.

## EF Core

- Configuración por entidad con `IEntityTypeConfiguration<T>` en `Data/Configurations/`.
- Migraciones: seguir la skill `dotnet-migrations`. Nombres en inglés PascalCase.
- Consultas: `AsNoTracking()` para lecturas; proyecciones a DTO en vez de devolver
  entidades.

## Pruebas (xUnit)

- Nomenclatura `Metodo_Scenario_Resultado`: `GenerateRoutine_WithNoEquipment_ProducesBodyweightPlan`.
- Un test, una afirmación lógica. AAA (Arrange-Act-Assert) visible.
- Tests de integración con `WebApplicationFactory` y BD de prueba (provider ligero,
  ej. SQLite in-memory o Testcontainers — ver ADR cuando se decida).

## Pipeline de interpretación (Voice Tracker)

Toda la lógica de captura respeta esta separación mínima: cada etapa es una interfaz
con implementación intercambiable y errores tipificados:

```
ISpeechToText        (audio → texto, español)
IIntentClassifier    (texto → CARGA_EJERCICIO | CARGA_SERIE | CIERRE | AYUDA | INVALIDO)
IEntityExtractor     (texto → Ejercicio | Serie | Repeticiones | Peso | Unidad, normalizados)
SesionCapturaService (estado del diálogo: ejercicio activo, serie siguiente, parciales)
```

- Errores de pipeline → tipos de resultado de dominio (no excepciones cruzando capas).
  Ambigüedad/dato faltante → resultado `AclaracionRequerida`, nunca valor inferido.
- El webhook solo orquesta: firma, ruteo por tipo (`text`/`audio`) y encolado.
  Toda la lógica UTF-8 de negocio vive fuera de la clase del endpoint.
- Concurrency del alumno: mensajes por WhatsApp pueden llegar casi simultáneamente;
  `SesionCaptura` debe soportar respuestas ordenadas (RowVersion o similar, ver ADR BD).
- Config de proveedores (STT/NLU) por DI con abstracción: poder cambiar vendor sin
  tocar el pipeline.

## Seguridad


- Secretos: `dotnet user-secrets` en desarrollo. `appsettings.json` solo valores no sensibles.
- CORS: orígenes explícitos de `src/web`, nunca `AllowAnyOrigin` con credenciales.
- Toda ruta del webhook de WhatsApp: firma validada (ver `whatsapp.md`).
