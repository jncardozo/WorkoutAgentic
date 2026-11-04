# Reglas de negocio — captura del alumno

> El pipeline (código + IA) DEBE cumplirlas. Reglas marcadas **[SUPUESTO]** = mi
> suposición más simple y razonable; confirmarlas en la sesión de arquitectura.
> Las reglas del generador de rutinas (progresión, deload) quedan al final como
> **Fase 2** — fuera del alcance del MVP de captura por voz.

## 1. Flujo de captura en la SPA

El alumno escribe o dicta en el navegador. El reconocimiento de voz ocurre en
la web y la API recibe únicamente el texto final mediante
`POST /api/capturas/{alumnoId}/texto` (ADR-0018). El texto pasa por el NLU y la
sesión de captura aplica las reglas de interpretación de ADR-0013.

- El alumno puede iniciar carga dictando o escribiendo en español. Todo entra al
  mismo pipeline con texto crudo. Los mensajes es-419/"gym español"
  coloquiales son válidos ("cargá", "anoté", "hice").
- **Regla base del dictado (ADR-0013)**: un mensaje de carga es
  `EJERCICIO + SERIES` ("press de banca 4 series") o
  `EJERCICIO + SERIE PUNTUAL` ("sillón, tercera serie"). Crea las filas con
  ejercicio y número de serie SOLAMENTE; los datos quedan `null` y todo dato
  extra que el alumno diga en este paso se ignora.
- **Regla de datos por serie (ADR-0014)**: `Nº DE SERIE + REPS y/o PESO`
  ("primera serie 12 repeticiones con 20 kilos", "segunda serie con 30",
  "tercera serie 10 al cuerpo"), SIN nombrar ejercicio. Llena la fila de ese
  ordinal del **LOTE VIVO** (las filas del último paso 1 creado, que comparten
  `CreadoUtc` — incluye lotes de texto libre resueltos como «otro»; la SPA lo
  muestra en «Estás cargando»): solo se escriben los valores dichos, repetir
  un valor lo sobrescribe (último dictado gana) y si la fila de esa serie no
  existe en el lote se avisa — jamás se crea la fila. Peso: kg por defecto,
  "en libras" se convierte a kg (×0,45359237), "al cuerpo" marca
  `PesoCorporal`. El TIEMPO se retiró del MVP: ya no se dicta ni se pide.
- **Regla para avanzar de ejercicio (ADR-0020)**: cada serie debe tener
  repeticiones y peso antes de iniciar el siguiente ejercicio. El peso puede ser
  una carga numérica en kg o «al cuerpo». Reps y peso se pueden completar en
  mensajes separados; mientras falte alguno, el sistema rechaza el nuevo lote y
  señala cada serie y campo pendiente. La misma regla se aplica al alta manual:
  una fila nueva debe guardarse completa. Editar la fila actual y completar sus
  valores sigue permitido.
- Regla madre: **no inventar**. El NLU normaliza el nombre del ejercicio a
  texto libre; solo pregunta por la variante cuando es realmente ambigua y lo
  hace sin opciones cerradas. No hay preguntas de peso/reps/tiempo:
  el dato llega por dictado (regla de datos) o por edición de la SPA, con
  `null` explícito mientras tanto (nunca un `null` disfrazado de dato real).
- **Fuera de la regla** (series sin ejercicio, ejercicio sin series, datos
  sin ordinal o sin filas en la sesión): respuesta de guía única, SIN tocar
  el estado ni crear filas. Con aclaración viva de ejercicio, un dictado de
  datos SE APLICA igual al lote vivo y la pregunta queda viva sin reintento
  (ADR-0014, enmienda 2026-10-23 por feedback real). Si el análisis llega con
  `Exito = false` (LLM caído: cuota 429, red, salida corrupta), el mensaje
  puede ser válido — solo no se pudo LEER: responde «No pude leer bien esos
  datos 🙂 ¿Me los pasás de nuevo?» SIN reintento, SIN abandono y sin tocar
  filas (ADR-0014).
- El cierre explícito ("terminé", "listo", "ya está") pasa la sesión a `LISTA`.
  El alumno también puede confirmar directamente desde la SPA cuando termina de
  cargar; la confirmación acepta sesiones `EnCurso` o `Lista` sin aclaraciones
  pendientes y congela la cuadrícula (ADR-0017).
