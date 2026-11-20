# ADR 0043 — Login Stitch y movimiento continuo

- **Estado:** Aceptado, solicitado por el usuario.
- **Fecha:** 2026-11-18.

## Contexto

El usuario solicita adaptar el login al proyecto Stitch «Voice Workout Tracker
SPA», pantalla «WorkoutAgentic — Login con Fondo de Ondas y Aura Animada», con
colores en movimiento continuo en los botones y dinamismo visual de fondo.

## Decisión

- Las pantallas de cuenta comparten tarjeta obsidiana, detalles Energy Orange,
  campos oscuros con íconos y la marca existente en un encabezado centrado.
- El fondo es decorativo: aura mediante gradientes radiales CSS, retícula y
  ondas SVG inline. Las animaciones CSS `@keyframes` son continuas y no dependen
  del hover. Los botones Ingresar/Crear cuenta y sus equivalentes en recuperación
  usan gradientes en pseudoelementos que se desplazan mediante `transform`.
  El acceso al registro tiene un barrido cálido continuo más sutil.
- El ecualizador del chip comunica la función de voz; no indica un micrófono
  encendido ni una conexión verificada. Las capas visuales no capturan eventos
  ni se anuncian a lectores de pantalla.
- Un control permite pausar/reanudar todas las animaciones del componente.
  `prefers-reduced-motion: reduce` las detiene y elimina las transiciones.
  Los estados de este control y de mostrar/ocultar contraseña usan signals.
- Se conservan validaciones, credenciales, rutas y llamadas de ADR-0038. No se
  copian accesos Google/WhatsApp, indicadores online ni afirmaciones de cifrado
  biométrico del prototipo: no representan funcionalidades del producto actual.
- Los estilos del encabezado se limitan a la presencia de `app-auth-page`;
  planilla, cuenta autenticada e instalación PWA mantienen sus estilos propios.

## Consecuencias y verificación

No se agregan dependencias, videos, GIF, temporizadores JavaScript ni cambios de
API. Se animan `transform` y opacidad, manteniendo legibles los textos. Verificar
lint, pruebas existentes de cuentas, compilación y vistas de escritorio/móvil,
incluyendo mostrar/ocultar contraseña, pausa y preferencia de movimiento reducido.
