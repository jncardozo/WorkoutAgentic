# ADR 0014 — Carga del dato de serie por dictado: `Nº SERIE + REPS y/o PESO` (+ retiro del TIEMPO de la UI)

- **Estado:** Aceptado (2026-10-23)
- **Contexto:** ADR-0013 dejó el lote base con filas de **solo ejercicio y
  número de serie** (los datos «se completaban después») y el MVP quedó con
  tres cosas sueltas: (a) la SPA mostraba un banner duplicado y confuso de
  «Te falta responder un detalle…» cuando había aclaración viva; (b) la
  columna `Tiempo` de la cuadrícula igualmente quedaba en `null` — un campo
  que no se dicta ni se pregunta, que no aporta y sí confunde; (c) el alumno
  no tenía forma alguna de cargar reps/peso por dictado. El usuario lo pidió
  con un ejemplo concreto: «Primera serie 12 repeticiones con 20Kilos».

## Decisión

La carga queda en DOS pasos. El paso 1 es la regla base de ADR-0013 sin
cambios; se agrega el paso 2:

```
PASO 1 (ADR-0013): EJERCICIO + SERIES  → filas con ejercicio + nº de serie
PASO 2 (ADR-0014): Nº DE SERIE + REPS y/o PESO  (SIN nombrar ejercicio)
                   → "primera serie 12 repeticiones con 20 kilos"
                     "segunda serie con 30" · "tercera serie 10 al cuerpo"
```

### 1. Efecto del paso 2 (`AplicarDatosSerieAsync`, puerta `EsCargaDatosSerie`)

- **Fila objetivo (enmienda 2026-10-23)**: la de ese ordinal del **LOTE VIVO** —
  las filas del último paso 1 creado (comparten el mismo `CreadoUtc`; editar
  filas o dictar datos no lo cambia). Incluye los lotes de **TEXTO LIBRE**
  (SinMatch resuelto como «otro»: la fila lleva el crudo y no hay id de
  catálogo). El `EjercicioActivoId` heredado ya no decide el objetivo; la SPA
  muestra el lote vivo en «Estás cargando: X» (canónico ?? crudo de la última
  fila creada). Si «segunda serie» repitiera el número dentro del lote vivo,
  gana la más vieja (Id asc).
- **Solo se escriben los valores DICHOS**: reps y/o peso con su unidad; el
  resto de la fila queda como estaba; repetir un valor lo sobrescribe
  (**último dictado gana**). `fila.PendienteAclaracion = false` cuando el
  dato llega. No se crean filas y `OrdenSerieSiguiente` no cambia.
- **Peso**: kg asumido («50» suelto o «20 kilos» → `Unidad.Kg`); «en libras» →
  lb con conversión exacta ×`0,45359237` (round 2, `NormalizarValores` — las
  mismas reglas de spec NLU §2 vigentes, ADR-0004); «al cuerpo» →
  `Unidad.PesoCorporal` con peso null.
- **Fuera de la regla** (sin ordinal, sin dato hablado, o sesión SIN filas —
  no hay lote vivo que apoye): la misma guía única de ADR-0013
  (`FueraDeRegla`), sin crear nada.
- **Fila inexistente** (p. ej. «sexta serie» con 4 filas): aviso corto
  (`TextosBot.SerieSinFila`) — **nada se inventa** (regla madre; el alta
  manual vive en la app — «+ Agregar fila»).
- **Aclaración viva de ejercicio (enmienda 2026-10-23, decisión del
  producto)**: el paso 2 SÍ se aplica aunque la pregunta «¿de qué ejercicio
  es esa serie?» siga esperando: el dato entra en la fila del lote vivo y la
  pregunta queda viva (`TextosBot.ConRecordatorioPendiente`), SIN reintento —
  dictar el dato no es una respuesta (errónea) a la pregunta. Revierte la
  versión original de este ADR (`PrimeroEjercicio`): exigir el ejercicio
  antes del dato frenaba el flujo real (caso de campo: dicta «sentadilla
  libre 4 series», pregunta viva, y «primera serie 12 con 20» debía llenar la
  fila igual).
