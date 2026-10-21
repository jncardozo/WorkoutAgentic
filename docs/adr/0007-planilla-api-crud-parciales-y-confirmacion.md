# ADR 0007 — PlanillaApi (mutaciones): CRUD de parciales + confirmación (snapshot write-once)

- **Estado:** Aceptado (2026-10-21)
- **Contexto:** arquitectura.md §5, ADR-0004 (S1/S5/S6/S7), `docs/domain/business-rules.md` §2

## S1 — Cuadrícula editable solo cuando el chat no tiene la palabra

**Decisión:** las mutaciones (agregar/editar/eliminar filas) van por la sesión
hablante y necesitan estado `EnCurso` o `Lista`.

- `EsperandoAclaracion` → **409** (el bot está esperando una respuesta por
  WhatsApp; si el alumno edita en paralelo, la resolución del chat pisaría o
  duplicaría lo editado — la serialización del diálogo manda).
- `Expirada` → **409** («seguir» reanuda; en la pausa no entra nada a la
  planilla, ADR-0004 S2).
- Filas de una sesión ya `Confirmada` → **409** (`FilaCongelada`: snapshot
  write-once, reglas §2).

[SUPUESTO] es más restrictivo que "editar hasta confirmar" (reglas §2) y el
criterio elegido a favor de la determinística: SPA y chat nunca compiten sobre
el mismo estado vivo.

## S2 — PUT = reemplazo completo + limpieza de la marca de aclaración

**Decisión:** `PUT /api/planillas/filas/{filaId}` reemplaza el conjunto
completo de campos editables (sin ambigüedad absent-vs-null): `ejercicioId`
(nulo → la fila queda como descripción libre y exige `ejercicioCrudo` no
vacío, ADR-0004 S7; con valor → debe existir en el catálogo), `nroSerie`,
`reps`, `pesoKg`, `unidad`, `tiempoSeg`. Un PUT exitoso limpia
`PendienteAclaracion = false`: el alumno intervino la fila que el chat
abandonó; si un dato vuelve a faltar queda como `null` visible y el snapshot
lo marca (`Incompleta`) al confirmar. El `ejercicioCrudo` original no se
sobrescribe al elegir un ejercicio (queda como rastro de lo que dijo el
alumno).

## S3 — Alta exige fila completa; edición no: mismos criterios del chat

**Decisión:** la completitud y la coherencia reusan las funciones internas del
chat (un solo lugar, sin reimplementar reglas de dominio):

- **Alta (`POST /filas`):** fila COMPLETA según el tipo de ejercicio, con
  `SesionCapturaServicio.CampoFaltante` — el alta desde la SPA no genera
  deuda de datos (el bot no está en el medio para pedir lo que falta).
- **Edición (PUT):** admite filas incompletas (es justamente el flujo de
  corregir el pendiente abandonado); se validan COHERENCIA (peso/tiempo
  fuera del tipo del ejercicio → 400; peso sin ejercicio → 400) pero no
  completitud.
- **Normalización de valores:** misma de `ADR-0004` S6 vía
  `NormalizarValores` (ahora `internal static`): lb→kg con factor fijo,
  «50» suelto → kg, PesoCorporal → peso null; y la regla del chat al anotar:
  tiempo presente sin unidad → `Unidad.Tiempo` (etiqueta de tipo, no dato).

## S4 — Confirmación: snapshot write-once solo desde LISTA

**Decisión:** `POST /planillas/confirmar` con precondiciones: estado `Lista`
(409 si otro, con el detalle del estado actual), sin aclaración viva (409 — en
Lista nunca la hay; la defensa es determinística) y ≥ 1 fila (400 si vacía).
Congela `PlanillaConfirmada` + `PlanillaFila`:

- `Orden` = 1..N en orden de creación (Id asc).
- `EjercicioCanonical` = nombre canónico, o el texto crudo en filas sin
  ejercicio, o vacío (no se inventa identidad).
- `TiempoSeg` y unidades congeladas tal cual la planilla (100 % kg).
- `Incompleta = DescripcionPendiente || CampoFaltante(...) != null` — el
  profesor ve marcadas las filas incompletas (reglas §2: la información
  nunca se pierde en silencio).

La sesión pasa a `Confirmada` y deja de ser hablante (ADR-0004 S1): el
próximo mensaje del chat abre sesión nueva. La respuesta del endpoint es la
vista del snapshot (no de sesión) para que la SPA navegue a la vista de
planilla confirmada. Las demás mutaciones devuelven la `RespuestaSesionActual`
refrescada: una sola llamada deja la cuadrícula al día.

## S5 — Esquema: `PlanillaFila.TiempoSeg` + `Incompleta` (migración aditiva)

Migración `20261021184533_PlanillaFilaTiempoSegAndIncompleteFlag`: dos
`AddColumn` (`TiempoSeg int?` nullable; `Incompleta bool default false`),
sin operaciones destructivas; `Down()` es el `DropColumn` de las mismas.
El snapshot hasta ahora no podía representar ejercicios por tiempo (plancha)
ni la marca visible al profesor.

## S6 — Catálogo para la cuadrícula, dentro del árbol PlanillaApi

**Decisión:** `GET /api/planillas/catalogo-de-ejercicios` (solo lectura, sin
identidad de alumno): el selector de alta/edición necesita el catálogo
canónico con sus banderas (`UsaPeso`, `UsaTiempo`). Vive en el mismo grupo
porque existe como soporte de la cuadrícula del alumno. DTO de lectura propio
(id, nombreCanonico, grupoMuscular, usaPeso, usaTiempo) — sin acoplar el DTO
del NLU (que tiene apodos y sirve al resolver del chat).

## Consecuencias

- [SUPUESTO] los agregados manuales de la SPA no alteran `OrdenSerieSiguiente`
  del chat: es el contador del diálogo por WhatsApp, no de la cuadrícula.
- [SUPUESTO] confirmar exige ≥ 1 fila: una planilla confirmada vacía sería
  ruido para el profesor (y perderían sentido las marcas de incompletas).
- El CRUD usa el mismo criterio de normalización/completitud que el chat, así
  que la planilla que ve la SPA es EXACTAMENTE lo que el profesor contará
  después; si cambia una regla (factor lb→kg, campos por tipo), cambia una vez.
- La identificación sigue provisional por id de alumno (ruta/cuerpo) hasta
  AuthApi (S6); el contrato muta solo en el borde si eso cambia.
