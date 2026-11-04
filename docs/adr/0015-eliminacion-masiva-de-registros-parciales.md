# ADR 0015 — Eliminación masiva de registros parciales de la cuadrícula

- **Estado:** Aceptado (2026-10-24)
- **Contexto:** ADR-0007 define el CRUD de `RegistroParcial` editable antes de
  confirmar. La SPA ya permite eliminar filas una por una y necesita poder
  vaciar de una vez la cuadrícula de la sesión vigente.

## Decisión

Se agrega `DELETE /api/planillas/sesion-actual/{alumnoId}/filas`. El endpoint
resuelve la sesión hablante del alumno y aplica las mismas restricciones de
estado que las demás mutaciones de la cuadrícula (`EnCurso` o `Lista`). Elimina
todos sus `RegistroParcial`, conserva la sesión y devuelve la vista refrescada.
No modifica ni elimina planillas confirmadas.

La SPA muestra la acción cuando hay filas y solicita una segunda confirmación
antes de ejecutar el borrado. Tras el éxito reemplaza la vista con la sesión
vacía; ante un error conserva los datos recibidos del servidor.

## Consecuencias

- La operación es idempotente: vaciar una sesión sin filas produce la misma
  vista vacía.
- Una sesión en aclaración o expirada sigue protegida por las reglas comunes de
  mutación.
- Una planilla confirmada permanece inmutable y separada de los parciales.
