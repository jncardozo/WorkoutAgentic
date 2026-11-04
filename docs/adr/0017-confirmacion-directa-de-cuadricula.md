# ADR 0017 — Confirmación directa de la cuadrícula desde la SPA

- **Estado:** Aceptado (2026-10-24)
- **Vigencia:** La confirmación y el snapshot siguen vigentes. La referencia a
  disponibilidad para el profesor quedó fuera del alcance en ADR-0018.
- **Contexto:** la confirmación existente requería que el alumno dictara
  «terminé» para pasar la sesión a `Lista`. Al finalizar una jornada desde la
  cuadrícula, esto obligaba a volver al panel de captura antes de confirmar.

## Decisión

- La SPA permite confirmar una cuadrícula con filas cuando la sesión está en
  `EnCurso` o `Lista`, siempre que no haya una aclaración pendiente.
- La API conserva las validaciones existentes: alumno y sesión válidos,
  cuadrícula no vacía, sin aclaraciones pendientes y sesión no pausada ni ya
  confirmada.
- La confirmación crea el mismo snapshot inmutable y deja la planilla en solo
  lectura. El cierre por dictado («terminé») sigue disponible como transición a
  `Lista`, pero ya no es requisito para confirmar desde la SPA.
- La interfaz muestra la acción debajo de la cuadrícula y explica que al
  confirmar queda congelada y visible para el profesor.

## Consecuencias

- El alumno puede terminar y confirmar la jornada directamente desde el lugar
  donde revisa sus ejercicios.
- Sesiones `Expirada` o `EsperandoAclaracion` siguen sin poder confirmarse;
  deben reanudarse o resolver su aclaración primero.
- La planilla vacía continúa sin confirmación para no crear snapshots sin
  contenido.
