# Habilitar el envío real por email

La integración de correo no crea ni activa una cuenta externa. El aviso de envío
no habilitado indica que faltan los datos del proveedor en la API. No depende del
formato PDF/Excel ni de la dirección ingresada por el alumno.

## Para las pruebas con Railway gratuito

1. Crear una cuenta propia en [Brevo](https://www.brevo.com/) y completar la
   activación del correo transaccional que solicite el proveedor.
2. Autorizar y verificar el remitente según sus requisitos actuales. No usar una
   dirección inventada ni asumir que una dirección de Gmail está autorizada.
3. Crear una **API key de Brevo**, distinta de una contraseña o clave SMTP.
4. Configurar la API local con `dotnet user-secrets`, o las variables equivalentes
   en Railway. No pegar la clave en el chat ni agregarla a Git.

Desde la raíz del repositorio, reemplazar los marcadores **solo en la terminal
local**:

```powershell
dotnet user-secrets set "EmailDelivery:Provider" "brevo" --project src/api/WorkoutAgentic.Api
dotnet user-secrets set "EmailDelivery:FromAddress" "TU_REMITENTE_AUTORIZADO" --project src/api/WorkoutAgentic.Api
dotnet user-secrets set "EmailDelivery:FromName" "WorkoutAgentic" --project src/api/WorkoutAgentic.Api
dotnet user-secrets set "EmailDelivery:ApiKey" "TU_API_KEY_PRIVADA" --project src/api/WorkoutAgentic.Api
```

En Railway usar `EmailDelivery__Provider`, `EmailDelivery__FromAddress`,
`EmailDelivery__FromName` y `EmailDelivery__ApiKey`. Los user-secrets locales no se
copian a Railway y los archivos `.env` no se cargan automáticamente.

Reiniciar la API después de configurar los valores. En Railway, aplicar las
variables y volver a desplegar. Luego abrir «Enviar planilla por email» o usar
«Comprobar de nuevo» si el panel ya estaba abierto.

## Qué verifica la aplicación

El estado autenticado `/api/planillas/correo/estado` indica si hay configuración
local suficiente. No envía emails, comprueba cuotas ni certifica que el remitente
esté aprobado. Una clave inválida o un rechazo del proveedor fallarán al enviar.

Para verificar el circuito completo, elegir una planilla histórica y enviar una
copia PDF y otra Excel a una casilla de prueba autorizada por su titular. Revisar
bandeja y spam, abrir ambos adjuntos y comprobar fecha, ejercicios, series y
notas. Una aceptación del proveedor no garantiza recepción en bandeja de entrada.

## Referencias

- [Railway: SMTP bloqueado en Free, Trial y Hobby](https://docs.railway.com/networking/outbound-networking).
- [Brevo: API de envío transaccional](https://developers.brevo.com/reference/send-transac-email).
- [Brevo: límites de su plan gratuito](https://help.brevo.com/hc/en-us/articles/208580669-FAQs-What-are-the-limits-of-the-Free-plan).
