# ADR 0050 — Envío de reportes desde el historial

- **Estado:** Aceptado, solicitado por el usuario.
- **Fecha:** 2026-11-22.

## Contexto

El envío por correo de ADR-0019 estaba disponible en la vista posterior a la
confirmación, pero el historial abría un selector de reportes sin esa acción.
Los entrenamientos guardados tenían PDF y Excel descargables, sin un acceso
visible al envío que la API ya soportaba.

## Decisión

- Las tarjetas del selector ofrecen «Enviar PDF por email» y «Enviar Excel por
  email». El detalle y la vista de impresión también permiten abrir el envío.
- Se muestra un formulario inline reutilizable (`ReportEmail`), sin otro modal.
  Identifica la planilla por fecha y número y permite elegir un destinatario y
  formato. El destinatario empieza vacío; nunca se envía sin acción explícita.
- El formulario mantiene estados propios de envío, éxito y error. Durante una
  petición bloquea nuevas solicitudes y cambios de destinatario/formato desde
  ese formulario. Un error conserva el destinatario para reintentar.
- Al cambiar de planilla se limpia el formulario y se desuscribe de la petición
  anterior, para que su respuesta no aparezca asociada a otro entrenamiento.
  Cerrar una vista no garantiza cancelar un correo ya aceptado por el servidor;
  no hay reintentos automáticos ni garantía de entrega exactamente una vez.
- Se reutiliza `POST /api/planillas/confirmadas/{id}/correo`, la verificación de
  propiedad y los generadores del snapshot. No se modifica la planilla histórica,
  la sesión en curso ni el esquema de la base de datos.
- El formato nulo se rechaza con 400, igual que cualquier formato no admitido.
  La ausencia de configuración de correo devuelve 503 y un rechazo devuelve 502. La SPA muestra
  mensajes de usuario y conserva las descargas como alternativa.

## Verificación

**Ampliación 2026-11-23 (ADR-0054):** el borrado lógico excluye la planilla de
nuevos envíos y descargas, incluso con su URL/ID anterior (404). No revoca
correos ya iniciados o entregados. El cambio de nombre conserva el adjunto.

- Pruebas de interfaz: accesos desde tarjetas, detalle e impresión; formato e ID
  correctos; dirección inválida; bloqueo durante el envío; errores y cambio de
  planilla; conservación del borrador y de la sesión al volver al historial.
- Pruebas de API con cookies y antiforgery reales y SMTP local sobre loopback:
  recepción MIME de PDF/Excel, destinatario y adjuntos comparados con la descarga
  del snapshot; parciales actuales intactos; SMTP ausente/rechazado; entradas
  inválidas y acceso a una planilla ajena.
- El receptor de prueba no envía mensajes externos y no certifica credenciales,
  TLS del proveedor, entrega en bandeja de entrada o carpeta de spam. Es necesaria
  una prueba real con un proveedor configurado antes de certificar el correo del
  despliegue.

ADR-0051 agrega entrega por HTTPS para hosts que bloquean SMTP, incluido Railway
gratuito, conservando el envío SMTP predeterminado para entornos compatibles.
