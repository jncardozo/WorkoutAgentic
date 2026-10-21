# ADR 0003 — Interpretación de capturas (transcripción, NLU y resolver)

- **Estado:** Aceptado (2026-10-21)
- **Contexto:** ADR-0002 (S1/S2), `docs/domain/nlu-captura.md`

## S1 — Descarga de notas de voz por Graph API

**Decisión:** `Features/WhatsappMedia` (`IWhatsappAudioDownloader`): dos llamadas —
`GET /{media_id}` (metadatos con la url temporal) y luego `GET url` con Bearer para
los bytes. La url nunca se persiste (convención WhatsApp); límite defensivo de
20 MB (los inline base64 a Gemini no deben pasarse; notas de voz reales < 2 MB).
Un medio 4xx (expirado) no se reintenta; 5xx/429/red → retriable.

## S2 — Salida JSON del LLM y separación de etapas

**Decisión:** una sola llamada a Gemini (`generateContent`, temperatura 0) produce
intención + entidades con `responseMimeType: application/json` y `responseSchema`
estricto (`Features/Nlu/NluSchema.cs`). `IIntentClassifier` y
`IEntityExtractor` comparten esa única salida vía adaptador scoped
(`GeminiNlu`): una interpretación JSON auditable por mensaje, sin doble llamada.
Los enums del schema usan los identificadores del spec (`CARGA_EJERCICIO`, `AYUDA`…).

**Campos extra sobre el spec §2:** `cantidad_series` ("3x12" → 3) y
`tiempo_segundos` ("1 minuto de plancha" → 60); faltantes son `null` — regla madre.

## S3 — Transcripción (STT)

**Decisión:** modelo `"gemini-2.5-flash"` por defecto para S1/S2 (configurable
`Gemini:ModeloTranscripcion/ModeloNlu`) — [SUPUESTO] la familia flash vigente cumple
los fixtures promedio; se recalibra si la calidad no sostiene. Prompt fijo de
transcripción literal (sin corregir ni interpretar); salida vacía/bloqueada →
fallo tipado → el flujo pide reenvío, nunca adivina.

## S4 — CatalogResolver (determinístico, offline)

**Decisión:** reglas fijas, en este orden:

1. **Nombre de grupo muscular**: tokens del query (sin stopwords) == tokens del
   grupo → un solo ejercicio → match; ≥ 2 → `Ambiguo` con sugerencias (spec NLU
   "el pecho"). Va ANTES del léxico para que "pecho" no matchee "press de pecho"
   por contención.
2. **Match léxico** (por frase = nombre canónico o apodo): exacto → 1.0;
   contención (ambos ≥ 4 chars) → 0.95; Levenshtein ratio. `Matcheado` solo si
   hay UN único candidato ≥ umbral **0.85** [SUPUESTO] — [SUPUESTO] se calibra
   con fixtures reales; ≥ 2 → `Ambiguo` (top-3 por score).
3. Si nada cae: sugerencias top-3 solo con score > 0.55 (lejanías no sugieren).

Scoring case-insensitive, sin diacríticos y sin signos; apodos del catálogo son
la vía principal ("sillón" → Sillón de cuadríceps). El test de fixtures vive en
`CatalogoEjercicioResolverTests`.

## S5 — Catálogo mínimo del MVP

**Decisión:** seed idempotente en `DbInitializer` de 10 ejercicios con apodos
(Sillón, Prensa, Press de pecho, Cruces, Remo, Curlo, Militar, Tríceps en polea,
Dominadas, Plancha): cubre los fixtures del spec y ≥ 2 ejercicios por grupo
"pecho"/"pierna" para probar la ambigüedad. [SUPUESTO] el profesor edita/expande el
catálogo desde la SPA en fases siguientes; el seed no repiembra.

## S6 — Conexión del pipeline

**Decisión:** `CapturaWorker` abre un scope por mensaje y delega en
`ICapturaPipeline` (contrato del slice webhook, implementado por
`Features/Nlu`); sin pipelines registrados, el log mantiene el comportamiento
previo (los tests de integración del webhook no dependen del NLU). La salida de
este slice es la línea de log auditable `Captura interpretada: …` con el texto
crudo incluido (reglas de negocio §4).

**Fallos de etapa** (descarga, STT, LLM) → log con título de error y mensaje
descartado para el pipeline actual; las respuestas pedidas por el spec se
construyen en el slice de sesión con `IWhatsappSender`.

## Consecuencias

- La calidad de la interpretación depende del prompt + schema: los casos reales
  nuevos se suman como fixtures (`/nlu-case`).
- El memo del adaptador por-scope simplifica el ratio 1 llamada/mensaje; si se
  parte en dos etapas con llamadas separadas, se duplica costo/latencia.
- Levenshtein en software  (sin dependencias) es O(n·m) por mensaje con catálogo
  chico: suficiente para el MVP.
