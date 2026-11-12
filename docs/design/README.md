# Diseño — prototipo Stitch

Fuente visual: prototipo generado en **Stitch** (proyecto «Voice Workout Tracker
SPA»), capturado como `prototipo-stitch.html` en esta carpeta. El HTML conserva
el diseño histórico. La SPA de `src/web` usa la variante **Energy Orange** y
tarjetas por ejercicio aprobadas en ADR-0032.

## Diseño vigente — Energy Orange (ADR-0032)

- Acento primario `#ff6a00`, hover `#ffab66`, activo `#e85d00`, texto oscuro
  sobre botones. Se mantienen las superficies oscuras y las tipografías actuales.
- Estados de éxito: naranja `#ff9340`, texto claro `#ffb780`/`#ffd0ad` y fondo
  `#24170f`. «Lista para confirmar», «Tu carga quedó cerrada», progreso, botones,
  editor y reportes usan esta familia. Errores y advertencias mantienen su semántica.
- Marca existente con adaptación cromática CSS; no cambia su geometría.
- **Data Tables:** agrupar elementos repetitivos en tarjetas con una cabecera
  unificada, sin repetir el ejercicio en cada fila. Cabecera con nombre y cantidad
  de series; tabla con Serie, Reps, Peso, Comentarios, Observaciones y acciones
  cuando corresponde. Las cabeceras quedan fuera del scroll lateral de la tabla.
- Agrupación por nombre sin distinguir mayúsculas ni espacios externos. Orden de
  primera aparición de ejercicios y orden original de series dentro de cada uno.
  No se alteran registros ni numeración. Aplica a sesión actual y vista confirmada.
- «Editar» o tocar el número abre edición inline; el nombre de esa serie se
  corrige en un campo adicional. «Editar entrenamiento» conserva el editor
  múltiple con ejercicios colapsados. Ver `docs/domain/edicion-planilla.md`.

## Progreso y acciones (ADR-0033)

La sesión separa progreso y acciones (ADR-0033): panel con conteo, porcentaje y
riel de 16 px; debajo, espacio y divisor antes de Editar entrenamiento/Agregar
fila. Eliminar todos tiene su propio grupo. El relleno cambia suavemente con los
datos reales y muestra una textura animada breve; al llegar al 100% indica «Todas
las series completas». Movimiento reducido desactiva ambas animaciones. Completar
series no confirma la planilla. El componente vive en `training-progress.*`.

## Ayuda de captura (ADR-0034)

«Ejemplos rápidos» contiene una única card «💡 Comando de ejemplo». Abre un
popup de lectura con el ejercicio Sillón de cuádriceps y cuatro series, separadas
en párrafos. Un aviso explica que se deben completar todas antes de pasar al
siguiente ejercicio y relaciona esa completitud con el 100% de progreso.

La ayuda no modifica el borrador ni envía texto. Se cierra mediante la X, un toque
fuera o Escape. Conserva foco modal, devuelve el foco a la card y permite scroll
interno en pantallas pequeñas. Implementación: `command-example.*`.

## Íconos y arranque de la app instalada (ADR-0035)

La instalación usa el símbolo plano `public/brand/app-icon-orange.svg`, naranja
`#ff6a00` sobre fondo opaco `#080c10`. Las esquinas y máscaras las aplica el SO.
Se entregan PNG normales y maskable separados de 192/512 px, apple-touch-icon de
180 px y favicons de 32/48 px más favicon.ico. Los maskable mantienen el símbolo
dentro del círculo seguro de radio 40% del ancho. No usar el PNG verde histórico
ni el filtro CSS del encabezado para representar la app instalada.

El manifiesto define nombre, identidad, entrada `/`, modo standalone y colores
oscuros. Android genera el arranque con esos metadatos; Safari gestiona su propio
lanzamiento y dispone del ícono Apple. El HTML ya es oscuro antes de cargar la
SPA. No se agrega una pantalla de carga artificial ni caché offline.

El enlace al manifiesto usa `crossorigin="use-credentials"` para incluir la sesión
del navegador cuando se prueba mediante un túnel privado (ADR-0036). Validar
también desde la URL HTTPS del túnel con sesión activa: un HTTP 200 que contiene
la página de login no es un manifiesto válido. No cambiar la privacidad del puerto
como parte de una corrección visual o de descarga del manifiesto.

Para regenerar los archivos desde Windows, ejecutar desde `src/web`:

```powershell
./scripts/generate-app-icons.ps1
```

Tras publicar, verificar una instalación nueva en Android/iOS reales, incluyendo
el recorte del ícono, arranque y entrada a la planilla. El navegador de escritorio
permite verificar recursos y metadatos; no sustituye la prueba del launcher móvil.

## Qué se corrigió al portar (ADR-0009)

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
| Panel «Dictá tu entrenamiento» con glow naranja | `planilla-page.html` (`.panel-captura`, `.glow`) |
| Cuadrícula «Sesión #N» editable por tarjetas de ejercicio | `planilla-page.html` (`.exercise-card`, `table.cuadricula`) |
| Barra «Confirmar planilla» (gradiente naranja) | `planilla-page.html` (`.barra-confirmar`, `.btn-confirmar`) |
| Toast esquina inferior derecha | `planilla-page.ts` (`toast` signal) + `.toast` |
| Tokens de color/tipografía | `styles.scss` (`--bg #080c10`, `--panel #0f141c`, `--borde #1e2633`, naranja `#ff6a00`) |

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

