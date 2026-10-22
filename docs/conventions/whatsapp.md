# Convenciones WhatsApp Cloud API

> **DEPRECADAS (ADR-0009, 2026-10-21):** el canal de WhatsApp quedó
> deshabilitado por el pivot a captura por texto en la PWA. Estas convenciones
> se conservan como referencia del código que sigue compilando (webhook,
> firma, envíos, media) y para el caso de revivir el canal con la bandera
> `Whatsapp:WebhookHabilitado`. La captura activa hoy es voice-tracker por
> texto: `POST /api/capturas/{alumnoId}/texto` (ADR-0009).

> Fuente de verdad para toda la mensajería. Skills relacionadas: `whatsapp-integration`,
> `nlu-espanol`.

## Topología (Voice Tracker)

```
Meta (Cloud API) ──POST webhook──▶ API .NET (src/api)
                                     ├─ mensaje `text`  → pipeline NLU directo
                                     └─ mensaje `audio` → descarga medio → STT → pipeline NLU
API .NET ──POST /messages──▶ Graph API ──▶ alumno (respuestas/aclaraciones)
SPA (src/web) ◀── APIs propias ──▶ API .NET (planillas parciales/confirmadas)
```

## Credenciales (todas vía user-secrets / variables de entorno)

| Variable | Uso |
|---|---|
| `WHATSAPP_ACCESS_TOKEN` | Bearer token para llamar a Graph API |
| `WHATSAPP_VERIFY_TOKEN` | Valor arbitrario para el handshake del webhook |
| `WHATSAPP_APP_SECRET` | Clave HMAC para validar `X-Hub-Signature-256` |
| `WHATSAPP_PHONE_NUMBER_ID` | Número emisor en la llamada a Graph API |
| `WHATSAPP_BUSINESS_ACCOUNT_ID` | Cuenta WABA (plantillas, calidad) |
| `WHATSAPP_API_VERSION` | ej. `v23.0` |

## Webhook entrante

1. **Handshake (GET)**: Meta envía `hub.mode=subscribe`, `hub.verify_token`,
   `hub.challenge`. Si el token coincide, responder 200 con `hub.challenge` como
   texto plano (sin JSON).
2. **Eventos (POST)**: JSON con `entry[].changes[].value.messages[]` (y
   `statuses[]` para actualizaciones de entrega).
3. **Firma OBLIGATORIA**: header `X-Hub-Signature-256: sha256=<hex>`; HMAC-SHA256 del
   body crudo (bytes, sin re-serializar) con `WHATSAPP_APP_SECRET`, comparación
   timing-safe. Si no coincide → 401 y NO procesar.
4. **Responder 200 inmediatamente** y procesar en background. Meta reintenta
   (1 min, 5 min, 10 min… hasta 24 h) si no respondemos.
5. **Ruteo por tipo (Voice Tracker)**: los mensajes relevantes son
   `messages[].type` = `text` (carga por texto → pipeline NLU directo) y `audio`
   (nota de voz → descargar medio → STT → pipeline NLU). Otros tipos (`image`,
   `location`, `contacts`…): descartados para el estado de sesión, con respuesta de
   cortesía pidiendo texto o nota de voz.

## Notas de voz (type `audio`)

- El evento trae `audio.id`, `mime_type: audio/ogg; codecs=opus`, `duration`, `sha256`.
- `audio.link` es temporal: **NO persistirla**. Descargar el medio apenas llega el
  webhook (ver "Descarga de medios") y encolar el procesamiento.
- Persistir solo: media_id, duración, tamaño y el resultado de la transcripción.
- Duraciones: nota de voz típica ≤ 2 min. Si la descarga/STT se demora más de unos
  segundos, responder al alumno "🎧 Procesando tu audio..." y continuar en background.
- Audio no descargable o expirado → pedir al alumno que reenvíe la nota de voz.

### Descarga de medios

```
GET https://graph.facebook.com/{WHATSAPP_API_VERSION}/{MEDIA_ID}
Authorization: Bearer {WHATSAPP_ACCESS_TOKEN}
```
Respuesta: binario (audio/ogg opus). Sin `Accept` especial; límites de tamaño y
caducidad según la doc de Graph Media API.

## Mensajes salientes

- Endpoint: `POST https://graph.facebook.com/{WHATSAPP_API_VERSION}/{WHATSAPP_PHONE_NUMBER_ID}/messages`
- Header: `Authorization: Bearer {WHATSAPP_ACCESS_TOKEN}`.
- Body mínimo texto:
  ```json
  { "messaging_product": "whatsapp", "to": "<telefono>", "type": "text",
    "text": { "body": "..." } }
  ```
- Teléfonos en formato internacional SIN `+`, `(`, `)` ni `-`: `5215512345678`.
- Si respondemos a un mensaje entrante, incluir `context.message_id` del recibido.

## Ventana de 24 horas

- Dentro de 24 h del último mensaje del usuario: texto/media libre sin restricciones.
- Fuera de la ventana: SOLO plantillas (`type: "template"`) aprobadas por Meta.
- El flujo de captura decide: si `last_user_message_at` + 24 h < now → plantilla.

## Límites y errores de Graph API

- Errores: `error.code` (ej. 131026 no pagado, 131047 rate limit, 131051 error general),
  `error.message`, `error.error_data.details`.
- Rate limit: respetar `X-Business-Use-Case-Usage` o manejar 429 con backoff.
- Logs: jamás imprimir el access token ni el body completo si contiene datos de usuarios.

## Desarrollo local

- Número de prueba de Meta (agregar destinatarios allowlist) + ngrok para el webhook.
- Configurar la URL del webhook y el verify token en Meta App Dashboard → WhatsApp → Configuration.
