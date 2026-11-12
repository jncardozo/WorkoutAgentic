# ADR 0035 — Íconos instalados y arranque Energy Orange

- **Estado:** Aceptado (2026-11-11), propuesta aprobada por el usuario.

## Contexto

El encabezado adaptaba la marca verde con un filtro CSS, pero el ícono instalado
seguía usando el PNG original. La SPA no enlazaba un manifiesto ni declaraba los
colores de arranque; la instalación móvil mostraba un ícono verde y fondo blanco.

## Decisión

- Crear un símbolo vectorial plano de mancuerna/onda naranja `#ff6a00` sobre
  fondo opaco `#080c10`, sin texturas, marco ni esquinas redondeadas dibujadas.
  El sistema operativo aplica su propia máscara.
- Conservar el SVG editable `public/brand/app-icon-orange.svg`. Exportar PNG
  normales de 192/512 px, maskable de 192/512 px, apple-touch-icon de 180 px y
  favicons de 32/48 px. Reemplazar también el favicon.ico de raíz.
- Los maskable usan una composición independiente con más margen. Todo el
  símbolo queda dentro del círculo seguro de radio 40% del ancho, sin alfa.
  Los normales amplían el símbolo 1,12 veces. Las exportaciones se reproducen
  con `src/web/scripts/generate-app-icons.ps1` en Windows, sin dependencias nuevas.
- Enlazar `manifest.webmanifest` desde index.html: id, start_url y scope en `/`,
  display standalone, nombre WorkoutAgentic, idioma español, íconos separados
  por purpose y background_color/theme_color oscuros. No fijar orientación.
- Declarar theme-color, color-scheme y apple-touch-icon en el HTML. Un fondo
  oscuro inline evita el destello blanco mientras se cargan estilos/Angular.
- Android genera su pantalla de arranque a partir del manifiesto; no agregar una
  pantalla artificial ni retrasos. El tamaño/animación final dependen del SO.
  iOS recibe el ícono específico; Safari gestiona su propia pantalla de lanzamiento
  y no usa background_color del manifiesto de la misma manera que Android.

## Consecuencias y verificación

No cambia la captura, NLU, persistencia ni autenticación. No se agrega un service
worker ni caché offline como parte de este cambio visual. Los PNG verdes
históricos se conservan; no se referencian como íconos de instalación.

Validar tamaños/colores, opacidad y zona segura, distribución de los archivos en
el build y entrega HTTP del manifiesto y sus recursos. Completar la validación
del launcher y arranque en Android/iOS reales después de publicar. Para comparar
el nuevo ícono, usar una instalación nueva desde la URL actualizada; la instalación
anterior puede conservar metadatos. No es necesario borrar datos del sitio.

Fuentes: [manifiesto y arranque](https://web.dev/learn/pwa/web-app-manifest),
[íconos maskable](https://web.dev/articles/maskable-icon),
[íconos de Safari](https://developer.apple.com/library/archive/documentation/AppleApplications/Reference/SafariWebContent/ConfiguringWebApplications/ConfiguringWebApplications.html).
