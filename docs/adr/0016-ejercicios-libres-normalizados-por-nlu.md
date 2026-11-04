# ADR 0016 — Ejercicios de texto libre normalizados por NLU

- **Estado:** Aceptado (2026-10-24)
- **Contexto:** ADR-0002 S2 y ADR-0003 S4/S5 usaban un catálogo cerrado y un
  resolver léxico determinístico. Una variante válida como «Press de banca con
  mancuernas» no estaba sembrada, por lo que el resolver la rechazaba y
  ofrecía opciones semánticamente incorrectas. El alumno puede describir el
  mismo movimiento con expresiones distintas.

## Decisión

- El NLU normaliza el nombre del ejercicio a texto libre en español, sin exigir
  que exista en un catálogo ni asignar un `EjercicioId`. Formas equivalentes
  como «Press de banca con mancuernas» y «Banco plano con mancuernas» deben
  compartir la etiqueta normalizada.
- `RegistroParcial.EjercicioCrudo` conserva la frase del alumno y se agrega
  `RegistroParcial.EjercicioCanonico` para la etiqueta normalizada. La
  migración agrega una columna nullable; las filas históricas conservan sus
  relaciones y texto actuales.
- Si el NLU no puede determinar la variante, el sistema formula una pregunta
  abierta, sin sugerencias enumeradas ni resolver contra catálogo. La respuesta
  del alumno vuelve a pasar por NLU.
- La SPA permite escribir nombres libres al agregar o editar filas. El catálogo
  deja de ser requisito en la captura y en la cuadrícula.

## Consecuencias

- El nombre normalizado depende de la salida del NLU; el texto crudo conserva
  la evidencia literal para revisión y auditoría.
- El catálogo y sus filas históricas permanecen en la base para compatibilidad,
  pero ya no deciden la identidad de ejercicios dictados ni aparecen como
  opciones obligatorias en la cuadrícula.
- La ambigüedad se resuelve preguntando al alumno, no eligiendo una alternativa
  por similitud lexical.

## Alternativas descartadas

- Agregar cada frase nueva como apodo de catálogo: exige mantenimiento continuo
  y sigue fallando ante variantes nunca sembradas.
- Guardar únicamente la frase literal: no permite unificar expresiones
  equivalentes para la lectura de la planilla.
