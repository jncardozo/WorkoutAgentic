# ADR 0060 — Borrador descartable, historial compacto y cargas por ejercicio

- **Estado:** Aceptado (2026-11-27).
- **Contexto:** el alumno necesita descartar texto mal reconocido antes de
  enviarlo. Las acciones del historial y la comparación con RM ocupan demasiado
  espacio en móvil. La gráfica conjunta de series no identifica sus ejercicios.

## Decisión

- Agregar «Borrar texto» debajo del borrador. Cancela el dictado activo, limpia
  el texto y el aviso del micrófono y devuelve el foco al campo. El alumno puede
  volver a escribir o activar «Dictar». Se bloquea durante el envío y mientras
  hay ediciones móviles pendientes. Solo descarta texto local; no elimina series
  ni envía solicitudes a la API.
- Usar lápiz y papelera para renombrar/eliminar una entrada del historial, con
  tooltips visibles al pasar el puntero o enfocar, nombres accesibles que
  identifican el entrenamiento y áreas táctiles de 44 × 44 px. Se conserva el
  formulario de nombre y la confirmación de eliminación.
- Plegar por defecto la comparación con RM en los reportes PDF/Excel de la SPA.
  Su resumen informa cuántos ejercicios tienen referencia previa, o su ausencia.
  Desplegar conserva todas las descripciones congeladas y la advertencia de
  interpretación. La vista previa de impresión la abre inicialmente completa.
- Reemplazar las columnas conjuntas por tarjetas de cargas por ejercicio en
  ambos reportes de la SPA y en la vista PDF para imprimir. Cada tarjeta muestra
  nombre, número de serie, kilos y repeticiones; usa barras horizontales con escala
  propia explícita entre cero y la máxima carga numérica de ese ejercicio.
  Cero tiene barra de longitud cero. Peso corporal, tiempo y datos ausentes se
  identifican con texto, sin convertirlos a kilos ni omitir sus series.

## Consecuencias y validación

- Presentación exclusivamente frontend: conserva contratos, snapshot, NLU,
  archivos descargables y reglas de RM de ADR-0055.
- Complementa ADR-0022, ADR-0039 y ADR-0054. Verificar lint, pruebas y build del
  frontend; cubrir descarte durante dictado, bloqueos durante envío y edición,
  agrupación y escala de cargas y apertura de comparación histórica.
