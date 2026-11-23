# ADR 0054 — Confirmación explícita y gestión de entrenamientos guardados

- **Estado:** Aceptado, solicitado por el usuario tras pruebas de QA.
- **Fecha:** 2026-11-23.
- **Amplía:** ADR-0017, ADR-0019, ADR-0024, ADR-0046 y ADR-0050.

## Contexto

QA confirmó una planilla antes de terminar la carga. También necesita identificar
los entrenamientos por un nombre propio y retirar del historial los cargados
incorrectamente. Hasta ahora la fecha encabezaba cada entrada y los snapshots
confirmados solo admitían lectura, exportación y envío.

## Decisión

- «Confirmar planilla» abre un `dialog` nativo con la pregunta «¿Terminaste tu
  entrenamiento?» y la explicación «Estás por confirmar esta planilla. Una vez
  confirmada, no podrás modificar sus series». Solo «Sí, confirmar planilla»
  llama a la API; «Cancelar» y Escape conservan filas y borradores.
  El foco inicial está en Cancelar. Durante la petición se bloquean nuevas
  acciones; un fallo conserva el diálogo y permite reintentar. No modificar
  las condiciones de dominio para confirmar desde `EnCurso` o `Lista`.
- `PlanillaConfirmada.Name` es metadato opcional: máximo 120 caracteres después
  de trim, una sola línea y vacío convertido en null. No inferir nombres a
  partir de ejercicios. Sin nombre se muestra «Entrenamiento #N». Puede
  agregarse, cambiarse o quitarse desde «Mis entrenamientos», también en
  planillas anteriores. No altera series, sesión, fecha ni reportes congelados.
- Orden visual: nombre, fecha, cantidad de series, hora y número de planilla.
  Se conserva el orden descendente por confirmación, no por nombre. La hora es
  la de confirmación almacenada, presentada en la zona local del navegador;
  no representa un comienzo de entrenamiento que el sistema no registra.
  El DTO explicita UTC (`Z`) porque SQLite no conserva `DateTime.Kind`.
- «Eliminar entrenamiento» requiere un segundo diálogo que identifica el
  entrenamiento. Solo la aceptación explícita ejecuta el borrado lógico:
  `DeletedUtc` queda en UTC. La API aplica filtros globales a planillas y filas
  para excluirlo de historial, exportación, envío y cambios de nombre.
  El contador de la SPA se actualiza al éxito, no de forma anticipada.
- La sesión original sigue `Confirmada`; ni la eliminación ni el cambio de
  nombre reabren parciales ni modifican otra carga en curso. El snapshot y sus
  filas permanecen almacenados. No se ofrece restauración en esta versión.
- Los archivos ya descargados/enviados y las peticiones de exportación/envío
  ya iniciadas no pueden recuperarse o revocarse mediante esta eliminación.
  El bloqueo rige para nuevas solicitudes tras el borrado lógico.
- Endpoints `PUT /api/planillas/confirmadas/{id}/nombre` y
  `DELETE /api/planillas/confirmadas/{id}` bajo autenticación, acuerdo,
  propiedad del alumno y CSRF existentes. Un ID ajeno, inexistente o eliminado
  devuelve 404. Updates SQL condicionales no pisan una eliminación concurrente.
- Migración nueva `AddTrainingHistoryMetadata`: dos columnas nullable, sin
  editar migraciones anteriores ni borrar datos. Las planillas existentes
  continúan visibles y sin nombre hasta que su titular lo agregue.
- Cambiar los estados de UI a «Sin sesión de entrenamiento» y «Sin sesión de
  entrenamiento activa» para distinguir captura de autenticación.

## Verificación

- Cancelación sin POST, confirmación explícita, doble clic, fallo/reintento,
  Escape, foco inicial y regreso al control de origen.
- Renombrar normaliza espacios, acepta vacío, rechaza longitud y controles;
  conserva fecha/series/exportaciones y el borrador ante errores.
- Borrado conserva filas y sesión confirmada, actualiza lista/conteo y bloquea
  nuevos reportes/envíos/renombrados; propiedad, CSRF y acuerdo obligatorios.
- Migrar una base con planillas previas conserva snapshots y sus filas.
- Compilación, lint y suites del backend/frontend antes de publicar.
