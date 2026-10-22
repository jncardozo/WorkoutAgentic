# Glosario del dominio — WorkoutAgentic

> Terminología canónica. Los códigos, clases y mensajes de UI deben usar estos
> conceptos sin ambigüedad.

## Conceptos centrales

| Término | Definición |
|---|---|
| **Rutina** | Plan de entrenamiento de un cliente, con duración (semanas) y objetivo. |
| **Sesión** | Un día de entrenamiento dentro de una rutina (ej. "Empuje – lunes"). |
| **Planilla** | Cuadrícula resumen de las cargas registradas en la sesión de entrenamiento (ejercicio, serie, reps, peso). Se visualiza en la SPA. |
| **Ejercicio** | Movimiento del catálogo (ej. Sentadilla con barra). Tiene nombre canónico, apodos y grupos musculares. |
| **Serie** | Bloque de repeticiones de un ejercicio con carga y descanso asignados. |
| **Repeticiones (reps)** | Veces que se ejecuta un movimiento en una serie. |
| **Carga (peso)** | Resistencia usada en kg. En ejercicios de peso corporal, el progreso es por variantes/leverage. |
| **Descanso** | Tiempo de recuperación entre series de un mismo ejercicio. |
| **RIR** | "Reps In Reserve": repeticiones que quedarían en el tanque al terminar la serie. RIR 2 = parar 2 reps antes del fallo. |
| **RPE** | Esfuerzo percibido 1–10 (RIR 2 ≈ RPE 8). Escala de intensidad del agente. |
| **1RM** | Máximo teórico para una repetición; sirve para calcular cargas (evitar pedir 1RM real a novatos). |
| **Volumen** | Series efectivas por grupo muscular por semana. Métrica de dosis principal. |
| **Frecuencia** | Veces por semana que se entrena un grupo muscular o patrón. |

## Captura por voz (Voice Tracker MVP)

| Término | Definición |
|---|---|
| **SesionCaptura** | Estado del diálogo con un alumno (hoy por dictado en la PWA, ADR-0009): ejercicio activo, última serie cargada, registros parciales y estado (EN_CURSO, ESPERANDO_ACLARACION, LISTA, CONFIRMADA, EXPIRADA). |
| **RegistroParcial** | Fila de datos capturada y no confirmada (ejercicio, serie, reps, peso) — puede corregirse/eliminarse; nunca sale tal cual al profesor. |
| **PlanillaConfirmada** | Snapshot inmutable de la planilla que el alumno aprobó; visible para su profesor (vista solo lectura, ADR-0010). |
| **Aclaración** | Respuesta del bot que pide el dato faltante/ambiguo (ej. "¿A qué peso tomaste esa serie?"); pausa el flujo hasta recibir respuesta. |
| **Transcripción** | Texto en español generado por STT a partir de la nota de voz; se guarda junto al audio original. |
| **Intent** | Intención detectada del mensaje: CARGA_EJERCICIO, CARGA_SERIE, CIERRE, AYUDA/CONSULTA, INVALIDO. |
| **Catálogo de ejercicios** | Tabla canónica con nombre normalizado + apodos coloquiales ("sillón", "el pecho", "press en máquina") para el matcheo de entidades. |
| **Alumno** | Cuenta con rol ALUMNO que el profesor da de alta de forma explícita (ADR-0010: nombre obligatorio, WhatsApp opcional); dicta su entrenamiento en la PWA y confirma planillas. |
| **Profesor** | Cuenta con rol PROFESOR: da de alta alumnos y consulta sus planillas confirmadas (solo lectura). |
| **Vinculación** | Relación alumno↔profesor (asignación); define a quién ve el profesor. Inicia por invitación del profesor [SUPUESTO: ver reglas]. |

## Gestión del cliente

| Término | Definición |
|---|---|
| **Cliente** | Usuario final que conversa por WhatsApp y entrena. |
| **Nivel** | Principiante / intermedio / avanzado. Define velocidad de progresión y complejidad de ejercicios. |
| **Objetivo** | Hipertrofia, fuerza, acondicionamiento general, pérdida de grasa, movilidad. |
| **Perfil** | Datos del cliente: nivel, objetivo, equipamiento disponible, días/hora por sesión, historial y lesiones. |
| **Historial** | Registro de sesiones completadas: adherencia, cargas, RPE reportado. |

## Progresión

| Término | Definición |
|---|---|
| **Sobrecarga progresiva** | Incremento gradual y planificado de demanda (carga, reps o series). |
| **Progresión doble** | Aumentar reps dentro de un rango; al topear, subir carga y reiniciar reps. |
| **Deload** | Semana de descarga con volumen/intensidad reducidos (~50–60%) para recuperar. |
| **Mesociclo** | Bloque de 4–6 semanas con un foco; termina con deload y reevaluación. |
| **PR (record personal)** | Mejor marca registrada por ejercicio (peso×reps). |
