---
description: Agrega un caso de interpretación (NLU) al spec con su entrada esperada y sus entidades
---

Suma el caso de NLU "$ARGUMENTS" al spec del Voice Tracker:

1. Lee `docs/domain/nlu-captura.md` y ubica la sección correspondiente al intent del
   caso (carga de ejercicio, carga de serie, cierre, ayuda/consulta, fuera de spec).
2. Documenta el caso con este formato:
   - **Entrada** (frase coloquial exacta, entre comillas).
   - **Intent esperado** + **entidades** (ejercicio, serie, repeticiones, peso, unidad)
     con su valor normalizado; entre `[?]` lo que el sistema DEBE aclarar.
   - **Respuesta del bot** si es incompleto/ambiguo (derivación a aclaración).
   - **Variantes regionales** equivalentes (rioplatense/latinoamericano) si aplica.
3. Agrega la entrada como test de fixture si el mensaje ya existe en la lista de tests
   sugeridos del spec (sección "Corpus de frases").
4. Si el caso contradice una regla vigente de `docs/domain/business-rules.md`, NO lo
   agregues: señala el conflicto y pregúntame antes de tocar el spec.
