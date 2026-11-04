# Spec NLU — Captura de entrenamientos en español (Voice Tracker MVP)

> Fuente de verdad para la interpretación. Toda implementación debe ajustarse a este
> spec; los casos nuevos se agregan con `/nlu-case` y quedan como fixtures de test.

## 0. Regla del dictado (ADR-0013 + ADR-0014)

El alumno carga en DOS pasos sobre la misma cuadrícula:

```
PASO 1 · armar filas
  EJERCICIO + SERIES            → "press de banca 4 series"  (lote de N filas)
  EJERCICIO + SERIE PUNTUAL     → "sillón, tercera serie"    (una fila con ese nº)

PASO 2 · cargar el dato de UNA serie (sin nombrar ejercicio)
  Nº DE SERIE + REPS y/o PESO   → "primera serie 12 repeticiones con 20 kilos"
                                  "segunda serie con 30" · "tercera serie 10 al cuerpo"
```

Efecto del PASO 1 (lo aplica `SesionCapturaServicio`, no un intent):

- Nacen las filas con **ejercicio (crudo + canónico) y número de serie SOLAMENTE**:
  todos los datos de serie quedan `null`. **Todo dato extra que el alumno diga
  en el paso 1 se ignora: ni se pregunta ni se guarda** (el dato llega por el
  paso 2).
- El nombre canónico es texto libre normalizado por el NLU, no una identidad
  elegida de un catálogo. Variantes equivalentes como «press de banca con
  mancuernas» y «banco plano con mancuernas» comparten la etiqueta; el campo
  `EjercicioCrudo` conserva la frase original.
- Numeración: lote nuevo de un ejercicio nuevo ⇒ 1..N; si el ejercicio ya
  tiene filas en la sesión ⇒ continúa desde `OrdenSerieSiguiente`. La serie
  puntual usa su ordinal tal cual. Con el ejercicio aún SIN resolver
  (aclaración viva) la numeración es provisional: empieza en el ordinal o
  en 1 — nunca hereda el contador de otro ejercicio.
- Tope por lote: `MaxSeriesLote = 30` [SUPUESTO]; una cantidad mayor es
  ruido de voz → responde la guía, no crea filas.

Efecto del PASO 2 (ADR-0014, determinístico sobre entidades extraídas):

- Va dirigido a la **fila de ese ordinal del LOTE VIVO** — las filas del
  último paso 1 creado (comparten `CreadoUtc`; editar filas o dictar datos no
  lo cambia). Incluye lotes de texto libre, sin id de catálogo. La SPA lo
  muestra en «Estás cargando: …» (canónico ?? crudo; el
  `EjercicioActivoId` heredado ya no decide el objetivo). Solo se escriben los
  valores dichos: reps y/o peso (con su unidad); lo demás de la fila queda
  como estaba y repetir un valor lo sobrescribe (último dictado gana).
