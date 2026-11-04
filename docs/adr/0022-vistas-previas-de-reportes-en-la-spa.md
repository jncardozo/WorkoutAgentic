# ADR 0022 — Vistas previas de reportes en la SPA

- **Estado:** Aceptado (2026-10-27)
- **Contexto:** después de confirmar la planilla, el alumno necesita elegir un
  reporte PDF o Excel, revisar sus datos y luego descargarlo o imprimirlo.

## Decisión

- Al confirmar, la SPA abre un selector de formato con las opciones PDF y Excel.
- Cada formato tiene una vista de reporte con acciones de descarga e impresión.
- La acción Imprimir abre una previsualización PDF A4 o de la hoja Excel; desde
  esa previsualización se lanza la impresión del navegador.
- Las vistas se alimentan exclusivamente del snapshot confirmado disponible en
  la SPA; no agregan nombres, firmas, datos de auditoría ni valores de ejemplo.
- Las acciones de descarga siguen usando los endpoints existentes. La vista
  previa Excel refleja las columnas y los cálculos exportados por la API.
- El modo de impresión oculta los controles de previsualización e imprime el
  formato seleccionado.

## Consecuencias

- El usuario puede alternar entre los formatos y volver a la planilla sin
  afectar la captura ni modificar el snapshot.
- La previsualización no crea una nueva persistencia ni cambia el contrato de
  la API.
