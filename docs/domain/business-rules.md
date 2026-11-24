# Reglas de negocio — captura del alumno

> El pipeline (código + IA) DEBE cumplirlas. Reglas marcadas **[SUPUESTO]** = mi
> suposición más simple y razonable; confirmarlas en la sesión de arquitectura.
> Las reglas del generador de rutinas (progresión, deload) quedan al final como
> **Fase 2** — fuera del alcance del MVP de captura por voz.

## 1. Flujo de captura en la SPA

El alumno escribe o dicta en el navegador. El reconocimiento de voz ocurre en
la web y la API recibe únicamente el texto final mediante
`POST /api/capturas/{alumnoId}/texto` (ADR-0018). El texto pasa por el NLU y la
sesión de captura aplica las reglas de interpretación de ADR-0025.

- El alumno puede iniciar carga dictando o escribiendo en español. Todo entra al
  mismo pipeline con texto crudo. Los mensajes es-419/"gym español"
  coloquiales son válidos ("cargá", "anoté", "hice").
- **Dictado de varias series (ADR-0025)**: ejercicio + cantidad de series crea
  filas con los valores comunes y particulares expresados. Una sola frase puede
  contener repeticiones, pesos distintos, comentarios y observaciones por serie.
  El flujo de crear filas vacías y completarlas después sigue disponible.
- **Actualizaciones por ordinal (ADR-0025)**: una o varias series con datos/notas
  actualizan filas existentes. Sin nombre usan el lote vivo; con ejercicio exigen
  coincidencia inequívoca de nombre y ordinal. Ninguna actualización crea filas.
  Se valida todo antes de aplicar: un destino inexistente o ambiguo impide cambios
  parciales. Los valores omitidos no borran datos previos.
- **Notas distintas**: Comentarios almacena el propósito de la serie (aproximación,
  entrada en calor, calentamiento, efectiva); Observaciones almacena sensaciones
  e incidentes del alumno. Son opcionales y nunca se deducen. Se conservan al
  confirmar, consultar el historial y exportar PDF/Excel.
- **Regla para avanzar de ejercicio (ADR-0020)**: cada serie debe tener
  repeticiones y peso antes de iniciar el siguiente ejercicio. El peso puede ser
  una carga numérica en kg o «al cuerpo». Reps y peso se pueden completar en
  mensajes separados; mientras falte alguno, el sistema rechaza el nuevo lote y
  señala cada serie y campo pendiente. La misma regla se aplica al alta manual:
  una fila nueva debe guardarse completa. Editar la fila actual y completar sus
  valores sigue permitido.
- Regla madre: **no inventar**. El NLU normaliza el nombre del ejercicio a
  texto libre; solo pregunta por la variante cuando es realmente ambigua y lo
  hace sin opciones cerradas. Si un valor o su alcance es ambiguo, se pide
  aclararlo sin aplicar el mensaje; si simplemente falta, el dato llega por dictado (regla de datos) o por edición de la SPA, con
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

- **Edición de entrenamiento (ADR-0027):** la grilla es la vista de consulta y
  ofrece «Editar» inline por fila y «Editar entrenamiento» en un editor vertical
  por ejercicio. Este último permite corregir tarjetas y guardarlas juntas, sin
  selección de valores compartidos ni checkbox corporal (ADR-0029).
  Se editan únicamente los valores intervenidos; borrar notas es explícito. Los
  borradores no se persisten hasta guardar y se conservan si falla el envío.
  Cambios concurrentes se informan sin sobrescribirlos. Ver el contrato y la
  interacción en [edicion-planilla.md](edicion-planilla.md).

- La planilla en curso muestra los `RegistroParcial` en tarjetas por ejercicio,
  con nombre único en la cabecera y filas con serie, repeticiones, peso,
  comentarios y observaciones (ADR-0032). La agrupación es visual; el detalle
  confirmado se consulta desde los reportes y no se duplica en la pantalla de
  confirmación (ADR-0057). No cambia datos ni el orden persistido. **La columna/campo TIEMPO
  se retiró de la cuadrícula y de los formularios (ADR-0014)** — el dato sigue existiendo a nivel API pero ya no
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
- Antes de confirmar, la SPA pide aceptación explícita mediante un popup con
  «Sí, confirmar planilla» y «Cancelar». Cancelar o Escape conserva series y
  borradores. La advertencia aclara que las series confirmadas no son editables.
- Si la planilla tiene repeticiones/peso faltantes o ejercicio por aclarar, el
  popup detalla ejercicio, serie y campos pendientes. Ofrece «Volver a editar»
  (sin guardar) o «Confirmar de todos modos» (aceptación explícita). La API exige
  `acceptIncomplete=true`; ausente/false devuelve 409 sin cambiar la sesión.
  «Al cuerpo» es carga declarada y las notas/RM son opcionales. Se conservan los
  null e indicadores de incompleta al confirmar con aceptación (ADR-0057).
- La vista confirmada muestra estado/fecha y acciones de reportes, envío y nuevo
  entrenamiento. El detalle de ejercicios/series queda en los reportes y sus
  vistas previas; no se elimina del snapshot ni del historial (ADR-0057).
- El historial admite un nombre opcional editable (hasta 120 caracteres, una
  línea) sin cambiar series ni fecha. Sin nombre muestra «Entrenamiento #N».
  Nombre, fecha, cantidad de series, hora de confirmación local y número de
  planilla identifican cada entrada (ADR-0054).
