# ADR 0049 — Aceptación obligatoria del acuerdo antes de la planilla

- **Estado:** Aceptado, solicitado por el usuario.
- **Fecha:** 2026-11-19.

## Contexto

El usuario solicita mostrar el acuerdo en la app y exigir aceptación antes de
continuar. Proporciona el nombre, domicilio y correo del responsable. El
documento bilateral del ADR-0048 tenía campos pendientes y una modalidad de
firma externa; no corresponde mostrarlo como contrato completado.

## Decisión

- Se crea [un texto específico para aceptación en la SPA](../legal/condiciones-prueba-app-v1.txt),
  adaptado del borrador anterior, sin anexo ni firmas manuscritas pendientes.
  Establece condiciones para esta prueba del MVP y conserva las excepciones a la
  reserva, el desarrollo independiente y la ausencia de cesión general de derechos.
  El responsable se identifica con los datos suministrados por el usuario, en
  configuración `Legal`. No se inventan DNI, sociedad, ciudad ni fuero pactado.
- El texto se incorpora como recurso del backend para que no dependa del directorio
  de trabajo ni de un archivo accesible en el servidor al publicar. La SPA obtiene
  texto, versión y SHA-256 desde `GET /api/legal/agreement`, que permite consulta
  sin iniciar sesión. El navegador lo muestra como texto, sin interpretar HTML.
- `/legal/confidencialidad` ofrece consulta previa desde login/registro.
  `/acuerdo-qa` exige una cuenta autenticada, permite leer todo el texto y
  descargarlo, y ofrece una casilla sin marcar y «Aceptar y continuar».
  «No acepto, salir» cierra sesión. No se obliga a desplazar el texto para simular
  que se leyó ni se registra aceptación por el solo hecho de abrir la pantalla.
- Tras registro o login, y también al entrar directamente con una cuenta existente,
  el guard de la planilla comprueba `/api/auth/me`. Sin aceptación vigente redirige
  al acuerdo. `agreementAccepted` se calcula en la API; no se confía en storage
  local, IDs de cuentas suministrados por el cliente ni cookies con un consentimiento
  desactualizado.
- `POST /api/legal/accept` exige autenticación, cuenta activa, antiforgery, aceptación
  explícita y coincidencia de versión y huella. La API obtiene la cuenta de la
  sesión. La migración nueva `AddAgreementAcceptances` crea registros con usuario,
  nombre y correo al aceptar, fecha UTC del servidor y copia exacta del documento.
  Una restricción única por usuario y huella hace idempotente la aceptación.
- El filtro de acceso de alumno exige aceptación vigente en **todas** las rutas
  de captura y planillas, incluidas historial, edición, eliminación, confirmación,
  exportación y envío por correo. Sin ella devuelve 403 con código
  `agreement_required`; la SPA redirige al acuerdo también ante esa respuesta.
  Identificación, consulta del acuerdo, aceptación y logout siguen disponibles.
- Una variación en el texto renderizado, incluidos los datos del responsable,
  cambia su huella y requiere aceptación de nuevo. Para modificaciones editoriales
  o legales se debe incrementar además la versión en `AgreementDocument`, que
  completa también el encabezado del texto. Las aceptaciones históricas no se
  actualizan ni eliminan.
- «Mi cuenta → Acuerdo de confidencialidad» permite volver a consultar y
  descargar la constancia. `GET /api/legal/receipt` entrega únicamente la última
  aceptación del usuario autenticado, con su texto exacto, fecha y huella, incluso
  si el documento vigente cambió. No admite elegir otro usuario por parámetros.
- Si falta un dato obligatorio de `Legal`, se permite consultar el documento
  informando que no está disponible para aceptar. La API rechaza la aceptación
  con 503 y mantiene el bloqueo de entrenamientos.

## Límites

La aceptación se vincula a una cuenta autenticada y no se presenta como firma
digital certificada. El autorregistro de ADR-0038 continúa abierto y sin verificar
correo: esta funcionalidad no implementa verificación de identidad, invitaciones
ni una lista de QA permitidos. El despliegue requiere gestionar por separado quién
puede acceder a la prueba. La revisión legal del texto y el aviso de privacidad
siguen siendo tareas distintas; el usuario autorizó la implementación y suministró
los datos del responsable, sin que eso equivalga a una revisión profesional.

No se conservan IP, agente de usuario, contraseñas, tokens ni cookies como parte de
la constancia. Su conservación y eventuales solicitudes de supresión requieren
gestión del responsable según las obligaciones aplicables. El borrador bilateral
original se conserva como referencia y no es el texto que publica la SPA.

## Verificación

Pruebas API para bloqueo antes de aceptar en operaciones de lectura y escritura,
aceptación explícita, antiforgery, versión desactualizada, persistencia, separación
de cuentas, idempotencia, constancia y cambio de documento. Pruebas SPA para
redirección de cuentas existentes, casilla inicialmente desmarcada, espera del
guardado, error y reintento, rechazo, consulta pública y descarga de constancia.