- **LLM caído (enmienda 2026-10-23, bug de campo)**: cuando el análisis llega
  con `Clasificacion.Exito = false` (cuota 429, red, salida no parseable),
  el mensaje puede ser perfectamente válido — solo no se pudo leer. Responde
  `TextosBot.FalloLectura` («No pude leer bien esos datos 🙂 ¿Me los pasás de
  nuevo?») en cualquier estado (con o sin aclaración viva): SIN reintento, SIN
  abandono, SIN tocar filas. Causa real corregida: dos mensajes válidos +
  Gemini 429 contaron como respuestas erróneas y abandonaron el lote pendiente
  con «La dejé marcada para completar después…».

### 2. Retiro del TIEMPO de la UI (SPA)

- Se quita la columna `Tiempo` de la cuadrícula (en curso y snapshot
  confirmado) de la vista Alumno y de la vista Profesor, y los campos de
  tiempo de los formularios de alta y edición. La API conserva `tiempoSeg`
  (sin migración; el dato de filas viejas se conserva).
- **`CampoFaltante` ya no exige «tiempo»**: un ejercicio por tiempo (plancha)
  queda definido por su fila solo con ejercicio (y serie); las reps ya no
  tienen sentido en un isométrico, así que tampoco se le exigen. Esto evita
  filas «Incompleta» permanentes por un dato que la UI ya no puede capturar.
- Los formularios dejan de bloquear reps para ejercicios de tiempo (el dato
  es opcional); el peso para ellos sigue validado como error (el backend
  rechaza peso en ejercicio por tiempo).

### 3. Prompt NLU (ADR-0002/S2)

El LLM extrae las entidades del paso 2 (ordinal OBLIGATORIO, reps, peso como
número exacto, unidad `lb`/`peso_corporal`/null-kg-default). `CARGA_EJERCICIO`
sigue extrayendo SOLO ejercicio + series (datos null) — la ruta la decide el
CONTENIDO del análisis en el servicio (`EsCapturaBase` vs
`EsCargaDatosSerie`), no el intent etiquetado.

## Textos bot nuevos

- `SerieConDatos(nombre, nro, detalle)` — «Serie N de X anotada: 12 reps con 20 kg.»
- `DetalleSerie(...)` — detalle legible: «12 reps con 20 kg», «10 reps al
  cuerpo», «con 30 kg»…
- `SerieSinFila(nombre, nro)` — «No veo la serie N de X en la cuadrícula…»
- `ConRecordatorioPendiente(respuesta)` — respuesta del dato + recordatorio de
  que la pregunta del ejercicio sigue viva (sin reintento).
- `FalloLectura` — «No pude leer bien esos datos 🙂 ¿Me los pasás de nuevo?»
  (análisis fallido del LLM; en cualquier estado, sin reintento).
- `SeriesAnotadasBase` / `Ayuda` / `Cortesia` / `FueraDeRegla` enseñan ahora
  los dos pasos (con el ejemplo del dato de serie).

## Consecuencias

- El flujo de captura queda completo: armar filas → dictar el dato de cada
  serie → (cierre + confirmación). El dato por fila también puede
  editarse/cargarse desde la cuadrícula de la SPA, como hasta hoy.
- La SPA queda sin banner de aclaraciones: la pregunta del bot vive solo en
  el panel de dictado y las filas pendientes muestran su chip «pendiente del
  bot».
- TDD: 13 tests de servicio (`DatosDeSerie_*`, incluidos lote vivo, texto
  libre, pregunta viva y análisis fallido) + tests SPA/endpoint existentes
  ajustados; e2e con Gemini real sobre la sesión de prueba.
- Pendiente a futuro: cuando exista peso por serie con detalle (RIR, tempo),
  esta regla podrá extenderse; el tiempo puede volver con una decisión
  producto explícita (dado «30 segundos», dictado opcional + columna).
