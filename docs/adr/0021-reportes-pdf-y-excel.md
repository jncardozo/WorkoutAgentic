# ADR 0021 — Diseño y cálculos de los reportes PDF y Excel

- **Estado:** Aceptado (2026-10-26)
- **Contexto:** los archivos descargables de planillas confirmadas deben tener
  una presentación imprimible y consistente con los mockups de Stitch. El modelo
  actual conserva el snapshot confirmado, pero no datos de firma, hash ni
  auditoría criptográfica.

## Decisión

- PDF y Excel usan el diseño claro orientado a impresión A4: cabecera verde,
  resumen, detalle de series y navegación/paginación adecuada al formato.
- Ambos formatos se generan exclusivamente desde `PlanillaConfirmada`; no se
  consultan parciales ni se modifica el snapshot.
- Los reportes omiten nombres y otros datos personales del alumno.
- El resumen puede mostrar cantidad de series, carga máxima numérica y volumen
  calculable. El volumen es la suma de `repeticiones × PesoKg` para filas que
  tienen ambos valores y no son `PesoCorporal`. Si no hay valores calculables,
  se muestra “Sin datos”; no se inventa un cero.
- El progreso visual compara cargas numéricas registradas con la carga máxima
  de esa planilla. Peso corporal y datos faltantes se identifican sin asignarles
  una carga.
- Las marcas de filas incompletas se conservan. No se agregan firmas digitales,
  hashes, folios externos ni estados de auditoría que el dominio no persiste.
- Se conserva la generación de archivos en la API y los endpoints existentes;
  no hay cambio de esquema ni de contrato de la SPA.

## Consecuencias

- Los cálculos de PDF y Excel comparten una misma fuente y coinciden entre sí.
- Los datos de demostración que aparecen en Stitch no se trasladan. En
  particular, los totales visuales del mockup no son fuente de verdad: el
  volumen se recalcula desde las filas confirmadas.
- La progresión de carga representa cargas numéricas por fila y no equivale a
  una recomendación de entrenamiento.
