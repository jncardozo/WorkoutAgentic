# ADR 0046 — Historial en panel modal

- **Estado:** Aceptado, solicitado por el usuario.
- **Fecha:** 2026-11-19.

## Contexto

El acceso «Historial» se confundía con las acciones secundarias. Su sección
inline desplazaba la captura y ocupaba espacio de trabajo al consultar sesiones.
El usuario aprobó un acceso destacado «Mis entrenamientos» y un panel superpuesto.

## Decisión

- `TrainingHistory` encapsula el botón y un `dialog` nativo: panel inferior hasta
  640 px y ventana centrada en escritorio. El encabezado permanece visible y la
  lista tiene scroll propio, altura limitada y márgenes de área segura en móvil.
- El botón muestra ícono de historial, contorno naranja, fondo cálido y contador
  de entrenamientos recibidos de la API. Durante carga o error no anuncia un
  conteo que pueda estar incompleto. No se agregan contadores simulados.
- La fecha es el dato principal de cada entrada; series y hora son secundarios,
  con número de planilla como referencia. La fila completa abre el flujo de
  reportes existente. Se conserva el orden descendente de confirmación.
- El modal se cierra mediante X, Escape o clic fuera. El diálogo nativo limita
  el foco a sus controles y vuelve al botón al cerrar. El cuerpo no se desplaza
  mientras está abierto; al cerrarlo recupera su posición y sus borradores.
- Se conservan los estados de carga, vacío y error; este último permite volver
  a consultar solo el historial. Volver de un reporte abre nuevamente la lista.
- Una transición breve presenta el panel. Movimiento reducido la desactiva.
- Sustituye la sección inline de ADR-0024, conservando su consulta de solo
  lectura, snapshots y acceso a reportes. No cambia autenticación ni la API.

## Verificación

Ejecutar lint, pruebas y build. Verificar apertura/cierre, foco, error y reintento,
orden y contador reales, preservación de borradores y retorno desde reportes.
Revisar móvil y escritorio, listas largas, scroll interno y área de dictado
sin desplazamiento al abrir y cerrar el panel.
