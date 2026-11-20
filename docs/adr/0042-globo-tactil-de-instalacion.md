# ADR 0042 — Globo táctil de instalación móvil

- **Estado:** Aceptado; cambio solicitado por el usuario.
- **Fecha:** 2026-11-18.

## Contexto

Tras ADR-0041 el usuario sigue sin ver la tarjeta en su Android y solicita un
tooltip con la leyenda «Instalar» tanto en Android como en iOS. No se ha podido
verificar qué versión recibe ese dispositivo desde el túnel privado.

## Decisión

- Sustituir la tarjeta por un globo compacto «Instalar», con ícono y cierre,
  alineado a la derecha en una segunda fila del encabezado sticky. No superponer
  el globo sobre campos, dictado ni acciones de entrenamiento. Mantener el estilo
  Energy Orange, controles de 44 px y foco visible.
- Es un control táctil con semántica de botón, no un `role=tooltip` ni un texto
  dependiente del hover: el alumno debe poder accionarlo con toque o teclado.
- Mostrarlo desde el arranque en todos los navegadores Android e iOS, incluido
  iPad con identificación de escritorio. Usar identificación Android de Client
  Hints y pantalla pequeña con puntero táctil como alternativas cuando la
  identificación del navegador sea reducida. No restringir la visibilidad a
  Chrome ni a la llegada de un evento de instalación.
- Al tocar, consumir el evento real de instalación si existe; de lo contrario,
  abrir ayuda modal. Android recibe pasos del menú de Chrome y la aclaración de
  que un acceso directo puede abrir el navegador. iOS recibe pasos de Safari;
  otros navegadores iOS reciben además la indicación de abrir la página en Safari.
  Si no se identifica el sistema, mostrar ambas rutas de ayuda.
- El globo sigue activo tras cancelar o fallar un prompt: otro toque abre la
  ayuda mientras el navegador no ofrezca un nuevo intento. No prometer ni simular
  instalación. Conservar cierre por Escape, retorno de foco, ocultación en la
  PWA instalada, editor e impresión y descarte durante la carga actual de la SPA.

## Consecuencias y verificación

Reemplaza la presentación y el bloqueo previo al evento de ADR-0040/0041.
La instalación nativa continúa dependiendo del navegador. No se modifica la
autenticación del túnel ni se certifica la causa del problema reportado allí.
Verificar identificación de navegadores móviles, ayuda Android/iOS, oferta real,
cancelación, error, descarte, foco y modos instalados; ejecutar lint, pruebas y
build. Validar que el servidor del puerto 4200 entregue esta versión antes de
atribuir una ausencia del globo al navegador del celular.

Fuentes: [instalación desde la web](https://web.dev/learn/pwa/installation-prompt),
[pasos de Chrome Android](https://support.google.com/chrome/answer/9658361?co=GENIE.Platform%3DAndroid&hl=es).
