# Arquitectura — Voice Tracker MVP

> Estado: v1 (2026-10-21) — ADR-0002 **aceptado**; los supuestos marcados
> **[S1..S8]** se confirman al implementar (no cambian el diseño).
> Reglas de negocio de referencia: `docs/domain/business-rules.md`.
> Spec de interpretación: `docs/domain/nlu-captura.md`.

## 1. Visión general

```
┌────────────────────────┐        webhook POST (text|audio)
│  WhatsApp Cloud API    │──────────────────────────────────────┐
└────────────────────────┘                                      │
                                                                ▼
                                              ┌─────────────────────────────┐
                                              │  WhatsAppWebhookEndpoint    │
                                              │  · valida X-Hub-Signature   │
                                              │  · encola mensaje           │
                                              │  · responde 200 YA          │
                                              └──────────────┬──────────────┘
                                                             ▼
                                              ┌─────────────────────────────┐
        ┌── audio ─▶ MediaDownloader ─▶ STT ─▶│  CapturaWorker              │
        │                        (español)    │  (BackgroundService)        │
        └── texto ───────────────────────────▶│                             │
                                              └──────────────┬──────────────┘
                                                             ▼
                                    ┌────────────────────────────────────────┐
                                    │  NluPipeline (etapas separadas)        │
                                    │  1 IIntentClassifier  → intent         │
                                    │  2 IEntityExtractor   → entidades      │
                                    │  3 CatalogResolver    → ejercicioId|?  │
                                    │  4 SesionCapturaService (state machine)│
                                    └───────┬───────────────────┬────────────┘
                                            │ ok                │ ambiguo/faltante
                                     RegistroParcial    AclaracionPendiente
                                            └───────────┬───────┘
                                                        ▼
                                        ┌───────────────────────────────┐
                                        │  WhatsappSender (Graph API)   │
                                        │  respuesta/aclaración al alumno│
                                        └───────────────────────────────┘

┌────────────────────────┐    HTTPS + cookies
│  SPA Angular 21        │◀──────────────────────────▶ API REST propia
│  · Alumno: planilla    │        (src/api, wwwroot)   · /api/capturas/*
│    editable + confirmar│                             · /api/planillas/*
│  · Profesor: solo      │                             · /api/profesor/*  (RBAC)
│    lectura             │
└────────────────────────┘
```

Un solo despliegue para el MVP: **API .NET + SPA servida desde `wwwroot`** + SQLite
siguiendo el contenedor Docker.

## 2. Componentes del backend (`src/api`)

| Componente | Responsabilidad | Notas |
|---|---|---|
| `WhatsAppWebhook` | GET verify + POST de eventos; validación HMAC; encola y devuelve 200 | Sin lógica de negocio |
| `CapturaWorker` | `BackgroundService` que consume la cola in-process | Desacopla latencia 200↔procesamiento |
| `MediaDownloader` | `GET /{MEDIA_ID}` de Graph, guardado temporal | audio/ogg opus ≤ 2 min |
| `ISpeechToText` | audio → texto es-419 | Implementación: Gemini API (ADR-0002) |
| `IIntentClassifier` | texto → `CARGA_EJERCICIO / CARGA_SERIE / CIERRE / AYUDA / INVALIDO` + confianza | LLM (Gemini) con JSON Schema |
| `IEntityExtractor` | texto → ejercicio(crudo), serie, reps, peso, unidad | JSON estructurado; NUNCA inventa valores |
| `CatalogResolver` | texto de ejercicio crudo → `EjercicioId` (fuzzy contra catálogo) | Determinístico y auditable; sin match → sugerencias |
| `SesionCapturaService` | máquina de estados del diálogo; aplica entidades; crea parciales o aclaraciones | Persistido en BD (S3) |
| `WhatsappSender` | envío libre dentro de ventana 24 h / plantilla después | Cola de reintentos con backoff |
| `PlanillaApi` | CRUD de parciales + confirmación (snapshot inmutable) | Solo dueño (alumno) |
| `ProfesorApi` | listado de alumnos vinculados + planillas confirmadas | Solo lectura (RBAC) |
| `AuthApi` | login SPA (cookie) + roles + invitaciones profesor→alumno | S6 |

## 3. Máquina de estados de `SesionCaptura`

```
              ┌──────────── mensaje (text|audio procesado) ────────────┐
              ▼                                                        │
 (vacío) ──▶ EN_CURSO ──entidad faltante/ambigua──▶ ESPERANDO_ACLARACION ┤
                │                                        │ respuesta OK │
                │                                        │ 2 reintentos │
                │                                        ▼              │
                │ cierre ("listo")              fila con pendiente=true │
                ▼                                                        │
              LISTA ──[alumno confirma en SPA]──▶ CONFIRMADA (snapshot
                └─[2 h sin actividad]──▶ EXPIRADA ──"seguir"──▶ EN_CURSO   │
                                                                          │
                 CONFIRMADA ──▶ FIN (parciales bloqueados) ◀───────────────┘
```

- El estado vive en la BD: se puede atender desde cualquier instancia o reinicio.
- Concurrency (S3): campo `RowVersion` evita doble aplicación de mensajes casi
  simultáneos del mismo alumno.

## 4. Modelo de datos (EF Core, SQLite MVP)

