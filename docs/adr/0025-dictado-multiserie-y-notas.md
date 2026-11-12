# ADR 0025 — Dictado de varias series, comentarios y observaciones

- **Estado:** Aceptado (2026-11-10), solicitado por el usuario.
- **Actualización de voz:** ADR-0028 reemplaza la escucha continua del motor por
  ciclos de una sola frase, conservando la sesión de hasta 60 segundos.
- Sustituye las restricciones de ADR-0011/0013/0014 que limitaban cada
  dictado a una frase y descartaban los datos del alta de un lote.

## Decisión

- Un mensaje puede crear un lote de hasta 30 series con los datos expresados.
  Los valores comunes explícitos se aplican a todas las filas; los detalles
  de una serie tienen prioridad. Los ordinales del detalle de un lote son
  relativos a ese lote (1..N), aunque la numeración guardada continúe un lote anterior.
- Se agregan `Comments` y `Observations`, opcionales, a cada parcial y al
  snapshot. Comentarios conserva el tipo de serie expresado (aproximación,
  entrada en calor, calentamiento, efectiva); observaciones conserva las
  sensaciones e incidentes del alumno, sin diagnósticos ni inferencias.
- Un mensaje sin cantidad de series puede actualizar varias filas existentes.
  Sin ejercicio usa el lote vivo; con ejercicio exige coincidencia inequívoca
  de nombre normalizado o crudo y ordinal dentro de la sesión. No crea filas.
  La forma histórica «ejercicio + ordinal» sin datos sigue creando una fila.
- Antes de aplicar se validan todos los ordinales, valores y destinos. Un
  detalle ambiguo, duplicado, fuera de lote o inválido rechaza el mensaje
  completo y pide aclararlo en la SPA. No se aplican cambios parciales.
- Reps y carga siguen determinando completitud (ADR-0020). Las notas no son
  obligatorias. Ausencia significa null; una edición manual puede limpiarlas.
- Una migración aditiva agrega las columnas nullable sin modificar migraciones
  históricas. Confirmación, historial y exportaciones conservan ambas notas.
- La escucha continua dura como máximo 60 segundos desde el clic, incluso si
  el navegador finaliza antes y se reinicia por silencio. Parada manual,
  errores de permiso/red y salida de la pantalla cancelan los reinicios.
  Los resultados finales se agregan una sola vez al borrador; los interinos
  no se envían. El envío espera a que termine el reconocimiento.

## Consecuencias

El flujo anterior sigue disponible, pero ya no es obligatorio separar alta y
datos. El reconocimiento depende del navegador/dispositivo; la aplicación no
puede garantizar captura continua si el sistema interrumpe el micrófono.
Se prueba el contrato NLU con respuestas simuladas y la aplicación de filas
con SQLite; la calidad de transcripción se verifica adicionalmente en móvil.
