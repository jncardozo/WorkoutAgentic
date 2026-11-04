# Diseño — prototipo Stitch

Fuente visual: prototipo generado en **Stitch** (proyecto «Voice Workout Tracker
SPA»), capturado como `prototipo-stitch.html` en esta carpeta. La SPA de
`src/web` replica su diseño 1 a 1.

## Qué se corregió al portar (ADR-0009)

El prototipo salió de una iteración anterior que nombraba al canal WhatsApp,
deshabilitado desde ADR-0009. En ADR-0018 se retiraron del producto la integración de WhatsApp y la vista de profesor. Correcciones aplicadas en la SPA (los códigos del
HTML original se conservan como referencia en `prototipo-stitch.html`):

| Texto del prototipo | Corrección en la SPA | Motivo |
|---|---|---|
| «Tu entrenamiento, por WhatsApp» (sub de marca) | «tu entrenamiento, por voz y texto» | ADR-0009/0018: captura en la SPA |
| «Bot sincronizado (+54 9 11 …)» (chip del header) | se omite (inventaba datos); la meta muestra «última actividad» y «Alumno #N» reales | Regla madre: no inventar datos |
| «Actualizar datos desde WhatsApp» (title del botón) | «Actualizar datos desde la app» | ADR-0009/0018 |
| Procedencia «Voz WhatsApp» por fila | columna omitida: la API no expone origen por fila | No inventar datos |
| Toast «Sincronizando últimas notas de voz de WhatsApp…» | «Fila agregada…» / «Fila eliminada…» / «Planilla confirmada…» según la acción real | ADR-0009 |

## Mapeo prototipo → SPA

| Bloque del prototipo | Codificación en Angular |
|---|---|
| Header sticky + blur + marca | `src/app/app.html` / `app.scss` |
| Título «Planilla de …» + badge de estado con pulso | `features/planilla/planilla-page.html` (`.encabezado-planilla`) |
| Panel «Dictá tu entrenamiento» con glow lima | `planilla-page.html` (`.panel-captura`, `.glow`) |
| Cuadrícula «Sesión #N» editable | `planilla-page.html` (`.panel-sesion`, `table.cuadricula`) |
| Barra «Confirmar planilla» (gradiente lima→esmeralda) | `planilla-page.html` (`.barra-confirmar`, `.btn-confirmar`) |
| Toast esquina inferior derecha | `planilla-page.ts` (`toast` signal) + `.toast` |
| Tokens de color/tipografía | `styles.scss` (`--bg #080c10`, `--panel #0f141c`, `--borde #1e2633`, lima `#a3e635`, esmeralda `#22c55e`/`#34d399`) |

Tipografías del prototipo: **Inter** (texto/títulos) + **JetBrains Mono**
(números de la cuadrícula), cargadas en `index.html`. Esto reemplaza a
Space Grotesk/Geist (mención previa del AGENTS.md); si se quiere revertir,
crear ADR.

## Divergencias conscientes (dominio > prototipo)

- No se portan los datos simulados del prototipo (RPE «9.5 (Fallo)», cargas
  progresivas, «Día 1», «Rutina Hipertrofia A»): el modelo real no los tiene.
- Las filas del prototipo eran estáticas; en la SPA siguen siendo **editables
  inline** (borradores ADR-0007) con la misma apariencia.
- La columna «Origen» no se portó (sin dato en la API); si el dominio la quiere,
  primero agregar `origen` a `FilaParcialVista`/snapshot.

