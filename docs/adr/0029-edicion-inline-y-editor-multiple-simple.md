# ADR 0029 — Edición inline y editor múltiple simplificado

- **Estado:** Aceptado (2026-11-11), solicitado por el usuario.
- **Contexto:** se necesitan dos formas de edición claramente diferenciadas.

## Decisión

- «Editar» en la cuadrícula vuelve a abrir los campos inline de esa fila, con
  Guardar/Cancelar y el PUT individual. Tocar el nombre del ejercicio hace lo mismo.
- «Editar entrenamiento» conserva el editor múltiple, inicialmente colapsado,
  y el guardado transaccional de varias filas editadas individualmente.
- Se retiran la herramienta «Aplicar a varias series», la selección de filas,
  los valores compartidos y su código de estado/aplicación del editor múltiple.
- Se retira el checkbox «Al cuerpo» de ese editor. Las cargas corporales ya
  registradas se muestran como texto y se conservan al editar otros campos.
  Introducir un peso numérico cambia esa fila a kg. El dictado, la API y los
  formularios inline/alta conservan su soporte de peso corporal.

Se mantienen las validaciones, la confirmación de descarte, la protección de
conflictos y los snapshots inmutables. No cambia el esquema ni los contratos API.
Esta decisión sustituye las secciones de apertura individual y aplicación de
valores compartidos de ADR-0027. La especificación actual está en
[edicion-planilla.md](../domain/edicion-planilla.md).
