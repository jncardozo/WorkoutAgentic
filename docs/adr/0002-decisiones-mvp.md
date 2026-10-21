# ADR 0002 — Decisiones técnicas del MVP (Voice Tracker)

- **Estado:** Aceptado (2026-10-21) · **Fecha:** 2026-10-21
- **Contexto extendido:** `docs/arquitectura.md`, `docs/domain/nlu-captura.md`

## S1 — Proveedor de transcripción de voz (STT) en español

**Decisión:** Gemini API (endpoint `generateContent` con blobs de audio; modelo
flash de la familia actual) como `ISpeechToText`.

| Criterio | Gemini API | Whisper API (OpenAI) | faster-whisper (local) | Google Cloud STT |
|---|---|---|---|---|
| Acepta audio directo | ✅ | ✅ | ✅ | ✅ (config es-419 extra) |
| Calidad en es-419 coloquial | Alta | Alta | Buena (config de modelo) | Alta |
| Setup | 1 API key que ya poseemos | API key + SDK | requiere host con GPU 16–24 GB | proyecto GCP + credenciales IAM |
| Costo MVP | Tier gratuito suficiente para poda pruebas | ~USD 0.006/min | $0 (en hardware propio) | variable |
| Bonus | El MISMO proveedor hace el NLU (S2) → 1 integración | solo STT | solo STT | solo STT |

**Por qué:** los audios son notas de voz cortas (≤2 min), el volumen del MVP es bajo
y la misma cuenta de Google ya usada para Stitch mantiene la rotación de credenciales.
La interfaz `ISpeechToText` deja cambiar de proveedor sin tocar el pipeline.

**Supuestos:** confirmar límites refresh/cota gratuita vigentes al implementar
(verificar con Context7 en el momento); si el tier gratuito no alcanza para el
día a día, se pasa a plan pago o a `faster-whisper` local para dev.

## S2 — Motor de interpretación (NLU)

**Decisión:** híbrido, en dos etapas separadas por interfaz:

1. `IIntentClassifier` + `IEntityExtractor` → **LLM con response JSON Schema**
   (mismo proveedor de S1, modelo flash). El prompt exige: NO inferir valores
   faltantes; devolver `null` + campo `aclarar` cuando el dato no está claro
   (alineado con la regla madre: no inventar).
2. `CatalogResolver` → **determinístico local**: levenshtein/token-set contra
   `Ejercicio.NombreCanonico + Apodos`. El LLM NUNCA devuelve un `EjercicioId`
   directo; devuelve texto crudo y el resolver decide. Sin match → sugerencias
   (los 2–3 más cercanos) → aclaración.

**Por qué:** un LLM solo alucina nombres de ejercicios; un pipeline puro de regex no
sostiene la variación coloquial del español ("metele", "y medio", "3x12"). El híbrido
es auditable: cada mensaje queda registrado con su transcripción, salida JSON y
decisión del resolver.

**Alternativas descartadas:** regex/gramática pura (frágil con habla natural);
agente LLM autónomo con memoria de conversación intrínseca (menos auditable y más
caro por llamada; la memoria vive en `SesionCaptura`, no en el modelo).

## S3 — Persistencia

**Decisión:** **SQLite (modo WAL)** para el MVP con EF Core; migración a **Postgres
(Npgsql)** cuando existan usuarios reales (el código y migraciones se escriben para
ser portables; evitando funciones específicas de SQLite en el razonamiento).

**Por qué:** un solo proceso/despliegue (contenedor único, ver S8), cero operación de
servidor de BD en MVP, backup = copiar un archivo. El volumen de escritos del MVP
(relaciones tiempo-real por alumnos) es compatible con SQLite WAL.

**Migración/orquestación:** decisiones de estado en `SesionCaptura` con `RowVersion`.
La concurrencia de mensajes casi simultáneos del mismo alumno se serializa por
`AlumnoId` en el worker (cola por-partición sobre `Channel<T>`).

## S4 — "Bot" de WhatsApp

**Decisión:** **sin framework de bot**. El bot es: WhatsApp Cloud API (webhook +
Graph API) + nuestro pipeline .NET. Con referencia directa a la Graph API vía
`HttpClient` tipado (URLs versionadas por `WHATSAPP_API_VERSION`), DTOs propios y
validación HMAC con delegating handler compartido.

**Por qué:** el Bot Framework SDK de Microsoft está orientado a Teams u otros canales
y agrega abstracción que en Cloud API no aporta; no hay SDK oficial de Meta para .NET
sostenido para Cloud API conversacional. Con una sola intención de entrada/salida y
3-4 endpoints, un cliente propio mínimo es más auditable y libre de dependencias.

## S5 — Cola interna / separación 200-lógica

**Decisión:** cola **in-process con `Channel<T>`** en el host .NET
(`CapturaWorker` = `BackgroundService`). Sin Redis/Rabbit en MVP.

**Por qué:** un solo despliegue (S8); la cola solo suaviza picos y evita bloquear
el ACK. Si el MVP escala a multi-instancia, el mismo contrato se mueve a una cola
externa (ADR futuro).

## S6 — Autenticación y roles

**Decisión:** autenticación por **cookie de sesión (HttpOnly)** para la SPA (mismo
origen, servida por la API), con roles en claims (`ALUMNO`/`PROFESOR`). Login SPA:
email + contraseña (usuarios creados por invitación del profesor; el alumno también
existe con su `TelefonoWa` canónico para WhatsApp).

**Vinculación alumno↔profesor:** el profesor genera una invitación (código corto,
4 dígitos/verificación, expiración 7 días); el alumno lo ingresa en la SPA y queda
vinculada (`AlumnoId.ProfesorId`). El teléfono de WhatsApp se registra en el mismo
alta (o pidiendo el número en el primer mensaje por WhatsApp al crear/seguir
el vínculo).

**Alternativas descartadas en MVP:** OAuth externo (Google) (Fase 2), OTP por
WhatsApp para login (Fase 2), JWT (innecesario sin multipart/BFF).

## S7 — Tiempo real SPA

**Decisión:** refresco al enfocar la pestaña + botón refrescar. SignalR queda para
Fase 2 (avisos en vivo al profesor).

## S8 — Despliegue MVP

**Decisión:** un contenedor Docker (API + `wwwroot` de Angular build) + volumen para
SQLite; desplegable en cualquier VPS/Railway/Fly. Secrets por variables de entorno.
**[Supuesto:** canónico del dueño — se decide el hosting aparte**]**

## Consecuencias

- Un solo proveedor IA (Google) reduce integración, pero acopla S1+S2 a un vendor;
  mitiga por las interfaces + Adapter tests (swap rápido si cambia el costo/fiabilidad).
- SQLite obliga a cuidado de concurrencia de escritos en picos; mitigado por
  queue-by-partition + WAL.
- Sin framework de bot → más código propio único (HMAC, reintentos, plantillas);
  mitigado por las convenciones ya documentadas (`whatsapp.md`) y su skill.
