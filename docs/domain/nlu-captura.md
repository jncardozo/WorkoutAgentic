# Spec NLU — Captura de entrenamientos en español (Voice Tracker MVP)

> Fuente de verdad para la interpretación. Toda implementación debe ajustarse a este
> spec; los casos nuevos se agregan con `/nlu-case` y quedan como fixtures de test.

## 1. Intents

| Intent | Trigger típico | Efecto en el estado |
|---|---|---|
| `CARGA_EJERCICIO` | Nombra un ejercicio sin series | Fija `ejercicioActivo`, serie interna → 1 |
| `CARGA_SERIE` | Datos de una serie (e/ reps/ peso) | Agrega `RegistroParcial` y avanza contador |
| `CIERRE` | "terminé", "listo", "ya está", "eso es todo" | Consolida parciales → sesión `LISTA` |
| `AYUDA/CONSULTA` | "cómo hago para…", "qué puedo decir" | Respuesta con guía, NO modifica estado |
| `INVALIDO` | No es captura (saludos, spam, otros temas) | Respuesta de cortesía + reinvitación |

Prioridad de decidir: con la sesión abierta, un mensaje con entidades de carga mapea
`CARGA_SERIE` aunque no repita el ejercicio. "Quiero cargar un ejercicio nuevo"
cambia de ejercicio → `CARGA_EJERCICIO`.

## 2. Entidades (con normalización)

| Entidad | Normalización | Ejemplos → salida |
|---|---|---|
| `ejercicio` | Matcheo contra catálogo (nombre canónico + apodos) | "sillón" → `Sillón de cuadríceps`; "el pecho" → [?] pedir aclarar cuál ejercicio de pecho |
| `serie` | integer ordinal | "primera", "la una", "2°", "tercer" → 1, 1, 2, 3 |
| `repeticiones` | integer | "doce", "12", "una docena" → 12 |
| `peso` | decimal (kg default) | "45", "45 kilos", "40 y medio", "kilos y medio" → 45, 45, 40.5, 1.5** |
| `unidad` | kg / lb / peso_corporal / tiempo | "en la máquina con 45" → kg; "al cuerpo" → peso_corporal; "1 minuto de plancha" → tiempo=60s |
| `descanso` | segundos [opcional] | "descanso de un minuto" → 60 |

**⚠️ "kilos y medio" = 1.5 kg SOLO si no hay número previo; "45 y medio" = 45.5.
La interpretación se resuelve por contexto inmediato (número previo + "y medio"/"y cuarto").

## 3. Formas de mensaje frecuentes (rioplatense/español neutro)

- Verbos de carga: "cargá / hacé / anotá / registrá / anotame / sumá / metele"
  (voseo) y sin voseo: "carga", "anota", "registra", "toma nota".
- Expresiones de peso: "con 45", "a 45", "45 kilos", "dos platos",
  "un disco de 10" (→ [SUPUESTO] disco = 10 kg).
- Series sin explícito: "3x12 con 40" → 3 series de 12 reps a 40 kg → se generan 3
  registros (o se genera la fila "3×12×40" [DECISIÓN: granularidad de registro]).
- Correcciones: "no, la segunda fue con 50" → actualiza fila 2, NO agrega nueva.
- Cierres: "terminé/listo/ya/eso es todo/no cargo más". ✅ lista no exhaustiva:
  ver corpus.

## 4. Reglas de ambigüedad y aclaración

- Falta peso y ejercicio de fuerza → preguntar PESO ("¿Con cuánto peso tomaste
  la serie?"). Si el ejercicio es peso corporal (plancha, dominadas) no preguntar.
- Ejercicio no matcheado (score < umbral) → 2-3 sugerencias del catálogo
  ("¿Te referís a Sillón de cuadríceps o a Prensa?") + opción "otro" (texto libre).
- Números vagos ("cincuenta y pico", "unos 40") → pedir valor exacto.
- Doble entidad ambigua ("12 con medio") → luego de pedir, confirmar lectura
  ("12 reps con 12.5 kg, ¿correcto?").
- Si la aclaración sigue sin resolver tras 2 reintentos → registrar fila con
  `R.pendienteAclaracion=true` y seguir con lo demás.

## 5. Estado de sesión y memoria

La memoria conversacional vive en `SesionCaptura` (persistida):

```
SesionCaptura {
  alumnoId, estado: EN_CURSO|ESPERANDO_ACLARACION|LISTA|CONFIRMADA|EXPIRADA,
  ejercicioActivoId?, ordenSerieSiguiente,
  parciales: [{ ejercicioId|textoCrudo, serie, reps, peso, unidad, pendiente? }],
  ultimaAclaracion?: { datoFaltante, reintento, mensajeIdWhatsapp }
}
```

- Cada mensaje del alumno resuelve: valida la aclaración pendiente SI hay una,
  sino procesa el intent correspondiente.
- Datos sensibles: los audios/textos son del alumno → no enviarlos a proveedores
  externos sin revisar el ADR de privacidad; transcripciones guardadas se asocian
  a alumno para auditoría.

## 6. Corpus de frases (fixtures de test — set mínimo del MVP)

```
"hola chat, necesito que me cargues el siguiente ejercicio: sillón de cuadríceps,
 primera serie, 12 repeticiones con 45 kg"
 → CARGA_EJERCICIO+SERIE | sillón de cuadríceps, serie 1, reps 12, peso 45 kg

"cargame 3 series de prensa de 12 con 60"
 → CARGA_EJERCICIO+SERIES ×3 | prensa, 12 reps, 60 kg cada una

"segunda serie, 10 con 40"
 → CARGA_SERIE | ejercicioActivo, serie 2, reps 10, peso 40 kg

"anotame el pecho, la máquina con pulley, 3x12"
 → ejercicio [?] aclarar cuál del catálogo de pecho (press en máquina,
   cruces, etc.); reps 12 ×3 series

"hice remos con 50 y pico"
 → [?] pedir valor exacto ("¿Fue 50, 52.5, 55?")

"plancha 1 minuto"
 → CARGA_SERIE | plancha, unidad tiempo, 60 s

"kilos y medio en cada mano"
 → sin contexto numérico previo → [?] confirmar lectura ("¿1.5 kg por mano?")

"terminé, listo"
 → CIERRE

"profe, ¿cómo hago el remo con mancuernas?"
 → AYUDA/CONSULTA (no modifica estado)
```

Mantené este corpus como tests parametrizados del pipeline; cada caso real que
aparezca en producción se suma como fixture (variantes regionales: voseo rioplatense,
"placa" = disco en México, "mancuernas" = pesas, etc.).
