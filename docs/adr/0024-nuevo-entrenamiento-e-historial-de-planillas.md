# ADR 0024 — Nuevo entrenamiento e historial de planillas

- **Estado:** Aceptado (2026-10-27)
- **Contexto:** después de confirmar una planilla, la sesión activa queda vacía
  y el primer dictado puede abrir una nueva, pero la interfaz no ofrece una
  acción explícita para empezar ni permite recuperar confirmaciones anteriores.

## Decisión

- Mostrar «Nuevo entrenamiento» después de confirmar. La acción limpia la vista
  local de la planilla actual y deja el formulario listo para una carga nueva.
- Crear la sesión nueva de forma diferida con el primer mensaje válido; no
  persistir sesiones vacías ni borrar filas o snapshots confirmados.
- Exponer el historial del alumno mediante una consulta de solo lectura y
  permitir abrir cualquiera de sus reportes confirmados.
- Mantener cada snapshot inmutable y ordenar el historial por fecha de
  confirmación descendente.

## Consecuencias

- El alumno tiene un camino visible para cargar otro entrenamiento sin conocer
  comandos de voz.
- Las planillas anteriores siguen disponibles después de empezar otra carga.
- La consulta del historial no altera el estado de captura ni el contenido de
  las planillas confirmadas.
