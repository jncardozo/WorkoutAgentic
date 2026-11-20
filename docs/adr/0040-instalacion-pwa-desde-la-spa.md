# ADR 0040 — Instalación móvil desde la SPA

- **Estado:** Aceptado; propuesta aprobada por el usuario.
- **Fecha:** 2026-11-18.

## Contexto

El manifiesto y los íconos de ADR-0035 permiten instalar la aplicación, pero
el alumno debe descubrir la opción en el navegador. Se necesita una invitación
visible al entrar, sin ocupar la cabecera ni tapar las acciones de entrenamiento.

## Decisión

- Presentar una tarjeta debajo del encabezado, antes del contenido de las rutas,
  tanto antes como después del acceso. Usar ícono de la app, beneficio breve,
  botón naranja «Instalar» y acción secundaria «Ahora no». En pantallas pequeñas
  las acciones ocupan una segunda fila y tienen al menos 44 px de área táctil.
- Android: escuchar `beforeinstallprompt`, conservar el evento y abrir `prompt()`
  únicamente por el toque del alumno. Mostrar la acción cuando exista un evento
  real; su disponibilidad depende del navegador, HTTPS y sus criterios de uso.
  Consumir cada evento una sola vez, bloquear dobles toques y tratar cancelación
  y error sin anunciar una instalación inexistente. Una nueva oferta habilita
  otro intento, excepto si el alumno eligió «Ahora no» en esta carga de la SPA.
- Safari en iPhone: «Instalar» abre una guía en un `dialog` nativo con pasos
  Compartir, Agregar a pantalla de inicio y Agregar. Incluir la variante del menú
  Más y Abrir como app web. Safari no expone un prompt programático equivalente.
  El diálogo permite cierre por Escape y por «Entendido», contiene el foco y lo
  devuelve al botón de origen. No abrirlo automáticamente.
- Ocultar la promoción en standalone/fullscreen, con `navigator.standalone` en
  iOS o tras `appinstalled`. La confirmación del prompt oculta la invitación sin
  afirmar que la instalación ya terminó. No inferir que está instalada en iOS
  cuando se abre desde Safari: el navegador no ofrece esa detección general.
- «Ahora no» descarta la promoción durante la carga actual de la SPA, sin
  reaparecer al cambiar de ruta. Ocultarla en el editor de entrenamiento y al
  imprimir. No promover instalación en escritorio o navegadores sin soporte.

## Consecuencias y verificación

Se reutilizan manifiesto e íconos existentes, incluido `crossorigin=use-credentials`
de ADR-0036. No se agrega caché offline ni se modifican autenticación, API o NLU.
Chrome decide cuándo emitir el evento; la instalación no se puede forzar al
abrir por primera vez. En iPhone sigue siendo necesario completar los pasos de
Safari. No se usa la API genérica de compartir como sustituto de instalación.

Verificar lint, pruebas y build. Las pruebas simulan oferta, aceptación,
cancelación, errores, dobles toques, descarte, instalación, cambios de display,
guía de Safari y limpieza de listeners. Completar la validación nativa en Android
y iPhone desde la URL HTTPS publicada, incluyendo instalación desde el menú,
retorno a la SPA, teclado, tamaños pequeños y acceso desde el ícono instalado.

Fuentes: [promoción personalizada](https://web.dev/learn/pwa/installation-prompt),
[criterios de Chrome](https://web.dev/articles/install-criteria),
[instalación en iPhone](https://support.apple.com/en-az/guide/iphone/iphea86e5236/26/ios/26).
