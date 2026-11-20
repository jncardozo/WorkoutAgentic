# ADR 0039 — Menú de cuenta y alineación de acciones de captura

- **Estado:** Aceptado; propuesta aprobada por el usuario.
- **Fecha:** 2026-11-18.
- **Contexto:** en la captura móvil, el nombre y «Cerrar sesión» ocupan demasiado
  espacio en la cabecera. «Dictar» y «Comando de ejemplo» aparecen en filas
  distintas, con alturas y alineaciones que dificultan identificar las acciones.

## Decisión

- Reemplazar el nombre y botón permanente de salida por «Mi cuenta», con ícono
  de usuario, en la esquina superior derecha del encabezado.
- Un desplegable nativo `details`/`summary` presenta nombre y correo reales de
  la cuenta y la acción «Cerrar sesión», con ícono, tipografía y superficies
  Energy Orange. El nombre y correo pueden ocupar varias líneas dentro del panel.
- El control y la acción tienen un área táctil mínima de 44 px y foco visible.
  El menú se cierra al tocar fuera, pulsar Escape o elegir cerrar sesión;
  Escape devuelve el foco al control. Se conserva el guard de descarte del
  editor antes de finalizar la sesión y el comportamiento de autenticación.
- «Comando de ejemplo» queda a la izquierda y «Dictar» a la derecha en una
  misma fila con altura mínima compartida de 48 px. Se retira el rótulo
  «Ejemplos rápidos». Los textos pueden ajustarse dentro de su columna.
- En móvil (hasta 640 px), «Enviar» ocupa una fila completa debajo. En escritorio
  las tres acciones comparten fila bajo el campo de captura. Si no hay soporte
  de dictado, la ayuda ocupa la fila móvil y Enviar permanece debajo.
- La ayuda sigue abriendo el mismo diálogo; conserva sus bloqueos durante
  dictado/envío. No se cambia el reconocimiento de voz, el borrador ni la captura.

## Consecuencias y verificación

Solo modifica la presentación y apertura/cierre del menú de cuenta. No cambia
endpoints, persistencia, contratos ni reglas del dominio. Complementa ADR-0034
y ADR-0038. Verificar lint, pruebas frontend y build; las pruebas del menú cubren
la agrupación de cuenta, Escape con retorno de foco y cierre por toque exterior.
La comprobación visual final debe incluir el enlace de prueba en iPhone/Samsung.
