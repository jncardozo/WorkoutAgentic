# ADR 0032 — Energy Orange y cuadrícula por ejercicio

- **Estado:** Aceptado (2026-11-11), diseño aprobado por el usuario.

## Contexto

La interfaz usaba acentos lima/esmeralda y repetía el ejercicio en cada fila.
El usuario propone la referencia Stitch Energy Orange y tarjetas con una sola
cabecera por ejercicio para reducir ruido visual.

## Decisión

- La SPA usa naranja `#ff6a00` como acento, variantes claras para hover/textos
  y superficies marrón oscuro para confirmaciones. Botones, progreso, badges
  «Lista para confirmar», mensajes de carga cerrada, editor y reportes comparten
  la paleta. Errores y advertencias conservan sus colores semánticos.
- La marca existente se adapta con un filtro CSS, sin reemplazar el archivo.
- Sesión editable y snapshot confirmado presentan tarjetas por nombre de
  ejercicio, con cabecera única y cantidad de series. Cada tarjeta contiene una
  tabla sin columna Ejercicio: Serie, Reps, Peso, Comentarios, Observaciones y
  acciones solo cuando corresponden.
- Agrupación visual por nombre sin distinguir mayúsculas y espacios externos.
  Las tarjetas siguen el orden de primera aparición; las filas conservan su orden
  dentro del ejercicio. No se modifica el orden persistido, identidad, numeración,
  nombre ni contenido de ninguna fila. No se deducen equivalencias entre nombres.
- La cabecera queda fuera del scroll horizontal de la tabla. Solo la tabla puede
  desplazarse lateralmente; la página y la cabecera se ajustan al ancho móvil.
- «Editar» y el número de serie abren el borrador inline. El nombre de esa serie
  se puede corregir en una fila adicional del mismo bloque. Al guardar se vuelve
  a agrupar con los nombres recibidos de la API. El editor múltiple sigue disponible
  desde «Editar entrenamiento», con los ejercicios inicialmente colapsados.

## Consecuencias

Es un cambio de presentación, sin nuevas rutas, contratos ni migraciones.
La referencia histórica `prototipo-stitch.html` conserva su paleta original;
`docs/design/README.md` documenta el diseño vigente.
