# ADR 0057 — Confirmación informada de incompletas y vista confirmada compacta

- **Estado:** Aceptado (2026-11-24), solicitado tras pruebas de QA.
- **Amplía:** ADR-0017, ADR-0032 y ADR-0054.

## Contexto

La vista «Planilla confirmada» duplicaba las tarjetas de ejercicios/series que
ya están disponibles desde los reportes. En móvil ocupaban espacio antes de las
acciones. Además, el popup de confirmación era igual para una planilla completa
y una incompleta: el alumno podía congelar faltantes sin advertencia específica.

## Decisión

- La vista confirmada conserva estado/fecha, acceso a PDF/Excel, envío por correo,
  «Nuevo entrenamiento» y aviso de guardado en el historial. Retira las tarjetas
  de ejercicios y su cuadrícula duplicada. Las filas del snapshot permanecen
  intactas y consultables en reportes, vistas previas y adjuntos, también desde
  el historial. La cuadrícula editable de la captura conserva su comportamiento.
- «Confirmar planilla» mantiene el popup nativo accesible de ADR-0054. Si hay
  filas incompletas, muestra «Tu planilla tiene datos incompletos», una lista de
  ejercicio, número de serie y campos pendientes, y recuerda la inmutabilidad.
- Las acciones de ese aviso son «Volver a editar» y «Confirmar de todos modos».
  Volver/Escape cierran el aviso sin POST y conservan filas y borrador de texto.
  El alumno sigue en su cuadrícula editable y elige cómo completar los datos.
  El foco inicial queda en la salida; los controles se bloquean durante el envío.
- Con datos completos se conservan «¿Terminaste tu entrenamiento?»,
  «Sí, confirmar planilla» y «Cancelar», sin advertencia de faltantes.
- Una fila está incompleta para confirmar si tiene descripción de ejercicio
  pendiente o le faltan repeticiones/peso, según la misma regla usada para el
  indicador `Incompleta` del snapshot. «Al cuerpo» cuenta como carga declarada.
  Comentarios, observaciones y RM son opcionales; su ausencia no produce aviso.
  Las validaciones existentes de un RM marcado siguen siendo obligatorias.
- `POST /api/planillas/confirmar` agrega `acceptIncomplete` (bool, false por
  defecto). La SPA envía true únicamente desde el aviso de datos incompletos.
  El servidor vuelve a comprobar las filas actuales; sin aceptación devuelve
  409 ProblemDetails con los pendientes y no modifica sesión ni crea snapshot.
  Esto también protege frente a clientes antiguos o solicitudes directas.
- La aceptación permite congelar la planilla incompleta con sus null y marcas
  `Incompleta`, sin inventar valores. No permite confirmar sesiones vacías,
  expiradas o con aclaración viva, ni omitir las demás validaciones.
- La SPA registra sesión y contenido de las filas al abrir el popup. Si un
  refresco cambia lo revisado antes de enviar, cierra el aviso y solicita revisar
  la planilla nuevamente. No transfiere una aceptación anterior a otro estado.
- No se modifica el esquema de base de datos ni los snapshots históricos.

## Verificación

- Vista confirmada sin tarjetas/tablas duplicadas y con todas sus acciones.
- Aviso con serie/campos pendientes, salida sin POST y preservación de borrador.
- Confirmación explícita con `acceptIncomplete=true`; completas sin ese opt-in.
- Cambio de filas con el popup abierto exige una nueva revisión.
- API rechaza faltantes con aceptación ausente o false sin congelar la sesión;
  aceptación true conserva los faltantes del snapshot. Peso corporal y notas
  vacías no requieren aceptación de incompletitud.
- Accesibilidad del diálogo, error/reintento y bloqueo de doble envío.