| Tabla | Campos clave |
|---|---|
| `Profesor` | Id, Nombre, Email (login), Rol |
| `Alumno` | Id, ProfesorId?, TelefonoWa (único), Email? (login SPA), Estado |
| `Invitacion` | Codigo (8 chars), ProfesorId, AlumnoId?, ExpiraUtc, UsadaUtc? |
| `Ejercicio` | Id, NombreCanonico, Apodos (JSON), UsaPeso, UsaTiempo, GrupoMuscular |
| `SesionCaptura` | Id, AlumnoId, Estado, EjercicioActivoId?, OrdenSerieSiguiente, UltimaActividadUtc, RowVersion |
| `RegistroParcial` | Id, SesionId, EjercicioId?, EjercicioCrudo?, DescripcionPendiente, NroSerie, Reps?, PesoKg?, Unidad, PendienteAclaracion, CreadoUtc |
| `AclaracionPendiente` | SesionId (1:1), Campo, Reintento, MensajeBotWaId, ExpiraUtc |
| `Transcripcion` | Id, SesionId, MediaId, TextoCrudo, Proveedor, DuracionSeg, RecibidoUtc |
| `PlanillaConfirmada` | Id, SesionId, AlumnoId, ConfirmadaUtc |
| `PlanillaFila` | PlanillaId, Orden, EjercicioCanonical, NroSerie, Reps, PesoKg, Unidad (snapshot inmutable) |

Regla clave: `RegistroParcial` y `PlanillaFila` son **tablas distintas** — el parcial
es editable/borrable; la confirmada es un snapshot write-once.

## 5. Flujos principales

**Carga por voz**
```
nota de voz → webhook → cola → descarga medio → STT es-419 (Gemini)
→ NluPipeline → "sillón de cuadríceps, primera serie, 12 reps con 45 kg"
→ CatalogResolver: sillo→ Sillón de cuadríceps ✔
→ SesionCapturaService: crea parcial (serie 1, 12 reps, 45 kg)
→ respuesta: "✅ Sillón de cuadríceps · serie 1 · 12 × 45 kg. ¿Cuál sigue?"
→ resolución: "¿con qué peso tomaste la serie 2?" si falta peso.
```

**Cierre y confirmación**
```
"terminé" → LISTA → WhatsApp: link/aviso de revisar la planilla en la SPA
SPA Alumno: cuadrícula editable (agregar/editar/eliminar filas) → Confirmar
→ POST /api/planillas/confirmar → snapshot → CONFIRMADA
→ Profesor ve la planilla en "Mis alumnos" (solo lectura).
```

## 6. SPA Angular 21 (`src/web`)

| Vista | De | Contenido |
|---|---|---|
| Login | ambos | Email + contraseña; cookie `HttpOnly` |
| Alumno · Planilla | ALUMNO | Cuadrícula editable del estado `LISTA`: filas con editar/eliminar y agregar; botón Confirmar; estado con signals |
| Alumno · Historial | ALUMNO | Planillas confirmadas propias (lectura) |
| Profesor · Alumnos | PROFESOR | Lista de vinculados + última planilla confirmada; generar invitación |
| Profesor · Planilla | PROFESOR | Cuadrícula del snapshot, readonly |

- Componentes standalone + signals, Vitest (convención `angular21.md`).
- Densidad dark del prototipo Stitch: fondo `#0f131c`, primario `#9ee939`,
  Space Grotesk/Geist/JetBrains Mono → tokens en `styles.css`.
- Actualización: refresh de la planilla al enfocar la pestaña (sin SignalR en el MVP, S7).

## 7. Decisiones (resumen — justificación en ADR-0002)

| # | Decisión | Elección |
|---|---|---|
| S1 | Proveedor de voz (STT) | **Gemini API** (audio-in, es-419, JSON) |
| S2 | Motor de NLU | **LLM JSON Schema + resolver determinístico de catálogo** (híbrido) |
| S3 | Base de datos | **SQLite (WAL)** → migrar a Postgres/Npgsql al escalar |
| S4 | Bot de WhatsApp | **Sin framework**: Cloud API + pipeline propio en .NET |
| S5 | Cola interna | **`Channel<T>` in-process** (sin Redis/RabbitMQ) |
| S6 | Autenticación | **Cookie + roles**; login SPA por email; identidad WhatsApp por teléfono |
| S7 | Tiempo real SPA | **Polling al enfocar** (SignalR en Fase 2) |

## 8. Estrategia de testing

- **Fixtures NLU**: corpus de `nlu-captura.md` como xUnit parametrizados
  (frase → intent+entidades+aclaración esperada).
- **Máquina de estados**: tests de transiciones, expiración y concurrencia con
  `SesionCaptura` real.
- **Integración**: `WebApplicationFactory` con fakes de `ISpeechToText`/proveedor WA
  (no se llama a Meta/Gemini en tests).
- **SPA**: Vitest de componentes; Playwright E2E (MCP `playwright`) para el flujo de
  revisar/editar/confirmar.
- **Webhook**: test de firma válida/inválida y de ack-200-procesa-luego.

## 9. Riesgos y mitigaciones

| Riesgo | Mitigación |
|---|---|
| STT confunde números ("45" ↔ "cuarenta y cinco") | Normalizador de números + confirmar lectura cuando la confianza sea baja |
| Ejercicio por apodo no matchea | Catálogo con apodos curados + `CatalogResolver` fuzzy + sugerencias |
| Doble mensaje simultáneo del alumno | `RowVersion` + orden por timestamp de Meta |
| Costo/latencia de la IA | Audio corto (≤2 min), llamadas una sola vez por mensaje; proveedor detrás de interfaz para cambiar |
| Ventana 24 h vencida (alumno contesta tarde) | Ventana respetada; recordatorio con plantilla para reabrir sesión |

## 10. Fuera del MVP (hook para después)

- SignalR para planillas en vivo; PWA offline; progreso/estadísticas para el alumno;
  generador de rutinas con IA (Fase 2 de `business-rules.md`); STT local
  (faster-whisper en GPU del dev) para bajar costo cero.
