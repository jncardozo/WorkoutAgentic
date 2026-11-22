# ADR 0052 — Acceso único y estado de configuración del correo

- **Estado:** Aceptado, solicitado por el usuario.
- **Fecha:** 2026-11-22.

## Contexto

La revisión móvil de ADR-0050 mostró demasiadas acciones en las tarjetas de
PDF/Excel y botones visualmente pegados al número de series. El usuario solicita
un único acceso al envío por email, centrado y fácil de encontrar.

El aviso de envío no disponible corresponde a configuración incompleta (503).
Se verificó la ausencia de proveedor, API key y remitente de Brevo, así como de
host y remitente SMTP en la configuración local consultada. El usuario confirmó
que todavía no tiene cuenta de Brevo. No hay evidencia de un correo rechazado
por el proveedor: la operación se detiene antes de contactar un servidor.

## Decisión

- Sustituir los botones de correo de cada tarjeta por un único acceso centrado
  debajo de la introducción y antes de las tarjetas. Cada tarjeta conserva su
  acción para consultar el reporte, separada del contador de series.
- `ReportEmail` encapsula el acceso y el formulario. También se reutiliza como
  único acceso en el detalle, impresión y pantalla confirmada. El formato se
  elige dentro del formulario; en el detalle se propone el formato consultado.
- Abrir comprueba `GET /api/planillas/correo/estado`, protegido por autenticación
  y acuerdo vigente. Solo devuelve `{configured: boolean}`, sin credenciales,
  nombres de proveedor, dominios ni datos personales. No contacta proveedores.
- La validación de configuración se comparte con el envío real, evitando
  divergencias entre la consulta y el POST. Los logs de operador indican la
  clave faltante sin registrar su valor, destinatarios ni documentos.
- Mientras se comprueba, o falta configuración, no se habilita el formulario.
  Se distingue un problema de conexión de la ausencia de configuración. Ambos
  permiten comprobar de nuevo. Un 503 posterior vuelve a bloquear el formulario.
- `configured=true` solo confirma requisitos locales de configuración, no
  autorización del remitente, vigencia de la clave o entrega en bandeja de entrada.
  Se mantienen los errores de entrega y las pruebas SMTP/HTTPS de ADR-0051.
- No se crean cuentas externas, se inventan credenciales ni se simulan envíos
  satisfactorios. La habilitación real necesita un proveedor autorizado por el
  operador y una prueba de recepción de ambos adjuntos.

## Verificación

- Botón único antes de las tarjetas, elección de formato, cierre y foco.
- Configuración ausente, comprobación pendiente, errores de conexión, reintento
  y protección contra envíos antes de configurar.
- Endpoint sin secretos, autenticación y aceptación del acuerdo obligatorias.
- Se conservan las regresiones del envío de PDF/Excel y la sesión de captura.
