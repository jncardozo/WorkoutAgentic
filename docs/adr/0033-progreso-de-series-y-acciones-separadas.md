# ADR 0033 — Progreso de series y acciones separadas

- **Estado:** Aceptado (2026-11-11), propuesta aprobada por el usuario.

## Contexto

En móvil, las acciones de edición quedaban demasiado cerca del progreso y la
barra fina se confundía con un separador decorativo.

## Decisión

- El encabezado de sesión contiene el ejercicio activo y un panel de progreso.
  Debajo hay un bloque de acciones separado por espacio y un divisor. Editar
  entrenamiento y Agregar fila se apilan en móvil y comparten fila cuando hay
  espacio. Eliminar todos queda en un grupo independiente y conserva su confirmación.
- Un componente de presentación muestra el conteo de series y el porcentaje
  calculados por la página a partir de los datos reales: reps y carga completas,
  incluyendo peso corporal. La regla de completitud existente no cambia.
- La barra tiene un riel contrastante de 16 px, relleno naranja y transición de
  ancho de 450 ms. Una textura animada breve al aparecer refuerza su identidad,
  sin simular incrementos ni una petición en curso. Al completar se muestra
  «Todas las series completas» y se retira la textura. Esto no confirma la planilla.
- Se conserva role=progressbar con mínimo, máximo, valor y descripción del conteo.
  prefers-reduced-motion desactiva animación y transición. Sin filas no se muestra.

## Consecuencias

Solo cambia la presentación de la sesión. No modifica endpoints, datos, NLU ni
estados de negocio. El alumno sigue confirmando la planilla explícitamente.
