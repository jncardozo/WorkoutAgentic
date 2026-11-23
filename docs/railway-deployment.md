# Publicación de QA en Railway

## Servicio

- Proyecto existente `serene-luck`, servicio `WorkoutAgentic`, rama `dev`.
- Build context: raíz del repositorio (no `src/api` ni `src/web`).
- Dockerfile y configuración `railway.json` en la raíz. Sin comandos manuales
  de build/start que reemplacen el Dockerfile.
- Una réplica/región. Volumen persistente `/data`, hasta el límite del plan
  actual. SQLite y claves se inicializan al arrancar. No copiar la base local.
- Dominio público: `https://workoutagentic-production.up.railway.app`.
- Sonda `/health` por HTTP interno: 200 después de iniciar SQLite/migraciones.

## Variables del servicio

| Nombre | Valor/configuración |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `PORT` | `8080` (mismo puerto que el dominio) |
| `Deployment__RailwayEdge` | `true`, solo con las condiciones de ADR-0053 |
| `ConnectionStrings__Sqlite` | `Data Source=/data/workoutagentic.db;Cache=Shared` |
| `Auth__DataProtectionKeysPath` | `/data/auth-keys` |
| `Auth__PublicOrigin` | `https://workoutagentic-production.up.railway.app` |
| `AllowedHosts` | `workoutagentic-production.up.railway.app;healthcheck.railway.app` |
| `Gemini__ApiKey` | Secreto privado, nunca en Git |
| `Gemini__ModeloNlu` | Modelo configurado y validado con la cuenta del operador |
| `EmailDelivery__Provider` | `brevo` |
| `EmailDelivery__ApiKey` | Secreto privado, nunca en Git |
| `EmailDelivery__FromAddress` | Remitente verificado en Brevo |
| `EmailDelivery__FromName` | `WorkoutAgentic` |

No configurar `ASPNETCORE_FORWARDEDHEADERS_ENABLED` globalmente ni publicar
un TCP proxy. No agregar otros servicios no confiables al mismo entorno.
El origen explícito debe actualizarse si cambia el dominio público.
Las variables locales de user-secrets no se cargan automáticamente en Production.

## Verificación antes de compartir

1. Build Docker y despliegue terminan satisfactoriamente. `/health` responde 200.
2. Abrir `/` y `/login` directamente, recargar y revisar móvil.
3. Registrar una cuenta QA, iniciar sesión, aceptar explícitamente el acuerdo.
4. Cargar texto/voz, corregir y confirmar. Historial y snapshot conservan datos.
5. Descargar PDF y XLSX y abrir ambos; envío real a una casilla consentida.
   Comprobar recepción (incluido spam); aceptación de Brevo no prueba recepción.
6. Recuperar contraseña: enlace con este dominio, token válido y sesión revocada.
7. Reiniciar/redeploy y repetir login/historial; comprobar que base y claves
   permanecen. No eliminar ni desmontar el volumen al actualizar la app.

El servicio puede dormir según el plan. Revisar crédito y cuota antes de la
sesión con QA. No se garantiza disponibilidad continua por usar el plan gratuito.
El dictado depende también del navegador y de que el usuario autorice micrófono.

## Operación

Los logs no deben contener claves ni enlaces de recuperación. Revisar errores de
arranque, cuota Gemini y estado de Brevo ante fallos. Antes de migraciones futuras,
respaldar el volumen; volver a una imagen anterior no revierte el esquema SQLite.
La clave API de Brevo usada por QA puede reemplazarse por una de producción sin
reconstruir la imagen. No reutilizar contraseñas personales en cuentas QA.
