# ADR 0010 — Vista Profesor solo lectura + alta de alumnos explícita

- **Estado:** Aceptado (2026-10-22)
- **Vigencia:** El módulo de profesor se retiró en ADR-0018; este ADR queda como
  historial de la iteración anterior.
- **Contexto:** arquitectura §7 (Vista Profesor), reglas de roles de
  `business-rules.md` §4, ADR-0009 (pivot PWA: sin canal de alta automática),
  ADR-0008 (identidad provisional hasta AuthApi S6)

## S1 — Alta explícita de alumnos: `POST /api/alumnos`

**Decisión:** el alta de una cuenta de alumno es una acción explícita del
profesor (o del seed de demo), con `Nombre` y `TelefonoWa` opcional. Sin canal
habilitado, ya no existe «alta por primer contacto».

- Nombre obligatorio (trim, ≤120): es la etiqueta de todas las listas.
  Falla → 400 ProblemDetails «Nombre requerido».
- Teléfono opcional: si viene, se canoniza por dígitos
  (`WhatsappPhone.Canonizar`, 6..20 dígitos) — 400 «Teléfono inválido» si la
  forma no sirve; si no viene, queda **NULL** (regla madre: no inventar datos;
  el índice único trata los NULL por separado, igual que `Email`).
- Éxito → 201 Created con la vista de cuenta (`AlumnoVista`).
- El endpoint vive en `Features/Alumnos` (escritura de cuentas).

## S2 — Esquema: migración `AlumnoNombreAndOptionalPhone`

**Decisión:** dos cambios en `Alumno`, ambos no destructivos:

1. `Nombre` nueva columna no-null con default `''` (los alumnos históricos del
   flujo WhatsApp no tenían nombre: la vista muestra `#id` como fallback).
2. `TelefonoWa` pasa a **nullable**: era obligatoria porque era la identidad
   del canal; con el pivot es un atributo opcional. Los NULL no chocan entre
   sí en el índice único. Si WhatsApp volviera, el flujo del webhook sigue
   creando alumnos con teléfono no-null garantizado (canonización previa).

## S3 — Namespaces: `/api/alumnos` (escribir) vs `/api/profesores/*` (leer)

**Decisión:** la escritura de cuentas y la lectura del profesor van en
grupos separados para acotar el contrato que AuthApi (S6) protegerá con
cookie + rol PROFESOR:

- `GET /api/profesores/alumnos` → listado con resumen
  (`AlumnoProfesorVista`: nombre, teléfono opcional, cantidad confirmadas y
  última fecha), ordenado por nombre.
- `GET /api/profesores/alumnos/{alumnoId}/planillas` → historial completo de
  `PlanillaConfirmadaVista` del alumno, la más nueva primero. 404
  «Alumno inexistente» si la cuenta no existe.
  [SUPUESTO] El MVP asume un único profesor: el listado no filtra por
  `ProfesorId` hasta que existan invitaciones/login.

## S4 — Solo lectura (regla de roles)

**Decisión:** el slice profesor jamás consulta ni escribe sesiones
(`SesionCaptura`) ni parciales (`RegistroParcial`): la lectura es del
snapshot confirmado (`PlanillaConfirmada` + filas congeladas), mapeado al
MISMO shape que ya consume la vista Alumno. El profesor recibe exactamente lo
que el alumno confirmó.

## S5 — SPA: ruta `/profesor`

**Decisión:** vista lazy `ProfesorPage` con tres estados: listado de alumnos
(tabla con resumen + «Ver planillas» habilitado solo si hay confirmadas),
alta inline (nombre + WhatsApp opcional; al guardar entra ordenada y se abre
su historial) y el historial por alumno (una tarjeta por planilla con la
misma cuadrícula read-only y los badges de la vista Alumno). La entrada del
alumno suma el link «¿Sos el profesor?», y el encabezado de la planilla
muestra el nombre del alta (fallback `#id`).

- La encuesta de la planilla sigue siendo solo del rol alumno; el profesor no
  tiene polling (los confirmados no cambian: snapshot write-once).
