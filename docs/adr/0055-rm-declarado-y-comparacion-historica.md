# ADR 0055 — 1RM declarado y comparación histórica en reportes

- **Estado:** Aceptado (2026-11-24)
- **Contexto:** el alumno necesita registrar su repetición máxima por ejercicio
  y conocer qué proporción representa la mayor carga de otra sesión respecto de
  esa referencia. Agregar una nota libre no permite consultar ni validar esa marca.

## Decisión

- `IsOneRepMax` identifica una **declaración explícita del alumno** de su máximo
  para una repetición. Aplica a cualquier ejercicio con carga externa numérica,
  sin catálogo cerrado. No certifica una medición ni estima un 1RM.
- Requiere `Reps = 1`, peso convertido a kg mayor que cero y ausencia de tiempo.
  No acepta peso corporal o tiempo. La mayor carga de una sesión no se convierte
  automáticamente en RM. Tampoco un récord de varias repeticiones.
- El NLU incorpora `es_rm` nullable a raíz y a detalles: `true` marca, `false`
  quita por declaración expresa y `null` conserva lo anterior. Reconoce RM, 1RM,
  uno RM, erre eme, repetición máxima y equivalentes inequívocos. Un «récord
  personal» sin indicar una repetición requiere aclaración. Contradicción,
  repeticiones ausentes o alcance ambiguo requieren aclaración en la SPA; el
  alumno reenvía el dictado aclarado. El backend valida los valores finales de
  **todos** los destinos antes de modificar filas.
- El campo aparece en la cuadrícula, alta manual, edición individual y editor
  múltiple. Convive con comentarios/observaciones y forma parte de los valores
  originales usados para detectar conflictos concurrentes. No cambia datos
  numéricos automáticamente al activar el control.
- Se agrega el campo a `RegistroParcial` y `PlanillaFila` con migración nueva.
  Solo una marca confirmada, completa y no eliminada aporta referencias futuras.
  Las filas históricas empiezan sin marca; no se interpretan notas antiguas.
- Al confirmar, se selecciona el **mayor RM de planillas anteriores** del mismo
  alumno y ejercicio cuya confirmación no sea posterior a la nueva. Los empates
  se resuelven por confirmación más reciente, mayor ID de planilla y menor orden
  de fila. El RM de la sesión que se está confirmando sirve para sesiones futuras;
  su propio reporte lo muestra marcado y lo compara con la referencia previa.
- La identidad comparada es el nombre normalizado libre, ignorando mayúsculas y
  espacios redundantes. No se unen variantes, equipos, acentos o sinónimos por
  similitud; se prefiere ausencia de comparación a mezclar ejercicios diferentes.
- Cada fila confirmada congela peso, fecha UTC de confirmación, ID de planilla y
  orden de fila de la referencia. No hay FK al origen: eliminarlo lógicamente lo
  excluye de **nuevas** comparaciones sin alterar reportes ya confirmados.
  La confirmación y sus referencias se guardan en una misma transacción.
- La fecha mostrada es la fecha de confirmación del RM en Argentina, no una
  fecha de entrenamiento deducida del dictado. No se agrega carga retroactiva.
- El backend calcula por ejercicio `máxima carga numérica de sesión / RM previo
  × 100`, con un decimal y redondeo away-from-zero. No limita a 100 %: superar
  una referencia no demuestra por sí solo que haya un nuevo 1RM.
- Sin referencia se informa «Sin RM previo registrado»; sin carga numérica no
  se calcula porcentaje. La relación de cargas no afirma mejora o retroceso de
  fuerza ni emite recomendaciones de entrenamiento.
- PDF, Excel, vistas previas y adjuntos usan el mismo snapshot. PDF muestra la
  comparación con el primer detalle de cada ejercicio y pagina las explicaciones
  extensas. Excel mantiene la hoja `Reporte`, agrega la columna de RM y una hoja
  `Progreso RM` con valores numéricos, fecha, procedencia, porcentaje y explicación.
  La API expone `oneRepMaxProgress` calculado únicamente desde datos congelados.

## Consecuencias y validación

- Amplía ADR-0019 (exportación), ADR-0025 (captura de varias series), ADR-0027
  (edición) y ADR-0054 (historial), conservando su inmutabilidad y propiedad.
- Un RM menor posterior se conserva, pero no reemplaza al mayor histórico como
  referencia. Si el producto necesita el último RM evaluado, requiere cambiar
  esta regla explícitamente; no se deduce pérdida de fuerza.
- No se requieren nuevas llamadas de IA para calcular o redactar el análisis.
- Pruebas de contrato del proveedor simulado, captura atómica, conversión de
  unidades, edición/conflictos, persistencia, aislamiento alumno/variante/fecha,
  borrado del origen, porcentajes mayores que 100 y exportaciones PDF/XLSX.
  Las pruebas simuladas no certifican precisión de un proveedor LLM en vivo.