- El alumno puede eliminar un entrenamiento propio del historial tras un
  popup de confirmación. El borrado es lógico: conserva internamente snapshot,
  filas y sesión `Confirmada`; excluye la planilla de la lista, contador y
  nuevas consultas, exportaciones, envíos y renombrados. No se ofrece restaurar.
  No reabre ni cambia otra carga. Archivos/correos ya descargados o iniciados
  siguen existiendo. Los controles exigen cuenta, acuerdo, propiedad y CSRF.
- Después de confirmar, el alumno puede iniciar otra carga con la acción
  explícita «Nuevo entrenamiento». Esta acción limpia la vista local, no borra
  el snapshot ni crea una sesión vacía en la base; la nueva sesión se crea con
  el primer mensaje válido. Las planillas confirmadas anteriores que no se
  hayan eliminado permanecen consultables en el historial; sus series son de
  solo lectura, con nombre y eliminación disponibles según ADR-0054.
- Desde la vista confirmada, el alumno puede descargar el snapshot como Excel
  (`.xlsx`) o PDF, o enviarlo como adjunto a un correo y formato que elija.
  Exportar y enviar siempre usa los datos congelados de esa planilla; no lee los
  parciales editables ni permite alterarla (ADR-0019). El envío requiere SMTP
  configurado por el operador, o un proveedor HTTPS (ADR-0051); si falta, la SPA
  informa que no está disponible.
- El envío también está disponible al consultar una planilla histórica, desde
  un único acceso centrado en reportes y en el detalle de PDF/Excel (ADR-0052). Identifica la planilla por
  fecha y número, exige destinatario y acción explícita y no altera la carga
  actual. El formulario bloquea envíos simultáneos y conserva los datos ante error.
- Antes de habilitar el formulario se comprueba que el operador configuró un
  proveedor de correo. La ausencia de configuración se informa sin pedir datos
  de envío ni simular un resultado satisfactorio. Comprobar configuración no
  garantiza aceptación del proveedor ni entrega en bandeja de entrada.

## 3. Alcance de cuentas

- Antes de acceder a capturas o planillas, toda cuenta debe aceptar explícitamente
  la versión vigente del acuerdo de confidencialidad y condiciones de prueba
  (ADR-0049). La aceptación se guarda por cuenta en el servidor junto con el texto
  exacto y fecha UTC; leer el acuerdo, registrarse o iniciar sesión no lo acepta.
  Un cambio del documento requiere nueva aceptación. Se puede rechazar y cerrar
  sesión, y consultar o descargar el acuerdo desde la SPA.

- Cada alumno se registra explícitamente con nombre, correo y contraseña
  (ADR-0038). La cuenta de acceso y el alumno se crean juntos, sin apropiarse de
  datos históricos. La API exige sesión y verifica propiedad de sesiones,
  registros y reportes en todas las operaciones. No se elige identidad por ID.
- La SPA ofrece login, cierre de sesión y recuperación de contraseña. El cierre
  revoca las sesiones de la cuenta en todos los dispositivos. El registro de
  prueba es abierto y no exige verificación de email; no implica email verificado.

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
  - Las ambigüedades de peso, reps o serie se preguntan en la SPA sin aplicar
    el mensaje ni abrir otra aclaración persistida. Los datos simplemente ausentes
    llegan por dictado (ADR-0025) o por edición de la SPA. Si faltan, se bloquea
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

## 6. RM declarado y comparación histórica (ADR-0055)

- Cualquier ejercicio con carga externa puede tener una serie marcada como RM.
  Es una declaración explícita de máximo para una repetición: requiere reps=1,
  peso positivo normalizado a kg y ausencia de tiempo/peso corporal. No se estima
  desde varias reps ni se deduce por ser la mayor carga o una serie efectiva.
- Marca independiente de notas, revisable en la SPA antes de confirmar.
  Captura contradictoria pide aclaración sin cambiar filas; edición inválida
  rechaza la operación completa. Los datos históricos no reciben marcas inferidas.
- Solo RM completos y confirmados aportan referencias futuras. Al confirmar otra
  sesión, se elige el mayor RM de planillas anteriores del mismo alumno/ejercicio
  y no posteriores a su confirmación. Empates: fecha más reciente, mayor ID de
  planilla y menor orden de fila. Se ignoran mayúsculas y espacios redundantes,
  sin equiparar variantes ni sinónimos por aproximación.
- Congelar referencia (peso, fecha UTC de confirmación, planilla y fila de origen)
  junto con las series. La fecha del reporte se presenta en Argentina; no se
  deduce una fecha retroactiva del entrenamiento a partir del texto.
- Eliminar una planilla excluye sus RM de comparaciones nuevas. Los reportes
  ya confirmados mantienen la referencia utilizada aunque se elimine el origen.
- Reportar por ejercicio la máxima carga de sesión / RM previo ×100 con un
  decimal, sin limitar a 100 %. Sin referencia o sin carga numérica no inventar
  porcentajes. Un menor porcentaje no demuestra menor fuerza: pueden cambiar
  las repeticiones u otros factores. PDF, Excel, vistas previas y adjuntos usan
  los mismos datos congelados. Excel incluye la hoja «Progreso RM».

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
