# ADR 0059 — Tarjetas móviles sin editor de ejercicio y ordinal

- **Estado:** Aceptado (2026-11-25), solicitado tras probar las tarjetas.
- **Amplía:** ADR-0058.

## Decisión

- Retirar «Ejercicio y número de serie» únicamente de las tarjetas embebidas en
  la pantalla móvil. El nombre sigue visible en la cabecera del ejercicio y el
  ordinal en «Serie N»; se conservan peso, repeticiones, RM y notas editables.
- Conservar ejercicio y ordinal en el borrador y en el contrato de edición,
  aunque no haya controles visibles para modificarlos. Guardar cambios de carga
  no borra, reemplaza ni renumera esos datos, incluida la identidad histórica.
- La grilla y el editor de pantalla completa conservan la edición de identidad.
  La variante `embedded` sigue compartiendo guardado y validaciones existentes;
  no cambia la API ni la base de datos.

## Verificación

- Tarjetas sin la sección y con cabecera/ordinal visibles.
- Al cambiar peso se envían intactos ejercicioId, ejercicioCrudo y nroSerie en
  originales y valores nuevos. El editor completo conserva los controles.
