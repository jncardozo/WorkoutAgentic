# ADR 0013 — Regla única de captura: `EJERCICIO + SERIES` (lote base)

- **Estado:** Aceptado (2026-10-23)
- **Contexto:** ADR-0002/S2 (NLU = LLM JSON Schema + resolver determinístico),
  ADR-0004 (sesión de captura, lote por serie, aclaraciones), ADR-0009/0011
  (captura por dictado en la PWA), ADR-0012 (reapertura de `LISTA`). El dictado
  libre fallaba en producción: frases largas con datos por serie
  («sentadillas hack 4 series y 12 repeticiones, primera serie 40 kg») daban
  interpretaciones erráticas del LLM (ejercicio mal etiquetado, datos cruzados,
  filas de más). El usuario lo pidió explícitamente: «el chat de voz no está
  funcionando correctamente… agreguemos restricciones».

## Decisión

El dictado sigue UNA regla fija, en un solo mensaje:

```
EJERCICIO + SERIES        →  "press de banca 4 series"   ⇒ N filas (1..N)
EJERCICIO + SERIE PUNTUAL →  "sillón, tercera serie"     ⇒ 1 fila con ese ordinal
```

Efecto (puerta determinística `EsCapturaBase` en `SesionCapturaServicio`,
AUNQUE el LLM etiquete mal el intent — tolerancia al ruido de voz):

1. **Filas solo con ejercicio + número de serie.** Repeticiones, peso, unidad
   y tiempo SIEMPRE `null`, aunque se digan (se extraen como `null` por
   prompt y el servicio los ignora igual). Se completan después — el tema
   peso queda pendiente de definir con el usuario.
2. **Numeración**: ejercicio nuevo ⇒ 1..N; ejercicio ya cargado ⇒ continúa
   desde `OrdenSerieSiguiente`. La serie puntual usa su ordinal.
3. **Tope** `MaxSeriesLote = 30` [SUPUESTO]: una cantidad mayor no crea filas
   (ruido de voz), responde la guía.
4. **Fuera de la regla** (series sin ejercicio — p. ej. «3x12 con 60» —,
   ejercicio sin series, o datos sueltos): respuesta de guía única
   (`TextosBot.FueraDeRegla`) **sin crear filas, sin tocar estado y SIN el
   fallback al ejercicio activo** (eliminado). Tampoco existe la pregunta
   «¿cuántas series?» aislada: la guía pide la forma completa.
5. **Única aclaración viva: ejercicio** (ambiguo/sin match → filas pendientes
   + sugerencias + «otro»; 2 reintentos → abandono con `DescripcionPendiente`,
   igual que ADR-0004). La resolución del ejercicio confirma el lote SIN
   preguntar faltantes.
6. **Reapertura `LISTA` (acota ADR-0012)**: solo reabre un dictado EN REGLA
   (o «seguir»); el dictado fuera de la regla responde la guía y deja la
   planilla esperando su confirmación.

## Motivos

1. El dictado libre exigía que el LLM resuelva demasiadas entidades por
   mensaje; cada campo extra era una nueva oportunidad de error. La regla
   deja al LLM solo dos decisiones difíciles (¿es carga? ¿qué texto es el
   ejercicio?) y el resto es determinístico.
2. La regla madre «no inventar» queda más fuerte: no se aceptan datos
   ambiguos; los faltantes no se conjeturan ni se piden por voz — quedan
   `null` visibles y marcados (`Incompleta` en el snapshot) para completarse
   por otro paso.
3. Eliminar el fallback al activo quita la clase de bugs donde una serie
   suelta se atribuía al ejercicio equivocado.

## Consecuencias

- `AplicarCargaAsync`/`ContinuarLoteAsync` reescritos como lote base; se
  eliminaron las aclaraciones de peso/reps/tiempo/serie y sus respuestas
  (`PreguntaPor`, `DetalleSerie`, textos `PreguntaPeso/Reps/Tiempo`,
  `SeriesAnotadas`, `RegistroConfirmado`, `EjercicioAnotado(porTiempo)`).
- `CampoFaltante` sobrevive SOLO para la SPA: el alta de fila completa en la
  cuadrícula y el snapshot que marca `Incompleta` (ADR-0004/0007 sin cambios).
- Prompt NLU reescrito (regla única, datos de serie siempre `null`, sin
  mención a WhatsApp — coherente con ADR-0009).
- Spec NLU reescrito (§0 regla, corpus nuevo); reglas de negocio §1/§4
  actualizadas. El caso abierto «peso distinto por serie en el lote» queda
  cerrado por esta regla (el dictado no lleva peso); la etapa de peso será
  un paso de producto a definir.
- Suite: `SesionCapturaServicioTests` reescrita (lote base, fuera de regla,
  datos extra ignorados, tope, aclaración de ejercicio, cierre/reapertura);
  `CapturaTextoEndpointTests` cubre lote base + guía desde el endpoint.
