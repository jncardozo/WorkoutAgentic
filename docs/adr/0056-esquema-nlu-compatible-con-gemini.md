# ADR 0056 — Esquema NLU compatible con el límite de estados de Gemini

- **Estado:** Aceptado (2026-11-24).
- **Contexto:** tras ADR-0055, Railway registró respuestas `400 INVALID_ARGUMENT`
  de Gemini indicando que el esquema producía demasiados estados. Fallaba antes
  de interpretar incluso altas simples como «press de banca inclinado con
  mancuernas 4 series» y «sillon de cuadriceps 4 series». La SPA mostraba el fallo
  genérico de lectura y no se creaban filas. Se reprodujo el rechazo con el
  esquema desplegado y el modelo configurado, `gemini-3.5-flash-lite`.

## Decisión

- Conservar la salida JSON Schema tipada y la separación de etapas del pipeline.
  No cambiar de modelo ni recurrir a valores inventados para sortear el error.
- Quitar `maxItems` del array de detalles y `minimum` de su ordinal en el esquema
  enviado al proveedor. Esos límites expanden los estados del generador.
- Exigir todas las claves de los objetos, incluidos los detalles de cada serie,
  manteniendo nullable en los datos que pueden faltar. Una clave requerida no
  obliga a inventar un valor: dato ausente sigue siendo null. Así se reducen las
  ramas por claves opcionales y se exige que el proveedor incluya los campos de
  peso y RM. El prompt refuerza que no se omitan datos expresados.
- Para un alta con cantidad explícita, aceptar tanto `series=null` como detalles
  con ordinal válido y datos null: el proveedor real usa ambas representaciones
  para las series vacías. No agregan valores ni alteran la cantidad declarada.
  En actualizaciones se sigue rechazando un detalle sin datos.
- Los límites de negocio siguen siendo responsabilidad determinística del
  backend: lotes y listas de detalles hasta 30, ordinales positivos y únicos,
  destinos existentes, reps/peso válidos y reglas de RM. Todas las validaciones
  terminan antes de cambiar filas. No hay cambio de esquema de base de datos.
- Agregar regresiones del contrato enviado al proveedor y de los dos comandos
  reportados: cada uno crea cuatro filas con reps/peso null en una sesión
  editable sin series pendientes. Con series anteriores incompletas sigue
  aplicando el bloqueo de ADR-0020; con sesión expirada sigue exigiéndose «seguir».
- Además de los tests simulados, validar el esquema con llamadas manuales al
  proveedor real antes de publicar cambios en este contrato. Usar credenciales
  configuradas, sin imprimirlas ni persistirlas en fixtures.

## Evidencia y alcance

- Logs del despliegue `009c1075-6f25-4cc4-947e-893fec44df8c` del commit `ced4390`:
  rechazos del esquema el 24/11/2026 a las 19:08–19:09 UTC.
- La prueba real del esquema anterior devolvió el mismo 400. Simplificarlo
  permitió respuestas 200 con ambos ejercicios, cantidad 4 y datos ausentes null.
- Un 503 por demanda del proveedor es un fallo independiente y conserva el
  reintento acotado existente. Ningún test simulado demuestra disponibilidad
  ni precisión semántica permanente del servicio externo.

## Referencia

- [Gemini: simplificación de esquemas complejos](https://ai.google.dev/gemini-api/docs/structured-output#simplifying-complex-schemas).
