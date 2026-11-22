# ADR 0051 — Correo por HTTPS para Railway gratuito

- **Estado:** Aceptado como preparación del despliegue solicitado.
- **Fecha:** 2026-11-22.

## Contexto

La verificación de ADR-0050 confirmó el envío SMTP local, pero Railway bloquea
SMTP en Free, Trial y Hobby. Configurar solo `CorreoSmtp` no permite enviar
planillas ni recuperar contraseñas en ese despliegue.

## Decisión

- Centralizar la entrega de planillas y recuperación de contraseña en
  `EmailDeliverySender`, conservando SMTP como proveedor predeterminado.
- Agregar la opción explícita `EmailDelivery:Provider=brevo`, que realiza un
  POST HTTPS a la API transaccional oficial de Brevo con el adjunto en base64.
  No requiere agregar dependencias, cambiar la base ni contratar un plan pago.
- Configurar `EmailDelivery:ApiKey`, `FromAddress` y `FromName` fuera de Git. El
  operador debe crear/activar su cuenta y verificar el remitente según los
  requisitos del proveedor antes de enviar mensajes reales. Esta implementación
  no crea cuentas, verifica remitentes ni activa el servicio automáticamente.
- Proveedor desconocido o configuración incompleta: 503. Rechazo o fallo de
  red: 502. Sin cambio automático a SMTP ni reintentos que dupliquen mensajes.
  Límite de espera de 30 segundos para la entrega al proveedor.
- El cliente HTTPS no sigue redirecciones. No registrar cuerpos, claves,
  direcciones ni enlaces de recuperación en errores de transporte.
- Una respuesta satisfactoria confirma aceptación por el proveedor, no entrega
  en la bandeja de entrada. No se agregan webhooks ni seguimiento de apertura.

## Verificación

- Receptor SMTP real sobre loopback para el flujo completo API y ambos adjuntos.
- Contratos HTTP con proveedor sustituido en el límite de red: URL, cabecera de
  autenticación, destinatario, adjunto base64, errores de credenciales/cuota/red,
  tiempo de espera y recuperación de contraseña sin adjunto.
- Antes del despliegue, verificar con credenciales reales el remitente autorizado,
  conectividad HTTPS, recepción en una casilla consentida y apertura de PDF/Excel.

## Referencias

- [Restricciones de salida de Railway](https://docs.railway.com/networking/outbound-networking).
- [API de correo transaccional de Brevo](https://developers.brevo.com/reference/send-transac-email).
- [Límites del plan gratuito de Brevo](https://help.brevo.com/hc/en-us/articles/208580669-FAQs-What-are-the-limits-of-the-Free-plan).
