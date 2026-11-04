# ADR 0008 — SPA Alumno · Planilla: entrada provisional, encuesta y decisiones de frontend

- **Estado:** Aceptado (2026-10-21)
- **Contexto:** arquitectura.md §6 (Vista alumno), ADR-0004 (S2/S4/S6/S7), ADR-0007
  (PlanillaApi mutaciones), `docs/conventions/angular21.md`

## S1 — Identificación provisional del alumno (ruta + localStorage)

> **S1 superada por ADR-0022:** el formulario de entrada por número fue retirado
> para la prueba actual. Se conserva este texto como historial de la decisión.

**Decisión:** hasta AuthApi (S6), el alumno se identifica con una pantalla
`/entrada` que pide el número de alumno, lo guarda en
`localStorage['wa.alumnoId']` y navega a `/alumno/:alumnoId/planilla`; la ruta
raíz redirige con una función `redirectTo` que lee ese valor.

- Cumple «no inventar datos»: nada de ID hardcodeado (ej. alumno 1); el id
  explícito es el que la API valida (404 si no existe).
- La vista de planilla NO cambia al llegar AuthApi: cambia solo el borde de
  identificación (cookie → el backend resuelve el alumno del token y el
  parámetro de ruta desaparece).
- [SUPUESTO] MVP de un solo alumno real: la entrada pide el id porque no hay
  aún endpoint de «alumnos» que listar.

## S2 — Encuesta (polling) cada 10 s con pausa local

**Decisión:** la cuadrícula se refresca sola: `GET sesion-actual` cada 10 s,
pausada cuando hay borrador local abierto (fila en edición, alta abierta,
acción en curso) o planilla confirmada. El refresco pisa estado: si encuesta
durante un borrador, sobrescribiría la edición.

- Suficiente para el MVP (el onfocus + WebSocket quedan post-MVP, arquitectura
  S7 ya deja la puerta abierta).
- La encuesta falla en silencio (el estado de error de carga ya está visible).
- `Sesion: null` después de confirmar hace que dejar de encuestar sea
  innecesario: el polling sigue pero solo re-lee (útil: si el alumno arranca
  una carga nueva por WhatsApp, la cuadrícula reaparece sola).

## S3 — Mutaciones repintan con la respuesta; sin refetch extra

**Decisión:** la API devuelve la `RespuestaSesionActual` refrescada en cada
mutación (ADR-0007) y la SPA la usa directamente (`sesion.set(respuesta)`);
solo `POST /confirmar` devuelve el snapshot (`PlanillaConfirmadaVista`), que
se guarda en su propia signal y congela la vista.

## S4 — Error handling del borde: ProblemDetails → `FallaApi`

**Decisión:** un helper `fallaDe(error: unknown): FallaApi` mapea
`HttpErrorResponse` a `{estadoHttp, titulo, detalle}` leyendo
`title`/`detail` del ProblemDetails; con estado 0 → «Sin conexión». Sin
librería de toasts: cada sección muestra su banner (`fallaCarga` para carga,
`fallaAccion` para mutaciones y validaciones locales).

## S5 — Borrado en dos pasos

**Decisión:** la fila se marca `filaEnConfirmacion` (primer click muestra
¿Borrarla?/No, segundo confirma). Sin `window.confirm` (regla convenciones: no
bloquear con diálogos nativos) ni librería UI.

## S6 — Validación local espejo del dominio (nunca inventar)

**Decisión:** la pantalla valida en cliente lo mismo que valida la API
(ADR-0007 S3): alta exige fila COMPLETA según el tipo del ejercicio (`usaPeso`
→ peso o «al cuerpo» + reps; `usaTiempo` → tiempo; el resto → reps); la
edición valida coherencia (peso/tiempo fuera de tipo se limpian al cambiar de
ejercicio) pero permite filas incompletas — es el flujo para completar lo que
el chat abandonó. Los errores locales no inventan valores: se muestran, no se
autocompletan.

- El peso SIEMPRE se envía en kg (la planilla es 100 % kg, ADR-0004 S6) — la
  conversión desde lb solo vive en el chat, que sí traduce los valores al
  anotarlos.
- Unidad: `Kg` cuando hay peso, `PesoCorporal` al cuerpo, `Tiempo` cuando hay
  segundos, null cuando no aplica.

## S7 — Sin librería de UI; tokens en CSS

**Decisión:** estilos propios con tokens globales en `styles.scss`
(`--panel`, `--acento` lima, `--violeta`, tipografías Space Grotesk / Geist /
JetBrains Mono por `<link>` en `index.html`). Component styles con unidades
coherentes (monospace para números de la cuadrícula). Motivo: un solo módulo
de UI (planilla) — una librería agrega peso y decisiones de tema ajenas al
diseño dark.

## S8 — Rutas con lazy loading + `withComponentInputBinding`

**Decisión:** cada vista carga con `loadComponent`; `PlanillaPage` recibe
`alumnoId` por `input.required<number>()` ligado al parámetro de ruta
(con `withComponentInputBinding()`). Un `effect` sobre la entrada re-carga
datos y rearma la encuesta si la navegación cambia de alumno sin remontar el
componente.

## S9 — Dev proxy en lugar de CORS

**Decisión:** `ng serve` pasa por `proxy.conf.json` a la API dev
(`http://localhost:5259`); en Development el backend NO redirige a HTTPS
(`Program.cs` gana un gate `IsDevelopment`) para que el proxy hable http
directo. Cero configuración de CORS y el build de prod sigue sirviendo rutas
relativas `/api/**` (mismo origen detrás de un reverse-proxy).
