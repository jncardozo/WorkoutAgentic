# ADR 0020 — Completar las series antes de avanzar de ejercicio

- **Estado:** Aceptado (2026-10-26)
- **Contexto:** el flujo permitía crear el lote de otro ejercicio aunque las
  series del anterior todavía no tuvieran peso o repeticiones. Como el dictado
  de datos se aplica al lote más reciente, eso podía dejar series incompletas
  sin una forma directa de completarlas por voz.

## Decisión

- Cada serie necesita repeticiones y una carga declarada antes de iniciar el
  siguiente ejercicio. La carga puede ser un peso numérico o la indicación
  explícita «al cuerpo».
- El alumno puede dictar reps y peso en mensajes separados. La API muestra qué
  datos faltan y rechaza la creación de otro lote mientras exista alguna serie
  incompleta en la sesión.
- La regla se valida en el backend para cubrir dictado y clientes directos. La
  SPA muestra las series pendientes y bloquea el alta manual de filas hasta
  completarlas. El alta manual de una fila exige reps y peso, o «al cuerpo».
- Las filas creadas por el dictado continúan naciendo con datos vacíos para que
  el alumno pueda cargarlos después. La edición de esas filas sigue disponible.
- «Al cuerpo» cuenta como carga completa sin almacenar un peso en kg. No se
  infiere el peso de una persona ni se completan valores automáticamente.

## Consecuencias

- El alumno completa cada ejercicio antes de pasar al siguiente; el lote vivo
  sigue siendo inequívoco para la captura por ordinal.
- La API rechaza el lote siguiente con HTTP 200 y una instrucción del bot para
  la captura por texto, y con HTTP 409 para intentos de alta manual.
- Las sesiones existentes con lotes incompletos pueden corregirse editando sus
  filas; la interfaz identifica ejercicio, serie y campos faltantes.
- No cambia la confirmación: las reglas vigentes de planilla permiten confirmar
  filas incompletas y marcarlas en el snapshot. Esta decisión regula el avance
  entre ejercicios.
