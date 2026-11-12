# ADR 0027 — Editor de entrenamiento y guardado conjunto

- **Estado:** Aceptado (2026-11-10). Diseño aprobado por el usuario.
- **Actualización:** ADR-0029 restaura «Editar» inline y retira la selección
  compartida y el checkbox «Al cuerpo» del editor múltiple. El diseño siguiente
  conserva el historial de la propuesta original; rige la spec enlazada al final.
- **Contexto:** en móvil, las acciones de la tabla quedan fuera de la vista y
  requieren desplazamiento horizontal para editar cada serie.

## Decisión

La cuadrícula sigue siendo la vista de consulta. «Editar entrenamiento», encima
de la tabla, abre un editor de pantalla completa con series agrupadas por
ejercicio y tarjetas verticales. Tocar el nombre de una fila o «Editar» abre ese
mismo editor en su serie. Por ajuste aprobado por el usuario, la apertura desde
«Editar entrenamiento» muestra todos los ejercicios colapsados; solo la apertura
desde una fila despliega su ejercicio y destaca esa serie.
Notas y datos de identidad se despliegan para conservar una vista compacta.

«Aplicar a varias series» selecciona filas de un ejercicio y modifica únicamente
los campos elegidos en el borrador. Las notas ofrecen Conservar/Reemplazar/Borrar.
El pie fijo contiene Cancelar y Guardar cambios con el número de series editadas.
Se avisa antes de salir con cambios, también al navegar o recargar el documento.
Se detiene la voz y se pausa el refresco de filas mientras el editor está abierto.

El nuevo PUT `/api/planillas/sesion-actual/{alumnoId}/filas` valida las filas y su
pertenencia a la sesión antes de aplicar cambios. Usa una transacción y una única
persistencia, sin llamar repetidamente al PUT individual. Cada cambio incluye
los valores originales para detectar escrituras obsoletas (409). La respuesta
exitosa es la sesión actual refrescada. No cambia el esquema de datos.

Solo se permite editar sesiones EnCurso o Lista. No se crean filas, no se altera
su fecha de creación y no se modifica un snapshot confirmado. Reps y peso pueden
seguir pendientes; ADR-0020 mantiene el bloqueo del siguiente ejercicio.

## Documentos vigentes

El contrato y la aceptación están en [edicion-planilla.md](../domain/edicion-planilla.md).
Esta decisión amplía ADR-0007/0008; su descripción histórica de edición inline
queda sustituida por el editor para las acciones visibles de la SPA.
