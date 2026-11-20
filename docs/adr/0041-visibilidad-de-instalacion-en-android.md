# ADR 0041 — Visibilidad de la instalación en Android

- **Estado:** Aceptado como corrección del flujo solicitado.
- **Fecha:** 2026-11-18.

## Contexto

En una prueba con Chrome Android sobre el túnel HTTPS, la tarjeta de ADR-0040
no aparecía. Se ocultaba por completo hasta recibir `beforeinstallprompt`, sin
explicar al alumno la falta de disponibilidad. La captura no permite establecer
por qué Chrome no emitió el evento; un 401 obtenido sin sesión del túnel tampoco
demuestra que el manifiesto falle en la sesión del celular.

## Decisión

Se reemplaza la condición de visibilidad de ADR-0040 para Chrome Android:
mostrar la tarjeta desde el inicio, antes o después de iniciar sesión en la app.
Mientras no exista un evento real, «Instalar» permanece deshabilitado con el
mensaje «Chrome todavía no habilitó la instalación. Podés seguir usando la app
desde acá». No usar un indicador de carga ni prometer disponibilidad futura.

Al recibir `beforeinstallprompt`, habilitar el botón y retirar el mensaje. Tras
cancelar, explicar que Chrome debe habilitar un nuevo intento. Ante error,
conservar la explicación existente y esperar una nueva oferta. «Ahora no», los
estados instalados, la guía iOS y la exclusión de escritorio se conservan.
Otros navegadores Android pueden ofrecer instalación si emiten el evento real,
pero no reciben la explicación específica de Chrome al entrar.

## Consecuencias y verificación

Corrige la ausencia silenciosa de la interfaz; no garantiza ni fuerza el diálogo
nativo. No modifica permisos del túnel, autenticación, manifiesto ni datos.
La causa de la falta de oferta en el dispositivo sigue pendiente de diagnóstico.
Verificar renderizado inicial y bloqueo, habilitación por evento, cancelación,
reoferta, estados instalados, lint, pruebas frontend y build de producción.
