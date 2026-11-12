# ADR 0036 — Manifiesto con sesión en túneles privados

- **Estado:** Aceptado (2026-11-11), corrección de instalación reportada por el usuario.

## Contexto

Después de ADR-0035, Chrome Android mostraba «No se puede instalar esta app» en
el túnel de desarrollo privado. Al consultar el manifiesto y los íconos desde un
cliente sin sesión, el túnel redirige a GitHub y entrega HTML de inicio de sesión.
En localhost los mismos archivos son válidos. Esa comprobación local no cubría
la autenticación añadida por el túnel.

## Decisión

Declarar `crossorigin="use-credentials"` en el enlace al manifiesto. Su descarga
debe incluir la sesión existente del navegador, incluso cuando manifiesto y SPA
comparten origen. Esto permite solicitar el manifiesto a través del túnel privado
sin eliminar su autenticación ni hacer público el puerto.

No se agregan credenciales al archivo ni a sus URLs. No se cambia el manifiesto,
el service worker, la identidad de instalación, los íconos ni permisos del túnel.
Una solicitud sin sesión sigue redirigiéndose al login, como corresponde.

## Verificación y límites

Comprobar el atributo en el HTML servido y generado. Revalidar la instalación
desde Chrome Android con sesión activa en el mismo túnel. El cambio corrige el
modo de descarga del manifiesto; no certifica la instalación WebAPK en el celular
ni garantiza que no existan otros bloqueos del navegador o del servicio de instalación.

Referencia: [despliegue de manifiestos con credenciales](https://developer.mozilla.org/en-US/docs/Web/Progressive_web_apps/Manifest#deploying_a_manifest).
