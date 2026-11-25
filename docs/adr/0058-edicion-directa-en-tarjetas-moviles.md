# ADR 0058 — Edición directa en tarjetas móviles con alternativa de grilla

- **Estado:** Aceptado para prueba de uso (2026-11-25).
- **Amplía:** ADR-0027, ADR-0029 y ADR-0032.

## Contexto

Durante el entrenamiento, buscar «Editar» al final de una tabla con scroll
horizontal resultó incómodo. El alumno necesita consultar y modificar reps,
peso, RM y notas con el teléfono. El usuario solicita poder retirar el diseño
nuevo y volver a la grilla si la experiencia no mejora.

## Decisión

**Ampliación ADR-0059 (2026-11-25):** las tarjetas móviles retiran el desplegable
de ejercicio/número; conservan sus valores al guardar. La edición de identidad
sigue disponible en grilla y editor completo.

- En anchos hasta 767 px, mostrar por defecto tarjetas de series editables en
  la pantalla principal, agrupadas por ejercicio. Peso y repeticiones se editan
  tocando el campo; RM queda visible. Notas y ejercicio/número se despliegan.
  Los grupos empiezan abiertos y conservan su expansión al refrescar; los nuevos
  se abren para mostrar los datos recién cargados.
- Incluir el selector «Tarjetas / Grilla» en móvil. La grilla existente permanece
  disponible para comparar y volver sin despliegue adicional. La elección dura
  mientras la pantalla está montada; no se persiste en cuenta, base ni navegador.
  Desktop conserva la tabla, con «Editar» visible también en la primera columna.
- Reutilizar `TrainingEditor` con presentación `embedded`, compartiendo borradores,
  normalización, validaciones, detección de cambios, conflictos y API de edición
  múltiple. El editor de pantalla completa conserva sus grupos iniciales cerrados.
  La variante embebida está aislada por input y punto de montaje: puede retirarse
  sin modificar entidades, capturas, reportes o contratos de servidor.
- No hay autosave. Cada tarjeta modificada muestra «Sin guardar». Una barra fija
  inferior aparece al tener cambios y ofrece «Cancelar» y «Guardar cambios (N
  series)». Solo se envían las filas intervenidas en un único PUT atómico existente.
  Un error conserva los borradores; éxito actualiza la sesión y continúa en tarjetas.
- Cancelar con cambios pide confirmación y restaura los datos registrados sin
  abandonar las tarjetas. Cambiar de vista o salir por el router también solicita
  descarte. Recargar/cerrar usa el aviso nativo existente de beforeunload.
- Con borradores o guardado en curso, pausar el refresco e ignorar sus respuestas
  en vuelo. Deshabilitar captura/dictado, confirmación, alta y borrado masivo;
  consultar otra planilla pide guardar/cancelar primero. El texto de captura ya
  escrito se conserva. Un cambio de ancho no desmonta tarjetas con cambios.
- Cada serie ofrece eliminación con confirmación, disponible sin borradores.
  Usa la mutación existente; no crea otro mecanismo de persistencia.
- Mantener teclado numérico/decimal, entradas de al menos 16 px y controles de
  al menos 44 px. La barra fija acompaña `VisualViewport` (alto y offset) para
  quedar sobre el teclado. Reservar espacio inferior y safe area. Todo el scroll
  del diseño de tarjetas es vertical.
- Las restricciones de sesión y las validaciones de RM siguen vigentes. No
  inventar datos ni convertir valores pendientes al tocar un control. Las cargas
  corporales históricas se conservan si no se modifica el peso, como en ADR-0029.

## Verificación y reversibilidad

- Pruebas de edición sin guardado implícito, descarte, error, RM, cambios por fila,
  guardado explícito, eliminación, cambio de vista y refrescos en vuelo.
- Prueba de adaptación a VisualViewport y revisión en navegador con datos QA
  simulados en 320/360/390 px y grilla en 768/1280 px. El navegador de prueba
  verifica ausencia de overflow horizontal de página y acceso a guardar/editar.
- El criterio final de aceptación es la lectura/edición durante el uso real en
  el gimnasio. Si el usuario lo rechaza, retirar la presentación embebida y su
  montaje móvil; conservar la grilla y el editor de pantalla completa anteriores.
