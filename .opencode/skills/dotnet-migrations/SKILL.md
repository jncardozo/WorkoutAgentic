---
name: Dotnet Migrations
description: Crear y aplicar migraciones de EF Core de forma segura para el esquema de WorkoutAgentic
---

## Cuándo usar esta skill

- Cualquier cambio en entidades, configuración de EF Core o esquema de base de datos.

## Flujo de trabajo

1. Crear la migración con un nombre descriptivo del cambio, no del ticket:
   ```sh
   dotnet ef migrations add NombreDescriptivo --project src/api
   ```
2. **Revisar el `Up()`/`Down()` generado** antes de aplicar. Puntos de atención:
   - Operaciones destructivas (DropColumn, DropTable, cambios de tipo) → confirmar con
     el usuario antes de aplicar.
   - Datos semilla: prefieren.HasData en la configuración de la entidad.
3. Aplicar:
   ```sh
   dotnet ef database update --project src/api
   ```
   O generar script para entornos controlados:
   ```sh
   dotnet ef migrations script --idempotent --output migrations/ultima.sql
   ```
4. Verificar que `dotnet test` sigue en verde (los tests de integración usan el esquema).

## Reglas inquebrantables

- **Nunca editar una migración que ya fue aplicada** en una base de datos distinta a la
  local. Si el modelo cambió de nuevo, crear otra migración encima.
- No usar `dotnet ef database drop` sin confirmación explícita del usuario.
- El nombre de la migración va en inglés (PascalCase), igual que el resto del código.
