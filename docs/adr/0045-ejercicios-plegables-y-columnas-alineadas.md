# ADR 0045 — Ejercicios plegables y columnas alineadas

- **Estado:** Aceptado, solicitado por el usuario.
- **Fecha:** 2026-11-19.

## Contexto

Un entrenamiento completo genera muchas tarjetas y un recorrido vertical largo.
En el detalle del reporte, las filas flexibles repartían el espacio según sus
notas: comentarios y observaciones largos desalineaban serie, reps y peso.

## Decisión

- Las tarjetas por ejercicio de la sesión editable, planilla confirmada y detalle
  del reporte se implementan con `details`/`summary` nativos. Inician abiertas;
  el encabezado completo permite plegar/desplegar cada una con toque o teclado.
  Nombre, cantidad de series y flecha permanecen visibles. No forman un acordeón
  exclusivo: el alumno puede dejar abiertas las que necesite.
- Plegar conserva los nodos y borradores inline. El seguimiento por clave de
  ejercicio conserva el estado del desplegable durante las actualizaciones del
  mismo grupo. No se persiste una preferencia entre cargas de página.
- El detalle PDF de `ReporteWorkspace` usa tablas semánticas con estructura y
  anchos compartidos: Serie, Reps, Peso, Comentarios y Observaciones. Las celdas
  vacías muestran «—» y mantienen la posición de las demás columnas.
- Serie, reps y peso no parten líneas. Las notas conservan saltos de línea y
  ajustan textos largos dentro de su columna; todas las celdas se alinean arriba.
  En móvil la tabla tiene desplazamiento lateral dentro de cada ejercicio para
  mantener anchos legibles; el encabezado queda fuera de ese desplazamiento.
- Cambia la presentación, sin contratos de API, datos ni archivos descargables.

## Verificación

Ejecutar lint, pruebas y build; revisar plegado independiente, borradores inline,
actualización de grupos, nombres largos y columnas con notas extensas y vacías
en móvil/escritorio. Verificar que las filas numéricas mantienen las mismas
posiciones entre ejercicios y que no hay desbordamiento lateral de la página.
