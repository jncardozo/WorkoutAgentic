# ADR 0044 — Arranque animado Frecuencia Cinética

- **Estado:** Aceptado, solicitado por el usuario.
- **Fecha:** 2026-11-19.

## Contexto

El usuario aprobó el splash «Frecuencia Cinética» del proyecto Stitch y prioriza
que el alumno alcance a ver la animación, incluso si la app carga rápidamente.
La instalación actual es PWA: la pantalla inicial del sistema operativo no
ejecuta nuestras animaciones CSS/SVG (ADR-0035).

## Decisión

- El HTML inicial contiene una presentación a pantalla completa, fuera de
  `app-root`, con CSS crítico inline y SVG de mancuerna/ecualizador. El aura
  naranja respira, las barras oscilan con desfase y una línea de luz se desplaza
  continuamente. No requiere fuentes remotas, imágenes, Angular ni video para
  dibujarse. Las fuentes de la app se descargan sin bloquear este primer dibujo.
- La imagen estática usada por el sistema se actualiza a una mancuerna con
  ecualizador de la misma familia, manteniendo el fondo `#080c10`, tamaños y
  zonas seguras de ADR-0035. Android/iOS conservan el control de su splash.
- Una vez descargado el código, la inicialización de Angular y la navegación
  inicial ocurren en paralelo con la presentación. Se esperan al menos 2,2 s
  visibles desde el inicio del controlador; el tiempo en segundo plano no cuenta.
  Si la app tarda más, no se agrega otra espera. Esta solicitud sustituye la
  exclusión de presentaciones/retrasos de ADR-0035 solo para este arranque web.
- Cuando termina la navegación inicial aparece «Empezar entrenamiento» para
  omitir el tiempo restante; también continúa automáticamente con una salida
  de 240 ms. Se presenta una sola vez por carga completa del documento, no al
  cambiar rutas ni volver del segundo plano con el documento existente.
- El contenido de la app permanece `inert` y fuera del árbol accesible hasta
  retirar la presentación. Si el foco estaba en ella, se devuelve a la app.
  Movimiento reducido detiene todos los efectos y omite el tiempo mínimo.
- Se muestran estados reales «Preparando tu espacio de entrenamiento» y
  «Todo listo». La línea es indeterminada: no hay porcentaje ni procesos
  ficticios, versiones inventadas ni anuncio de un micrófono activo.
- Tras 12 s se informa que la carga tarda más de lo habitual. Un error de
  bootstrap/navegación ofrece un mensaje y recarga. El enlace de recarga existe
  en el HTML aunque no llegue a descargarse JavaScript; `noscript` explica que
  se necesita JavaScript para usar la aplicación.

## Verificación y límites

Probar espera mínima, navegación lenta, omisión manual, error, foco, segundo
plano y movimiento reducido; ejecutar lint, pruebas y build. Inspeccionar la
presentación y la transición en tamaños móviles y escritorio. La primera
pantalla nativa, la caché de íconos instalados y el arranque en Android/iOS
requieren verificación desde una instalación real con la versión publicada.
