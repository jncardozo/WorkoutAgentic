# ADR 0004 — Sesión de captura por WhatsApp (estados, aclaraciones y respuestas)

- **Estado:** Aceptado (2026-10-21)
- **Contexto:** ADR-0002 (S3), ADR-0003 (S6), `docs/domain/nlu-captura.md` §4/§5, `docs/domain/business-rules.md`

## S1 — Alumno identificado por teléfono y sesión hablante

**Decisión:** el teléfono canónico de WhatsApp (`WhatsappPhone.Canonizar`) identifica
al alumno; el primer contacto crea la cuenta (`Estado "ACTIVO"`, sin email ni
profesor). Sobre ese alumno, la conversación de captura vive en `SesionCaptura`
(estados `EnCurso`, `EsperandoAclaracion`, `Expirada`, `Lista`, `Confirmada`):
la sesión "hablante" es la última del alumno con cualquier estado distinto de
`Confirmada`. [SUPUESTO] MVP de un solo diálogo activo por alumno; no hay
sesiones paralelas ni selector.

## S2 — Ciclo de vida y pausa por inactividad

**Decisión:**

- `Lista` (tras CIERRE) no acepta más carga: cualquier mensaje responde la
  invitación a revisar/confirmar en la SPA, hasta que la planilla se confirme
  (entonces `Confirmada` y el próximo mensaje abre sesión nueva). [SUPUESTO] no
  hay "reabrir" de una planilla lista por chat.
- Sin actividad durante **2 h** (o la sesión ya venía `Expirada`) → `Expirada`
  persistida + aviso. Solo la palabra exacta **"seguir"** reanuda (vuelve a
  `EnCurso`, o a `EsperandoAclaracion` si quedó una pregunta viva). Los mensajes
  previos a "seguir" NO se procesan: nada entra a la planilla en una pausa.
- Cada mensaje aplicado avanza `UltimaActividadUtc` y `RowVersion` (columna
  simple, no token de concurrencia EF): el worker serializa mensajes por scope,
  el valor es cabecera de auditoría, no mecanismo de bloqueo.

## S3 — Granularidad de filas: por serie

**Decisión:** un mensaje genera una fila por serie ("3x12 con 40" → tres filas
con `NroSerie` consecutivos desde `SerieOrdinal` o `OrdenSerieSiguiente`).
`OrdenSerieSiguiente` avanza con el bloque: el lote se numeró y re-ordenar
responde el mensaje puntual de serie ("segunda serie" fija el ordinal). Al fijar
un ejercicio distinto al activo, la numeración reinicia en 1 (cada ejercicio lleva
su propia cuenta). Las filas comparten estado dentro del lote: una aclaración
resuelta aplica al lote completo (`Contexto.registroIds` referencia a todas).

## S4 — Aclaraciones: pregunta viva con contexto JSON mínimo

**Decisión:** una sola `AclaracionPendiente` por sesión (PK = FK). Guarda el
mínimo y determinista: `{"registroIds":[…],"crudo":"…"}` (500 chars máx). Al
responder, el servicio re-deriva TODO sin otra llamada al LLM:

- **peso/reps/tiempo/serie** → parser de reglas fijas (`ParseadorAclaraciones`):
  "50", "50,5", "50 kilos y medio", "kilos y medio" → 1,5; dígitos puros para
  enteros; `"otro"` y `"seguir"` exactos. Lo que no matchea es `null` = reintento,
  nunca un valor inventado.
- **ejercicio** → opciones enumeradas re-derivadas con el MISMO
  `CatalogoEjercicioResolver.Resolver(crudo, catálogo)` de la pregunta original:
  la lista numerada no cambia entre re-preguntas.

Tras **2 reintentos** sin resolver, la pregunta se abandona: las filas quedan
`PendienteAclaracion=true` (y `DescripcionPendiente=true` si no tienen ejercicio)
y el flujo sigue con el próximo mensaje [desvío: el mensaje que disparó el
abandono no se re-procesa; alternativa que se evaluó y se dejó para después].
**CIERRE** durante una aclaración: abandona lo pendiente y cierra en `Lista`.

## S5 — El servicio valida los faltantes (no confía en el LLM)

**Decisión:** `CampoAclarar` del LLM es una pista; el faltante REAL lo calcula el
servicio contra el catálogo (`CampoFaltante`), con prioridad ejercicio > peso >
reps > tiempo y reglas por tipo: un ejercicio `UsaPeso` sin peso pide peso (salvo
`PesoCorporal`); `UsaTiempo` pide tiempo; los ejercicios por tiempo NO piden reps.
Para sostenerlo, `aclarar.campo` de `NluSchema` incluye `"tiempo"`.

## S6 — Normalización de valores es responsabilidad del servicio

**Decisión:** conversiones explícitas en `SesionCapturaServicio`, nunca en el
prompt: `"lb"` → kg con factor fijo 0,45359237 y redondeo a 2 decimales
(AwayFromZero) — la planilla queda 100 % en kg; peso hablado sin unidad → `Kg`;
`PesoCorporal` → peso `null` y unidad `PesoCorporal`; tiempo sin unidad →
`Unidad.Tiempo`. Un faltante sigue siendo `null` (regla madre).

## S7 — Alta de ejercicio vs datos de serie

**Decisión:** `CARGA_EJERCICIO` sin datos de serie es alta "de palabra": fija el
activo y responde la invitación a cargar la serie, SIN filas. `Ambiguo`/`SinMatch`
no usan el activo (el alumno nombró otro ejercicio): con datos de serie las filas
se crean de una (pendientes, referenciadas por id en el contexto); sin datos solo
queda la pregunta. Serie sin ejercicio nombrado y sin activo ("12 con 45" a
secas) → pregunta simple sin filas: [pérdida pequeña documentada] los números de
ese mensaje no se anotan; el alumno los repite tras nombrar el ejercicio. `"otro"`
deja el crudo como descripción libre (`DescripcionPendiente`), editable en la SPA.

## S8 — Auditoría de voz

**Decisión:** cada audio exitoso inserta una `Transcripcion` (`MediaId` de Graph,
nunca la URL temporal; `Proveedor "Gemini"`; `DuracionSeg` del mensaje de
WhatsApp). El texto crudo queda para rastrear desvíos (reglas de negocio §4).

## Consecuencias

- El flujo del chat es estricto por mensaje: cada mensaje atiende exactamente
  una pregunta o un intent; no hay cola de faltantes ni backtracking — así la
  maquinaria es simple de seguir para el alumno.
- `ParseadorAclaraciones` y el umbral del resolver son datos fijos: los casos
  reales que fallen se suman como fixtures (`/nlu-case`), no como prompt.
- La sesión `Lista` hasta la confirmación en la SPA bloquea cargas nuevas por
  chat (aceptable para el MVP; una "sesión v2" podría abrir carga distinta).
- El abandono tras 2 reintentos deja filas parcialmente llenas que el profesor ve
  marcadas al confirmarse la planilla: la información nunca se pierde en silencio.
