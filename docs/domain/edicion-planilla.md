# Especificación — Edición del entrenamiento (ADR-0027, ADR-0029 y ADR-0032)

## Interacción

- Botón «Editar entrenamiento» encima de la tabla, sin desplazamiento lateral.
  Visible solo en EnCurso/Lista y habilitado con filas y sin otra operación activa.
- Las acciones Editar entrenamiento/Agregar fila están en un bloque separado del
  panel de progreso por espacio y divisor; Eliminar todos tiene un grupo propio
  (ADR-0033). El progreso muestra el conteo real y el porcentaje, con transición
  de ancho y textura breve, respetando movimiento reducido. Al 100% indica «Todas
  las series completas», sin cambiar el estado de confirmación de la planilla.
- «Editar entrenamiento» abre todos los ejercicios colapsados, incluso si hay
  series incompletas. El alumno elige qué sección desplegar.
- La cuadrícula presenta tarjetas por ejercicio con nombre único en la cabecera
  y cantidad de series, sin repetir el nombre en las filas. La cabecera permanece
  fuera del desplazamiento horizontal de cada tabla. Se agrupa por nombre sin
  distinguir mayúsculas y espacios externos, en orden de primera aparición,
  conservando el orden de las series dentro del ejercicio y todos los datos.
  Esta agrupación también aplica al snapshot confirmado en modo de solo lectura.
- El número de serie y «Editar» activan la edición inline de esa fila en la
  cuadrícula, sin abrir el editor múltiple. El nombre se edita en un campo
  adicional de esa serie. Guardar usa el PUT individual y vuelve a agrupar con los
  datos recibidos; Cancelar descarta solo el borrador de esa fila.
- Editor de pantalla completa, agrupado por nombre de ejercicio normalizado
  (o crudo si falta). Cada sección es desplegable y contiene tarjetas por serie.
  Agrupar para mostrar no cambia la identidad ni los números guardados.
- Repeticiones y peso tienen teclado numérico/decimal. En el editor múltiple no
  hay checkbox «Al cuerpo». Una carga corporal existente se muestra como texto,
  se conserva si el peso no cambia y pasa a kg si se introduce un peso numérico.
  La edición inline y el alta mantienen su checkbox. Notas se despliega y muestra un
  resumen si tiene contenido. Ejercicio/número se editan en un detalle adicional.
- Campos de 16 px y controles de al menos 44 px. Solo scroll vertical; el área
  de contenido se ajusta al teclado mediante VisualViewport. Encabezado y pie
  permanecen visibles y respetan las áreas seguras de Android/iOS.
- Pie: Cancelar y Guardar cambios (N series). Sin cambios, guardar está desactivado.
  Durante el envío se bloquean acciones y salida interna para evitar duplicados.
- Cancelar o navegar con cambios abre un diálogo: Seguir editando / Descartar
  cambios. Recargar/cerrar el documento usa el aviso nativo del navegador cuando
  este lo permite; no hay persistencia local automática. La plataforma móvil puede
  finalizar el proceso sin permitir ese aviso.
- Voz detenida al entrar; encuesta pausada. Las respuestas de encuestas ya en
  vuelo se ignoran mientras el editor está abierto. Cancelar recarga la planilla.

## Modificación de varias filas

El editor múltiple permite modificar los campos de cada tarjeta y guardar todas
las filas cambiadas juntas. No hay botón, panel ni selección «Aplicar a varias
series». Editar una tarjeta no copia valores a otras. Las notas se pueden borrar
dejando vacío el campo de esa tarjeta; los datos no intervenidos se conservan.

## API

`PUT /api/planillas/sesion-actual/{alumnoId}/filas`

```json
{
  "sessionId": 12,
  "rows": [{
    "id": 34,
    "original": {
      "ejercicioId": null, "ejercicioCrudo": "Sentadilla hack", "nroSerie": 1,
      "reps": 12, "pesoKg": 40, "unidad": "Kg", "tiempoSeg": null,
      "comments": "aproximación", "observations": null
    },
    "values": {
      "ejercicioId": null, "ejercicioCrudo": "Sentadilla hack", "nroSerie": 1,
      "reps": 10, "pesoKg": 45, "unidad": "Kg", "tiempoSeg": null,
      "comments": "aproximación", "observations": null
    }
  }]
}
```

- Solo se envían filas modificadas. Cada `original` conserva exactamente los
  campos recibidos, incluida identidad histórica, unidad, tiempo y notas null.
  `values` reemplaza todos los campos editables de esa fila, igual que el PUT
  individual. No deducir datos ni borrar campos históricos que la UI no muestra.
- Lista no vacía, IDs únicos, originales y valores presentes; 400 si falla.
- Alumno inexistente: 404. Sin sesión editable, pausada, con aclaración viva,
  confirmada o ID de sesión obsoleto: 409. IDs ajenos/inexistentes o valores
  originales diferentes: 409, sin divulgar filas de otro alumno.
- Validaciones de valores y normalización compartidas con edición individual:
  nombre no vacío, ordinal entero >=1, reps null o >=1, peso null o >=0;
  kg/lb/al cuerpo y restricciones de ejercicios históricos. Notas opcionales.
- Toda validación termina antes de mutar entidades; una sola transacción y
  SaveChanges. Un fallo impide guardar cualquier fila de la solicitud.
- Éxito 200 con `RespuestaSesionActual`; actualiza actividad/RowVersion y limpia
  marcas de pendiente de las filas intervenidas. No altera CreadoUtc, numeración
  siguiente, orden de filas ni filas no incluidas. No cambia EnCurso/Lista.
- La comprobación de originales evita sobrescribir una fila cambiada desde otro
  cliente. Filas no incluidas pueden cambiar sin invalidar el guardado.
- Error de red/validación/conflicto conserva el borrador y muestra el problema.
  Puede volver a la planilla actual con confirmación de descarte. No hay reintento
  automático ni resolución automática de conflictos.

## Criterios de aceptación

**Ampliación ADR-0055:** alta, edición inline y editor múltiple incluyen el
control «RM / 1 repetición», visible fuera de Notas. `isOneRepMax` forma parte de
los valores originales y nuevos del contrato de edición. El reemplazo completo
usa false si no se envía. No cambia automáticamente reps/peso: una marca requiere
reps=1 y peso positivo en kg, sin corporal/tiempo; de lo contrario falla toda la
operación. Quitar la marca conserva los demás datos. Se congela al confirmar.

1. Corregir varias filas y enviar una única petición; se guardan todas juntas.
2. Una fila inválida, ajena, borrada o cambiada impide guardar el resto.
3. Sesiones no editables e historial confirmado rechazan edición masiva.
4. Editar una tarjeta no altera los campos de otras series.
5. No hay herramienta de valores compartidos ni checkbox corporal en el editor
   múltiple. Las cargas corporales existentes se conservan al editar reps/notas.
6. Cancelar no persiste; fallar el envío conserva los campos del borrador.
7. Una serie pendiente se puede completar sin crear otra ni modificar otros datos.
8. En anchos de 320/360 px no hay scroll horizontal y las acciones siguen accesibles.
9. «Editar» muestra campos inline y guarda solo esa fila, sin abrir otra vista.
10. Cada ejercicio aparece una vez como cabecera; sus filas conservan reps, peso,
    comentarios, observaciones y acciones. Agrupar no modifica datos persistidos.
11. En móvil el scroll horizontal queda dentro de cada tabla y su cabecera sigue
    visible. Botones y estados de confirmación usan Energy Orange (ADR-0032).
