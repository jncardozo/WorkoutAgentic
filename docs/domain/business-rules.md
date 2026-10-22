# Reglas de negocio — Voice Tracker MVP

> El pipeline (código + IA) DEBE cumplirlas. Reglas marcadas **[SUPUESTO]** = mi
> suposición más simple y razonable; confirmarlas en la sesión de arquitectura.
> Las reglas del generador de rutinas (progresión, deload) quedan al final como
> **Fase 2** — fuera del alcance del MVP de captura por voz.

## 1. Flujo de captura (por texto en la PWA — ADR-0009)

> **Pivot:** la captura activa es por texto/dictado en la PWA
> (`POST /api/capturas/{alumnoId}/texto`); el canal WhatsApp quedó
> deshabilitado. Las reglas de interpretación y de aclaración NO cambian: el
> texto crudo entra al mismo pipeline. Las referencias a WhatsApp de esta
> sección describen el flujo histórico del chat (canal deshabilitado).

- El alumno puede iniciar carga dictando o escribiendo en español. Todo entra al
  mismo pipeline con texto crudo. Los mensajes es-419/"gym español"
  coloquiales son válidos ("cargá", "anoté", "hice").
- Regla madre: **no inventar**. Si falta peso, serie, reps o el ejercicio no matchea
  el catálogo con confianza, el bot pregunta. Nunca guarda con `null` implícito que
  parezca un dato real.
- Cada mensaje asume contexto: si ya hay ejercicio activo en `SesionCaptura` y el
  alumno solo dice "segunda serie, 10 con 40", se atribuye al ejercicio activo.
- El cierre explícito ("terminé", "listo", "ya está") consolida los parciales y pasa
  la sesión a `LISTA` → el bot invita a revisar la planilla en la SPA (link).
- **[SUPUESTO]** Inactividad 2 h → estado `EXPIRADA` con aviso al alumno ("Tu carga
  quedó pausada. Escribí 'seguir' para continuarla."); los parciales quedan guardados
  y se pueden reanudar con "seguir".

## 2. Planilla y confirmación (SPA)

- La planilla en curso muestra los `RegistroParcial` en cuadrícula: ejercicio, serie,
  repeticiones, peso (y unidad). Se puede editar, eliminar y agregar filas antes de
  confirmar.
- Confirmar crea `PlanillaConfirmada` (snapshot inmutable + timestamp) y bloquea los
  parciales. Editar filas a mano después de confirmar NO está permitido en el MVP
  **[SUPUESTO]**: corregir implica crear una planilla nueva con nota de corrección.
- La planilla confirmada es visible para:
  - el alumno (dueño) y
  - el profesor asignado, en **solo lectura**.

## 3. Roles y accesos

- Rol `ALUMNO`: crea/edita sus sesiones de captura y confirma SU planilla. Ve
  únicamente sus planillas.
- Rol `PROFESOR`: ve (solo lectura) las planillas confirmadas de los alumnos
  vinculados a él. No edita planillas en el MVP.
- Vinculación alumno↔profesor: **[SUPUESTO]** el profesor genera un código/invitación
  desde la SPA y el alumno lo acepta (primera vez desde WhatsApp o SPA).
- WhatsApp ↔ SPA: la identidad del alumno es la misma (mismo `AlumnoId`). **[SUPUESTO]**
  el primer contacto vía WhatsApp registra al usuario por su número de teléfono; el
  login de la SPA se vincula a ese alumno mediante invitación del profesor o código.

## 4. Reglas del pipeline (interpretación)

- Etapas separadas: transcripción → intención → entidades → sesión. Sin atajos.
- Transcripción: se guarda el texto crudo (con timestamps de logs) para auditoría de
  desvíos; errores de STT → pedir reenvío de la nota de voz, nunca adivinar.
- Entidades faltantes → regla de aclaración (ver skill `nlu-espanol` y spec NLU):
  - Peso no mencionado → preguntar peso (kg por defecto **[SUPUESTO]**).
  - Ejercicio no reconocido → ofrecer hasta 3 sugerencias del catálogo; si no hay
    match, guardar como texto con `R.descripcionPendiente=true` y continuar.
  - Números ambiguos ("cincuenta y pico") → pedir número exacto.
- Solo el alumno decide el cierre. El bot puede sugerir cerrar si pasaron X minutos
  sin mensajes, pero no consolida solo.

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
