# ADR 0019 — Exportación y envío de planillas confirmadas

- **Estado:** Aceptado (2026-10-24)
- **Contexto:** al terminar una jornada, el alumno necesita conservar o compartir
  una copia de la planilla desde la SPA.

## Decisión

- Después de confirmar, la misma vista presenta acciones para descargar el
  snapshot como Excel (`.xlsx`) o PDF y para enviarlo como adjunto por correo en
  cualquiera de esos dos formatos.
- El destinatario y el formato de correo los elige el alumno. La interfaz usa
  `jncardozo646@gmail.com` solo como ejemplo en el placeholder; no precompleta ni
  envía esa dirección.
- La API carga la planilla por su identificador y el `alumnoId` asociado. Los
  archivos se generan exclusivamente desde el snapshot confirmado, que permanece
  inmutable.
- El envío usa SMTP configurable mediante la sección `CorreoSmtp` de la
  configuración de la API. Host, puerto, TLS, usuario, contraseña y remitente
  deben configurarse fuera de los archivos versionados; en desarrollo se usa
  `dotnet user-secrets` y en producción variables de entorno o un almacén de
  secretos.
- Si SMTP no está configurado o el servidor rechaza el envío, la API devuelve un
  error claro y la SPA informa al alumno. Descargar no requiere SMTP.

## Consecuencias

- La SPA necesita conectividad con la API para exportar y enviar los archivos.
- El servidor SMTP entrega el mensaje y el archivo adjunto a la dirección elegida;
  el sistema no guarda una copia de los correos enviados.
- Las reglas de edición posteriores a la confirmación no cambian: la exportación
  no modifica ni reabre la planilla.

## Configuración de desarrollo

Desde la raíz del repositorio, configurar los valores necesarios en User Secrets
del proyecto `src/api/WorkoutAgentic.Api` (no guardar credenciales en
`appsettings.json`):

```powershell
dotnet user-secrets set "CorreoSmtp:Host" "smtp.example.com" --project src/api/WorkoutAgentic.Api
dotnet user-secrets set "CorreoSmtp:Port" "587" --project src/api/WorkoutAgentic.Api
dotnet user-secrets set "CorreoSmtp:EnableSsl" "true" --project src/api/WorkoutAgentic.Api
dotnet user-secrets set "CorreoSmtp:UserName" "usuario-smtp" --project src/api/WorkoutAgentic.Api
dotnet user-secrets set "CorreoSmtp:Password" "contraseña-smtp" --project src/api/WorkoutAgentic.Api
dotnet user-secrets set "CorreoSmtp:FromAddress" "entrenamiento@example.com" --project src/api/WorkoutAgentic.Api
```

En producción, las variables equivalentes usan el prefijo `CorreoSmtp__`, por
ejemplo `CorreoSmtp__Host` y `CorreoSmtp__Password`.
