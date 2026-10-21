---
name: WhatsApp Integration
description: Configurar y extender la integración con WhatsApp Cloud API de Meta — verificación de webhook, recepción de mensajes, envío (texto, media, plantillas), firma y tokens
---

## Cuándo usar esta skill

- Exponer o modificar el webhook de WhatsApp en la API .NET.
- Procesar **notas de voz** (mensajes de tipo `audio`) y descargar su archivo.
- Enviar mensajes salientes (texto, media, plantillas) desde el backend.
- Configurar o rotar credenciales, o diagnosticar por qué no llegan eventos.

## Flujo de trabajo

1. Lee `docs/conventions/whatsapp.md` — contiene el contrato completo de payloads,
   límites y el checklist de verificación de firma.
2. Variables requeridas (leerlas de configuración, JAMÁS hardcodearlas):
   - `WHATSAPP_ACCESS_TOKEN`, `WHATSAPP_VERIFY_TOKEN`, `WHATSAPP_APP_SECRET`
   - `WHATSAPP_PHONE_NUMBER_ID`, `WHATSAPP_BUSINESS_ACCOUNT_ID`, `WHATSAPP_API_VERSION`
3. **Verificación del webhook (GET)**: responder `hub.challenge` (texto plano, 200) solo
   si `hub.verify_token` coincide con el valor configurado.
4. **Eventos (POST)**: validar la firma `X-Hub-Signature-256` (HMAC-SHA256 del body
   con `WHATSAPP_APP_SECRET`) ANTES de deserializar o procesar. Responder 200 rápido;
   el procesamiento pesado va en background — Meta reintenta con backoff si no respondemos.
5. **Envío saliente**: `POST https://graph.facebook.com/{WHATSAPP_API_VERSION}/{WHATSAPP_PHONE_NUMBER_ID}/messages`.
   Regla de la ventana de 24 h: mensajes libres solo dentro de las 24 h desde el último
   mensaje del usuario; fuera de la ventana, únicamente plantillas aprobadas por Meta.
6. **Notas de voz (type `audio`)**: el evento trae `audio.id` + `mime_type: audio/ogg; codecs=opus`.
   Para obtener el archivo real:
   `GET https://graph.facebook.com/{WHATSAPP_API_VERSION}/{MEDIA_ID}` con
   `Authorization: Bearer {token}` (devuelve binario). La URL de `audio.link` es temporal;
   NO la persistir: descargar, encolar y borrar/transcribir. Paso inmediato:
   `speech-to-text` en español (ver skill `nlu-espanol`). Un audio de más de ~2 min
   (límite de nota de voz) puede alargar la espera: responder 200 y avisar
   "🎧 Procesando tu audio..." si demora.
7. Errores de Graph API vienen con `error.code` y `error.message`: mapearlos a un
   problema de dominio propio, nunca filtrar el token en logs.
7. En local, usar ngrok (o similar) para exponer el webhook y el número de prueba de Meta.

## Checklist antes de dar por terminada una tarea de mensajería

- [ ] La firma se valida en TODOS los handlers POST.
- [ ] Ningún log imprime el access token ni el app secret.
- [ ] El webhook responde 200 aunque el procesamiento interno falle (se registra error).
- [ ] Los mensajes salientes respetan la ventana de 24 h o usan plantilla.
