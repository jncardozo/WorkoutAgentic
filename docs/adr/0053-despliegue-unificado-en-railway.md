# ADR 0053 — SPA y API en un servicio Railway

- **Estado:** Aceptado para la publicación de QA solicitada.
- **Fecha:** 2026-11-23.

## Decisión

- Construir Angular 21 con Node 22/npm 11.19.0 y publicar la API .NET 10
  en etapas separadas de un Dockerfile. La imagen final contiene la SPA en
  `wwwroot`, sin SDK, Node, secretos, bases locales ni claves de autenticación.
- Servir ambos desde el mismo origen HTTPS. Resolver navegación directa de la
  SPA con `index.html`; las rutas `/api`, `/health` y los archivos inexistentes
  conservan su respuesta de API/404. No almacenar respuestas en caché.
- Escuchar en `0.0.0.0:$PORT`, con 8080 como valor por defecto. La sonda anónima
  `/health` comprueba conexión a SQLite y admite HTTP interno sin redirección.
  Las migraciones y el seed actuales terminan antes de aceptar tráfico.
- Montar un único volumen en `/data`: SQLite y `auth-keys` sobreviven a reinicios
  y despliegues. Exigir el montaje con `requiredMountPath`, una sola réplica,
  una sola región y no ejecutar migraciones en build/predeploy. La primera
  publicación usa una base nueva; no copia datos ni usuarios de desarrollo.
- El volumen Railway pertenece a root. La imagen usa root con `umask 077`
  para crear base y claves con acceso restringido al usuario del proceso.
  Las claves no están cifradas en reposo por Data Protection: proteger acceso
  al volumen y sus copias. Respaldar antes de cambios de esquema o rollback.
- Opt-in `Deployment:RailwayEdge=true` exclusivamente en el entorno dedicado
  de este servicio, sin TCP proxy y sin servicios privados no confiables.
  El borde HTTP de Railway termina TLS y reemplaza `X-Real-IP` y
  `X-Forwarded-Proto`; el middleware los procesa antes de HTTPS, cookies y
  límites por IP. No se toma `X-Forwarded-Host` ni el header RFC `Forwarded`.
  Railway no publica una lista estable de IPs del borde. Por eso este modo
  confía en el aislamiento del entorno, no en una allowlist de IPs ni en una
  cabecera que autentique al proxy. **No activarlo en un servidor expuesto
  directamente ni en un entorno con otros servicios no confiables.** Si cambia
  ese supuesto, revisar el ingreso antes de reutilizar esta configuración.
  Fuera del opt-in no se procesan cabeceras reenviadas.
- Conservar cookies Secure/HttpOnly, CSRF, acuerdo obligatorio y origen
  explícito de recuperación de contraseña. Proveedores Gemini/Brevo por
  variables privadas del servicio. Sin credenciales como argumentos de build.
- `railway.json` fija Dockerfile, sonda, montaje y reinicios acotados. Usar
  el plan existente, sin contratar ni ampliar recursos de pago automáticamente.

## Verificación

Tests de integración de rutas SPA/404, HTTPS sin opt-in, sonda HTTP y login con
cookies seguras. Compilación/publish real, arranque y persistencia tras reinicio.
El despliegue remoto debe comprobar imagen Linux, acceso público y proveedores
reales; un resultado local no sustituye esas comprobaciones.

## Referencias

- [Dockerfiles](https://docs.railway.com/builds/dockerfiles).
- [Esquema de configuración](https://railway.com/railway.schema.json).
- [Red pública](https://docs.railway.com/networking/public-networking/specs-and-limits).
- [Límite de confianza del ingreso](https://station.railway.com/questions/asp-net-core-trusted-ingress-boundary-an-ea6cad35).
- [Volúmenes](https://docs.railway.com/volumes).
- [Proxies ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0).
