# SPA del alumno

La aplicación Angular permite al alumno dictar o escribir su entrenamiento,
revisar y editar la cuadrícula, y confirmar su planilla. El reconocimiento de
voz se ejecuta en el navegador; la API recibe el texto final. El sprint actual
no incluye WhatsApp ni el módulo de profesor (ADR-0018).

Luego de confirmar, la vista permite descargar la planilla en Excel o PDF, o
enviar uno de esos formatos como adjunto a un correo. El envío requiere que la
API tenga SMTP configurado; no se guardan credenciales en este proyecto.

## Desarrollo

Desde esta carpeta:

```powershell
npm start
```

La SPA queda disponible en `http://localhost:4200/` y usa el proxy de desarrollo
para comunicarse con la API.

## Compilación y calidad

```powershell
npm run build -- --configuration development
npm run lint
npm test
```
