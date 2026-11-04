# ADR 0012 — Reapertura de la sesión `LISTA` por captura en la PWA

- **Estado:** Aceptado (2026-10-22)
- **Contexto:** ADR-0004/S (la sesión `LISTA` devolvía «planilla ya armada» a todo
  mensaje, sin abrir carga nueva — supuesto heredado del canal WhatsApp, donde el
  bot invitaba a ir a la SPA), ADR-0009 (pivot: captura por texto/voz en la PWA),
  reglas de negocio §2 (cuadrícula editable en `EnCurso` **y** `Lista`)

## Decisión

Con la sesión vigente en `LISTA`, un mensaje nuevo **reabre la carga** (estado
`EN_CURSO`) y se aplica con el flujo normal solo si trae **carga nueva**
(`CARGA_EJERCICIO`/`CARGA_SERIE`) o es **«seguir»**:

- «seguir» responde la cortesía de retoma (`Seguimos`/`SeguimosConPendiente`),
  conservando la aclaración viva que hubiera.
- Un intent de carga aplica las filas; el cierre siguiente vuelve a dejar la
  carga en `LISTA` («terminé» sigue siendo la única palabra de cierre).
- Ayuda, cortesía, inválidos y análisis fallido **no** reabren: responden la
  invitación a confirmar («Tu planilla ya está armada…») sin tocar estado.

## Motivos

1. Después del pivot PWA (ADR-0009), el dictado es el flujo principal. Que la
   sesión `LISTA` rechace todo texto dejaba al alumno en un callejón sin salida
   documentado: la captura «no hacía nada» y la carga no arrancaba.
2. La cuadrícula de la SPA YA es editable en `LISTA` (alta/manual, editar,
   eliminar según reglas §2 y ADR-0007): el pipeline era el único que trataba el
   estado como congelado. Se unifica el criterio.
3. No se abren sesiones nuevas mientras la anterior espera confirmación: la
   confirmación (`ConfirmarAsync`, ADR-0007 S4) opera sobre la sesión hablante,
   y sesiones `LISTA` huérfanas serían inconfirmables.

## Consecuencias

- `SesionCapturaServicio.NucleoAsync` reabre `LISTA` según intent; el resto del
  pipeline no cambia.
- Fixture en `docs/domain/nlu-captura.md` §5 y tests de estado en
  `SesionCapturaServicioTests` (`CargaSerieConSesionLista_ReabreYAplica`,
  `SeguirConSesionLista_ReabreSinTocarFilas`,
  `AyudaConSesionLista_SigueInvitandoAConfirmar`).
- Caso abierto para el spec (no cubierto por este ADR): valores por serie
  condicionales en lote (ej. «4 series…, primera serie 40 kg») — hoy el lote es
  uniforme (ADR-0004); el peso de un lote con «primera/última» queda como caso
  NLU pendiente de resolución.