- `fila.PendienteAclaracion = false` (el dato llegó; la fila deja de "esperar
  al bot"). Sin filas nuevas, `OrdenSerieSiguiente` no cambia.
- Falta el ordinal, falta el dato o la sesión no tiene filas (no hay lote
  vivo) → guía única, sin tocar estado. **La fila no existe** (p. ej. «sexta
  serie» con 4 filas) → aviso corto (`SerieSinFila`), jamás se inventa la
  fila (el alta manual vive en la app).
- Con **aclaración viva** («¿de qué ejercicio es esa serie?») el paso 2 SÍ se
  aplica al lote vivo y la pregunta SIGUE esperando su respuesta
  (`ConRecordatorioPendiente`) — SIN reintento: dictar el dato no es una
  respuesta (errónea) a la pregunta (ADR-0014, enmienda por feedback real:
  "sentadilla libre 4 series" + "primera serie 12 con 20" llenaba la fila y
  dejaba la pregunta viva).
- **LLM caído** (`Clasificacion.Exito = false`: cuota 429, red, salida no
  parseable): el análisis puede ser de un mensaje perfectamente válido, solo
  no se pudo leer → `FalloLectura` («No pude leer bien esos datos 🙂 ¿Me los
  pasás de nuevo?») en CUALQUIER estado, SIN reintento, SIN abandono y sin
  tocar nada (ADR-0014; causa real corregida: 2 mensajes válidos + 429
  abandonaban el lote con «La dejé marcada…»).

**Fuera de las dos reglas** (sin ejercicio, sin series en el paso 1, sin
ordinal/dato en el paso 2): guía única («decime el ejercicio y las series en
un solo mensaje») sin tocar estado ni apoyarse en filas ajenas al lote vivo.

- La única aclaración viva posible es **la variante del ejercicio cuando hay
  ambigüedad real**. La pregunta es abierta, sin una lista cerrada. No tener
  una entrada previa no es motivo para preguntar: el NLU normaliza el nombre
  como texto libre. Para series/peso/reps no se abre una aclaración pendiente;
  si falta un dato, se aplica la regla de avance ADR-0020. El campo TIEMPO
  se retiró del MVP (ADR-0014): no se dicta, no se pide y ya no hace falta
  para el snapshot (los ejercicios por tiempo quedan con ejercicio + serie).
- Verbatim para peso: kg asumido; "en libras" → lb → se convierte a kg con
  `0,45359237` (round 2); "al cuerpo" → `Unidad.PesoCorporal` con peso null.

**Regla de avance (ADR-0020):** cada fila del ejercicio requiere tanto
repeticiones como peso, o la indicación explícita «al cuerpo». Los datos pueden
llegar en mensajes parciales (primero reps, luego peso, o viceversa), pero la
API no permite crear el lote de otro ejercicio mientras haya una serie
incompleta en la sesión. La SPA muestra exactamente qué ejercicio, serie y dato
falta. «Al cuerpo» cuenta como carga declarada y no requiere kilos numéricos.
El alta manual de fila exige ambos datos antes de guardarse; las filas creadas
por dictado siguen naciendo vacías para poder completarlas después.

## 1. Intents

| Intent | Trigger típico | Efecto en el estado |
|---|---|---|
| `CARGA_EJERCICIO` | Nombra un ejercicio (con o sin series) | En regla ⇒ lote base (filas solo con ejercicio + serie); sin series ⇒ guía |
| `CARGA_SERIE` | Datos de UNA serie sin nombrar ejercicio (paso 2, ADR-0014) | Con ordinal + dato ⇒ llena la fila del LOTE VIVO; sin ordinal/dato sin filas ⇒ guía; NO hay apoyo fuera del lote vivo |
| `CIERRE` | "terminé", "listo", "ya está", "eso es todo" | Consolida ⇒ sesión `LISTA`; si había aclaración viva, la abandona |
| `AYUDA/CONSULTA` | "cómo hago para…", "qué puedo decir" | Respuesta con la regla; NO modifica estado |
| `INVALIDO` | No es captura (saludos, spam, otros temas) | Respuesta de cortesía + ejemplos en regla |

La clasificación la decide el LLM (JSON Schema, ADR-0002 S2), pero las puertas
son determinísticas dentro de `SesionCapturaServicio` (ADR-0013/0014):
`EsCapturaDatosSerie` exige ordinal 1..n + algún dato hablado y SIN ejercicio
nombrado; `EsCapturaBase` exige ejercicio nombrado + series en 1..30 (o un
ordinal puntual). Por eso un intent mal etiquetado no rompe el flujo: el
contenido del análisis decide la ruta.

## 2. Entidades (con normalización)

| Entidad | Uso en el dictado | Ejemplos → salida |
|---|---|---|
| `ejercicio` | Forma literal expresada por el alumno | "banco plano con mancuernas" → texto original |
| `ejercicio_normalizado` | Nombre estándar libre producido por el NLU; formas equivalentes deben coincidir | "banco plano con mancuernas" → `Press de banca con mancuernas` |
| `serie` (ordinal) | integer 1..n; OBLIGATORIO en el paso 2 | "primera", "la una", "2°", "tercer" → 1, 1, 2, 3 |
| `cantidad_series` | integer 1..30 | "4 series", "3x12" (el múltiplo) → 4, 3 |
| `repeticiones` | Solo en el paso 2 (CARGA_SERIE); SIEMPRE null en CARGA_EJERCICIO | "12 repeticiones" → 12 |
| `peso` | Solo en el paso 2 (número exacto); SIEMPRE null en CARGA_EJERCICIO | "45", "40 y medio" → 45, 40.5 |
| `unidad` | Solo en el paso 2: "en libras"/"libras"→`lb`; "al cuerpo"→`peso_corporal`; kg dicho/no dicho → null (kg es el default del sistema) | "40 en libras" → 40 + lb; "20 kilos" → 20 + null |
| `tiempo` | **SIEMPRE null** (campo retirado del MVP, ADR-0014) | "1 minuto de plancha" → null |
| `aclarar` | Solo para ambigüedad real del ejercicio; la pregunta es abierta y no enumera opciones | "¿Qué variante hiciste? Decime el nombre con tus palabras." |

Normalización al aplicar el paso 2 (`NormalizarValores`): lb→kg exactas
(`LibrasPorKilo = 0,45359237`, round 2 away-from-zero); peso hablado sin
unidad ⇒ `Unidad.Kg` ("50" suelto → kg, spec NLU §2 en vigencia); "al cuerpo"
⇒ unidad `PesoCorporal` con peso null. El tiempo no entra en el dictado.

## 3. Formas de mensaje frecuentes (rioplatense/español neutro)

- Verbos de carga: "cargá / hacé / anotá / registrá / anotame / sumá / metele"
  (voseo) y sin voseo: "carga", "anota", "registra", "toma nota". Son ruido si
  el mensaje no trae la forma en regla → guía.
- Lotes en abreviado: "3x12" es cantidad de series = 3 (el 12 son reps, se
  ignoran).
- Correcciones: **no hay por dictado en el MVP**; la cuadrícula de la SPA es
  editable (el alumno edita/elimina/agrega filas antes de confirmar, reglas §2).
- Cierres: "terminé/listo/ya está/eso es todo/no cargo más". ✅ lista no
  exhaustiva: ver corpus.

## 4. Reglas de ambigüedad y aclaración

- El NLU normaliza cada nombre expresado a una etiqueta libre; no necesita
  existir previamente. Si no puede determinar la variante con seguridad,
  crea las filas pendientes y formula una pregunta abierta.
- La respuesta a esa pregunta también pasa por NLU. No hay opciones enumeradas
  ni nombres exactos de catálogo que el alumno deba conocer.
- Si la aclaración sigue sin resolver tras 2 reintentos → el lote queda
  marcado (`PendienteAclaracion`/`DescripcionPendiente`) y el flujo sigue;
  la edición en la SPA limpia la marca (reglas §2).
- No hay aclaración de peso/reps/tiempo/serie: el flujo del dictado es solo
  las dos reglas (paso 1 y paso 2, ADR-0013/0014). Si el alumno dicta datos
  de serie con una aclaración de ejercicio viva, el bot repite la pregunta
  pendiente **sin contar reintento** (un mensaje válido no es una respuesta
  ilegible). Peso corporal y ejercicios por tiempo crean sus filas igual.
- El TIEMPO se retiró del MVP (ADR-0014): ya no se dicta, no se pide y
  `CampoFaltante` no lo exige — una fila de plancha con solo ejercicio y
  serie NO es Incompleta en el snapshot.

## 5. Estado de sesión y memoria

La memoria conversacional vive en `SesionCaptura` (persistida):

```
SesionCaptura {
  alumnoId, estado: EN_CURSO|ESPERANDO_ACLARACION|LISTA|CONFIRMADA|EXPIRADA,
  ejercicioActivoId?, ordenSerieSiguiente,
  aclaracion?: { campo='ejercicio', reintento, contextoJSON={registroIds, crudo} }
}
```

- Cada mensaje resuelve: valida la aclaración pendiente SI hay una (CIERRE
  domina y la abandona; un dictado de datos de serie solo recuerda la
  pregunta, sin reintento), sino procesa el intent — `EsCapturaBase` crea
  filas y `EsCapturaDatosSerie` aplica datos (ADR-0013/0014).
- Con la carga en `LISTA` (cerrada, esperando confirmación en la SPA):
  - un dictado **EN REGLA** la reabre (`EN_CURSO`, ADR-0012) y aplica el
    lote; el cierre siguiente vuelve a dejarla en `LISTA`;
  - "seguir" la reabre conservando la aclaración viva que hubiera;
  - fuera de la regla, ayuda, cortesía e inválidos invitan a confirmar sin
    tocar estado (los fuera-de-regla reciben la guía).
- Privacidad: el navegador procesa la voz y envía el texto reconocido. La API
  no recibe ni persiste audio.

## 6. Corpus de frases (fixtures de test — set mínimo del MVP)

```
"press de banca 4 series"
 → CARGA_EJERCICIO en regla | press de banca (match), filas serie 1-4,
   reps/peso null; confirmación «Anotadas 4 series… Después pasame el dato»

"cargame 3 series de prensa de 12 con 60"
 → en regla | prensa, 3 filas serie 1-3 (el "12 con 60" queda FUERA)

"sillón, tercera serie"
 → en regla (puntual) | sillón, una fila serie 3 sin datos

"otras 3 del sillón" (mismo ejercicio ya cargado)
 → en regla | el lote continúa la numeración (5 a 7 en partida de 4)

"press de banca con mancuernas 4 series"
 → cuatro filas con esa etiqueta normalizada; no consulta catálogo

"banco plano con mancuernas 4 series"
 → la misma etiqueta normalizada que «press de banca con mancuernas»

"press 4 series"
 → si la variante no queda clara, pregunta abierta y 4 filas pendientes;
   no sugiere ejercicios de una lista cerrada

"primera serie 12 repeticiones con 20 kilos" (paso 2, con filas del lote vivo)
 → CARGA_SERIE en regla | llena la SERIE 1 del LOTE VIVO con
   reps=12, peso=20 kg; aviso «Serie 1 de … anotada: 12 reps con 20 kg»

"tercera serie 15" / "segunda serie con 30" (paso 2, versiones parciales)
 → CARGA_SERIE | escribe SOLO lo dicho (reps=15, o peso=30 con Unidad.Kg);
   lo demás de la fila queda como estaba (último dictado gana)

"tercera serie 10 al cuerpo" (paso 2)
 → CARGA_SERIE | reps=10 + Unidad.PesoCorporal, peso null

"segunda serie 10 con 40 en libras" (paso 2)
 → CARGA_SERIE | lb→kg exactas (40 lb = 18,14 kg), Unidad.Kg

"sexta serie 10 con 20" (y el lote vivo tiene 4 filas)
 → fila inexistente | aviso «No veo la serie 6 de …»; NADA se crea (no se
   inventa la fila: el alta manual vive en la app)

"12 repeticiones con 20" (paso 2 sin ordinal)
 → FUERA de la regla → guía (falta el Nº de serie)

"primera serie 12 con 20" con ACLARACIÓN VIVA ("¿de qué ejercicio…?")
 → CARGA_SERIE en regla: se APLICA la fila del lote vivo (12/20) y la
   pregunta del ejercicio SIGUE viva sin reintento (recordatorio del bot;
   ADR-0014 enmienda 2026-10-23)

"primera serie 12 con 20" y el LLM FALLÓ (429 cuota, red, salida corrupta)
 → FalloLectura «No pude leer bien esos datos 🙂 ¿Me los pasás de nuevo?»;
   SIN reintento, SIN abandono, sin tocar filas ni estado (ADR-0014)

"segunda serie, 10 con 40" SIN filas previas
 → FUERA de la regla (sin ordinal o sin filas/lote vivo) → guía; nada
   se crea ni se apoya en nada ajeno

"press de banca" (sin series)
 → FUERA de la regla → guía; no crea filas ni fija el activo

"dominadas 4 series" / "plancha 4 series"
 → filas base igual, sin datos (el paso 1 no lleva datos, sea cual sea el
   tipo de ejercicio; para plancha el tiempo ya no se pide, ADR-0014)

"terminé, listo"
 → CIERRE

"profe, ¿cómo hago el remo con mancuernas?"
 → AYUDA/CONSULTA (no modifica estado)
```

Casos acumulados de producción (se suman acá y como fixtures de test):

```
"sentadillas hack 4 series y 12 repeticiones, primera serie 40 kg"
 → CARGA_EJERCICIO en regla | lote base de 4 filas (solo ejercicio + series);
   el "12 repeticiones" y el "primera serie 40 kg" se IGNORAN en el paso 1.
   El dato de cada fila llega por el paso 2 ("primera serie 12 repeticiones
   con 40 kilos").
 → [cerrado por ADR-0013/0014] el viejo caso abierto "[?] peso distinto por
   serie en el lote" queda resuelto: el lote no lleva datos; el dato por
   serie se dicta después ("Nº de serie + reps + peso").
```

Mantené este corpus como tests parametrizados del pipeline; cada caso real que
aparezca en producción se suma como fixture (variantes regionales: voseo rioplatense,
"placa" = disco en México, "mancuernas" = pesas, etc.).