- **[SUPUESTO]** Inactividad 2 h → estado `EXPIRADA` con aviso al alumno ("Tu carga
  quedó pausada. Escribí 'seguir' para continuarla."); los parciales quedan guardados
  y se pueden reanudar con "seguir".

## 2. Planilla y confirmación (SPA)

- La planilla en curso muestra los `RegistroParcial` en cuadrícula: ejercicio, serie,
  repeticiones y peso. **La columna/campo TIEMPO se retiró de la cuadrícula y de
  los formularios (ADR-0014)** — el dato sigue existiendo a nivel API pero ya no
  se captura ni exige. Se puede editar, eliminar filas individuales, eliminar
  todos los registros parciales de la sesión y agregar filas antes de confirmar
  (ADR-0015).
- Confirmar desde una sesión `EnCurso` o `Lista` crea `PlanillaConfirmada`
  (snapshot inmutable + timestamp) y bloquea los parciales. Requiere al menos una
  fila y ninguna aclaración pendiente. El cierre explícito también está disponible, pero
  no es requisito para confirmar en la SPA (ADR-0017). Editar filas a mano
  después de confirmar NO está permitido en el MVP **[SUPUESTO]**: corregir
  implica crear una planilla nueva con nota de corrección.

- La planilla confirmada queda congelada para el alumno que la confirmó.
- Después de confirmar, el alumno puede iniciar otra carga con la acción
  explícita «Nuevo entrenamiento». Esta acción limpia la vista local, no borra
  el snapshot ni crea una sesión vacía en la base; la nueva sesión se crea con
  el primer mensaje válido. Las planillas confirmadas anteriores permanecen
  consultables en el historial de solo lectura.
- Desde la vista confirmada, el alumno puede descargar el snapshot como Excel
  (`.xlsx`) o PDF, o enviarlo como adjunto a un correo y formato que elija.
  Exportar y enviar siempre usa los datos congelados de esa planilla; no lee los
  parciales editables ni permite alterarla (ADR-0019). El envío requiere SMTP
  configurado por el operador; si falta, la SPA informa que no está disponible.

## 3. Alcance de cuentas

- En este sprint solo se mantiene el flujo del alumno: abrir su planilla, capturar,
  editar, eliminar y confirmar registros. No hay interfaz ni endpoints del módulo
  de profesor (ADR-0018).

## 4. Reglas del pipeline (interpretación)

- Etapas separadas: texto reconocido en el navegador → intención → entidades → sesión.
  La API no recibe audio ni ejecuta STT.
- Entidades faltantes → regla de aclaración (ver skill `nlu-espanol` y spec NLU §0):
  - **Única pregunta viva: la variante del ejercicio cuando es ambigua**. El
    nombre se normaliza como texto libre y no depende de catálogo ni de una
    lista de apodos; la aclaración es abierta (ADR-0016).
  - No se abre una aclaración de chat para peso o reps: llegan por la regla de
    datos del paso 2 (ADR-0014) o por edición de la SPA. Si faltan, se bloquea
    el ejercicio siguiente hasta que estén completos (ADR-0020).
  - Dictado fuera de la regla → guía que enseña ambas reglas; nada se crea
    ni se pregunta por números sueltos.
- Solo el alumno decide cuándo cerrar o confirmar la cuadrícula. La aplicación no
  consolida una sesión automáticamente.

## 5. Límites (no negociables)

- Sin consejos médicos ni farmacológicos; si preguntan, derivar a profesional.
- Sin promesas de resultados.
- Los mensajes al alumno: español neutral, tono cercano, sin tecnicismos de sistema
  (no hablar de "intents", "JSON", "IDs").

---

## Fase 2 (fuera del MVP) — Reglas del generador de rutinas

> Se activarán cuando el producto agregue el agente generador; quedan documentadas
> como referencia. Ver historial de ADR para alcance.

1. **Recolección de perfil antes de generar**: nivel, objetivo, días/minutos,
   equipamiento, red flags de salud (dolor agudo/condición médica → derivar a
   profesional antes de prescribir).
2. **Prescripción**: rangos de reps por objetivo (fuerza 3–6, hipertrofia 6–12,
   conditioning 12–20) **[PENDIENTE confirmar]**; RIR 3–4 principiantes, nunca
   fallo a novatos; descansos 2–5 min fuerza / 1.5–3 min hipertrofia / 45–90 s
   aislamiento.
3. **Progresión**: doble progresión (reps→carga), +2.5% superiores / +5% inferiores,
   máximo un escalado semanal por ejercicio; deload en la semana propuesta.
4. **Ajustes por feedback**: dolor → bajar/sustituir; "muy fácil" → un escalón, nunca
   dos.
5. **Comunicación**: sin consejos médicos, sin promesas de resultados, cargas como
   estimación con RIR guía.
