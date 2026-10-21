---
name: NLU Español
description: Pipeline de interpretación de mensajes del Voice Tracker (voz → transcripción → intención → entidades en español coloquial), con reglas de aclaración y corpus de frases
---

## Cuándo usar esta skill

- Implementar, extender o debuggear el pipeline de interpretación del Voice Tracker MVP.
- Sumar ejemplos coloquiales, variantes regionales o casos ambiguos al spec.
- Escribir tests del pipeline (fixtures de frases con entidades esperadas).

## Fuente de verdad

1. `docs/domain/nlu-captura.md` — spec completo: intents, entidades, normalización y
   corpus de frases de test.
2. `docs/domain/business-rules.md` — reglas de negocio del flujo de captura
   (la sección "Reglas del pipeline" manda sobre cualquier detalle local).

## Etapas (SIEMPRE separadas — jamás acoplarlas)

```
audio → [1] Transcripción (STT español) → texto
texto → [2] Clasificación de intención → intent + confianza
texto → [3] Extracción de entidades → ejercicio/serie/repeticiones/peso/unidad (normalizados)
(intent+entidades) → [4] Estado de sesión → RegistroParcial o aclaración
```

## Reglas del pipeline

- Si una etapa falla o devuelve baja confianza → **Estado de aclaración**: pedir el
  dato faltante en español neutro (ej. "¿A qué peso tomaste esa serie?"). PROHIBIDO
  inventar o inferir silenciosamente un valor.
- "Ejercicio no reconocido" → proponer 2-3 sugerencias del catálogo próximas y pedir
  confirmación; si no hay match ≥ umbral, guardar como texto crudo con flag
  `R.descripcionPendiente` (nada más).
- Normalizaciones obligatorias documentadas en el spec: números dictados a palabras
  ("kilos y medio" = 1.5 kg), apodos de ejercicios ("sillón" → Sillón de cuadríceps),
  unidades (kg por defecto, minutos/segundos para descansos/planchas).
- El estado de sesión (ejercicio activo, última serie cargada) vive SOLO en
  `SesionCaptura` persistida — nunca en memoria de proceso (multi-instancia).

## Checklist antes de dar por terminada una tarea de NLU

- [ ] Cada etapa tiene su interfaz propia (`ISpeechToText`, `IIntentClassifier`,
      `IEntityExtractor`, `ISesionCapturaRepository`).
- [ ] Errores de cada etapa → tipo específico de dominio (no excepción cruda al webhook).
- [ ] Casos ambiguos del spec cubiertos por tests (fixtures en español coloquial).
- [ ] La respuesta al usuario pedida por el spec no filtra internals ni IDs técnicos.
