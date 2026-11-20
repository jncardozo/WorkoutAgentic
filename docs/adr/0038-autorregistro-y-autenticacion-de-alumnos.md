# ADR 0038 — Autorregistro y autenticación de alumnos

- **Estado:** Aceptado, solicitado por el usuario.
- **Fecha:** 2026-11-17.
- **Contexto:** dos testers (iPhone y Samsung) deben abrir un enlace, registrarse
  y cargar entrenamientos independientes. El ID de demo compartido de ADR-0022
  y el alta por nombre no representan una identidad autenticada.

## Decisión

- ASP.NET Core Identity con EF Core gestiona cuentas, hashes de contraseña,
  bloqueo y tokens de recuperación. La cuenta `ApplicationUser` tiene una
  relación única y obligatoria con `Alumno`. Credenciales y dominio quedan
  separados para permitir proveedores externos futuros.
- `POST /api/alumnos` es el autorregistro explícito con `name`, `email` y
  `password`. Crea identidad y alumno en una transacción. El correo es único
  sin distinguir mayúsculas. Nombre: 1–120 caracteres; correo: hasta 180;
  contraseña: 12–128, admite frases. Los errores se presentan en español.
- La SPA ofrece `/login`, `/registro`, `/recuperar-clave` y
  `/restablecer-clave`. Tras registrar inicia sesión y abre la planilla propia.
  La raíz requiere sesión y resuelve el alumno mediante `/api/auth/me`.
  Las rutas históricas con ID redirigen a la planilla del usuario autenticado.
- Login por email y contraseña. Cookie de autenticación HttpOnly, SameSite=Lax,
  sin persistencia explícita y con límite absoluto de ocho horas. Secure es
  obligatorio fuera de Development; HTTP se permite únicamente en desarrollo
  local. SPA y API se publican bajo un mismo origen HTTPS.
- Todas las mutaciones `/api` (incluidos registro, login y logout) validan un
  token antiforgery. `/api/auth/csrf` entrega el token de solicitud mediante
  `XSRF-TOKEN`, legible por Angular, que lo envía en `X-XSRF-TOKEN`.
  La cookie de autenticación permanece inaccesible a JavaScript. El token CSRF
  se renueva después de cambiar de identidad. No se guardan credenciales en
  localStorage ni sessionStorage. Respuestas API: Cache-Control=no-store.
- Captura y planillas requieren autenticación y pertenencia. Los IDs de rutas,
  consultas y cuerpos se comparan contra el alumno de la cuenta. Filas y
  snapshots se verifican por su propietario antes de operar. Recursos ajenos
  por ID devuelven 404; un alumno distinto del autenticado devuelve 403.
  El control incluye historial, edición múltiple, exportación y correo.
- Cinco contraseñas incorrectas bloquean el login durante 15 minutos. Las
  operaciones de cuentas admiten 20 solicitudes por minuto por IP de conexión
  y proceso; con proxy pueden compartir límite. No se confía en cabeceras de
  IP suministradas por el cliente. Escalado requiere revisar este límite.
- Logout invalida las sesiones de esa cuenta en todos los dispositivos mediante
  security stamp. El backend lo verifica en cada petición. Restablecer la
  contraseña también invalida las sesiones; no modifica entrenamientos.
- Recuperación con respuesta genérica exista o no el correo; token de Identity
  de una hora y de un solo uso efectivo tras el cambio de contraseña. Entrega
  por SMTP existente. El origen del enlace viene de `Auth:PublicOrigin`, HTTPS,
  nunca del Host enviado por el solicitante. Sin origen/SMTP configurados no
  se envía correo; la respuesta no revela la existencia de la cuenta. No se
  registran contraseñas, cookies ni enlaces/tokens de recuperación. La página
  usa Referrer-Policy mediante meta `no-referrer` y retira el token de la URL
  después de restablecer correctamente.
- Las claves Data Protection se conservan en `Auth:DataProtectionKeysPath` o
  `auth-keys` junto a la base SQLite. Deben persistir y protegerse con permisos
  del sistema operativo y respaldo restringido; no se incluyen en Git. Este
  almacenamiento no agrega cifrado en reposo por sí mismo. Compartir claves
  y configuración será obligatorio si se despliega más de una instancia.

## Alcance y evolución

Se habilita autorregistro abierto para esta prueba; no hay invitaciones de
profesor ni verificación de email como requisito de acceso. El email indicado
no constituye una identidad verificada. Antes de abrir el producto al público,
revisar verificación de email, MFA, protección contra abuso y operación del correo.
La recuperación ya tiene flujo funcional, pero exige SMTP y origen público reales.

No se implementa un emisor JWT propio. Una app nativa o integraciones futuras
podrán usar un proveedor OIDC con Authorization Code + PKCE y tokens para la API;
la SPA podrá conservar cookies. Esta evolución requiere integración adicional.

La migración `AddStudentIdentity` agrega las tablas de Identity sin modificar
migraciones anteriores ni asociar automáticamente alumnos históricos. Los datos
del alumno #1 se conservan y no pertenecen a las cuentas nuevas.

## Vigencia de decisiones anteriores

- Concreta cookie y credenciales de ADR-0002 S6; reemplaza su alta por invitación
  del profesor y no incorpora los roles/procesos de ese módulo retirado.
- Reemplaza el alta de solo nombre de ADR-0010 y la exclusión de autenticación
  de aquel sprint de ADR-0018.
- Sustituye el alumno fijo del ADR-0022 de inicio directo, sin afectar el otro
  ADR-0022 de vistas previas de reportes.

## Verificación

Pruebas con cookies y antiforgery reales: alta atómica, correo duplicado,
credenciales, bloqueo, logout y revocación, recuperación, acceso anónimo y
aislamiento de dos cuentas. Regresiones del dominio usan cuentas vinculadas a
sus alumnos sintéticos. Pruebas SPA cubren formulario, errores, guard y renovación
CSRF. Completar pruebas físicas en iPhone/Samsung desde el enlace HTTPS real y
entrega SMTP; las pruebas automatizadas no certifican esos servicios/dispositivos.

Referencias: [Identity para SPA](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-api-authorization?view=aspnetcore-10.0),
[antiforgery](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0).
