# ADR 0047 — Alineación del encabezado de planilla

- **Estado:** Aceptado, solicitado por el usuario.
- **Fecha:** 2026-11-19.

## Contexto

El estado y las acciones compartían una fila flexible: el botón de historial
tenía una altura distinta y «Actualizar» saltaba a una fila aislada en móvil.

## Decisión

- Agrupar estado y última actualización debajo del título, separados de las
  acciones. En escritorio, las acciones se alinean a la derecha; en móvil
  forman una fila a todo el ancho debajo de los metadatos.
- «Mis entrenamientos» ocupa el espacio principal de esa fila y «Actualizar»
  queda a su lado. Ambos comparten altura de 48 px, radios y alineación central.
- Hasta 359 px, Actualizar muestra solo su ícono, conservando su nombre
  accesible. El historial mantiene su texto y contador real.
- El título usa una escala de 24–30 px y una altura de línea más compacta.
  No cambian el historial modal, las acciones ni los datos del entrenamiento.

## Verificación

Lint, pruebas y compilación; inspección con nombres largos, sesión activa y sin
sesión, contador visible y vistas de 320/360/390 px y escritorio. Comprobar
igual altura de botones y ausencia de desbordamiento horizontal.
