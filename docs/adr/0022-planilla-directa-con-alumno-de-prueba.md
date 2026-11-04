# ADR 0022 — Inicio directo en la planilla con alumno de prueba

- **Estado:** Aceptado (2026-10-27)
- **Contexto:** ADR-0008 S1, ADR-0018, prueba de la SPA con alumnos invitados.

## Decisión

La ruta raíz de la SPA abre directamente la pantalla de captura y revisión de la
planilla. Se elimina el formulario que pedía el número de alumno y el botón para
cambiar de alumno. Durante este MVP, la SPA pasa internamente el ID `1` como
alumno de prueba.

## Consecuencias

- El alumno ve la carga de entrenamiento como primera interfaz y no necesita
  conocer su identificador interno.
- Todas las personas que prueben esta versión comparten los datos del alumno #1
  y su historial. No debe usarse para datos personales reales.
- La base de datos desplegada debe contar previamente con el alumno #1; el
  runtime no lo crea automáticamente.
- Al implementar autenticación, la SPA resolverá el alumno asociado a la cuenta
  autenticada y dejará de usar el ID fijo.
- La autenticación de Cloudflare es independiente de esta decisión; sus errores
  de callback deben diagnosticarse en el túnel/navegador.
