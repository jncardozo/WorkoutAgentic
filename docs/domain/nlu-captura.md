# Spec NLU — Captura de entrenamientos en español

> Fuente de verdad vigente. ADR-0025 amplía ADR-0013/0014: se pueden crear y
> completar varias series en un solo mensaje. Los ADR anteriores son historia.

## 0. Regla madre

La edición manual individual y masiva ocurre fuera del NLU, según
[edicion-planilla.md](edicion-planilla.md) (ADR-0027). No se reinterpreta el texto
original al corregir filas. La voz se detiene al abrir el editor y los cambios
manuales conservan el lote vivo y sus fechas de creación.

Nunca inventar datos. Transcripción, intención, entidades y aplicación son etapas
separadas. El alumno revisa el texto y la cuadrícula antes de confirmar. Fallo del
LLM → pedir reenvío, sin alterar filas, estados o reintentos de aclaración.

## 1. Intents y rutas

- CARGA_EJERCICIO: ejercicio + cantidad de series (1..30), con o sin datos.
  Crea N filas. Ejercicio + ordinal sin datos conserva el alta puntual histórica.
- CARGA_SERIE: ordinal(es) + reps, peso, comentarios u observaciones. Actualiza
  filas existentes, incluso si nombra el ejercicio; nunca crea filas implícitas.
- CIERRE: «terminé», «listo», «ya está». Pasa a Lista; abandona aclaración viva.
  «Cuarta y última serie» sigue siendo una carga, no es un cierre.
- AYUDA: consulta sin datos. INVALIDO: saludo u otros temas. No cambian filas.

El contenido de entidades decide entre alta y actualización, no solo la etiqueta
propuesta por el LLM. Sin ejercicio o cantidad/ordinal para un alta: guía, sin filas.

## 2. Entidades y alcance de valores

| Entidad JSON | Significado |
|---|---|
| ejercicio | Nombre literal del alumno; null si no lo menciona |
| ejercicio_normalizado | Nombre libre estándar, sin catálogo ni variantes inventadas |
| cantidad_series | Cantidad explícita 1..30; null para actualizaciones |
| serie | Ordinal puntual; null cuando hay cantidad_series |
| repeticiones, peso, unidad | Datos comunes explícitos de un alta, o de una única actualización histórica |
| comentarios | Tipo/propósito dicho: aproximación, entrada en calor, calentamiento, efectiva |
| observaciones | Sensaciones o incidentes expresados, sin diagnóstico ni interpretación médica |
| es_rm | Declaración de 1RM: true marca, false quita explícitamente, null conserva; también en cada detalle de series (ADR-0055) |
| series | Lista de detalles con serie, repeticiones, peso, unidad, comentarios, observaciones |
| aclarar | Campo y pregunta abierta cuando el contenido es realmente ambiguo |
| tiempo_segundos, descanso_segundos | Siempre null; no se capturan en el MVP |

En un alta por cantidad, los ordinales de series son relativos al lote (1..N).
Un valor común se aplica a todas sus filas y un detalle particular lo reemplaza.
«Las dos primeras» se expande en 1 y 2. «Cuarta y última» es una sola entrada.
No trasladar a otras series un peso, comentario u observación particular.
En una actualización múltiple los valores van en series; no mezclar valores raíz
con detalles. No repetir ordinales. Máximo 30 detalles por mensaje.

Peso: kg por defecto; lb se convierte en backend ×0,45359237, redondeo a dos
 decimales away-from-zero; «al cuerpo» → PesoCorporal y peso null. Reps >=1,
peso >=0. No aceptar simultáneamente peso numérico y peso corporal.
Las notas son opcionales; no se deduce que una serie sea efectiva o calentamiento.
Conservar las expresiones del alumno y su sentido, incluyendo lateralidad y negación.

## 3. Filas destino, estado y atomicidad

- Alta: ejercicio nuevo empieza 1..N. Otro lote consecutivo del mismo ejercicio
  continúa desde OrdenSerieSiguiente; los detalles relativos apuntan al lote nuevo.
- Actualización sin ejercicio: lote vivo (últimas filas creadas con mismo CreadoUtc).
- Con ejercicio: coincidencia inequívoca de nombre normalizado o crudo y ordinal
  dentro de la sesión. Si no existe, avisar. Si coincide más de una fila, pedir
  edición manual; jamás elegir por aproximación o crear una fila.
- Validar TODOS los detalles y destinos antes de aplicar. Valores inválidos,
  ordinales repetidos o fuera de lote, o alcance ambiguo: ninguna fila cambia.
- Solo se escriben valores presentes. Datos omitidos conservan lo existente;
  el último valor explícito reemplaza el anterior. La edición manual permite borrar notas.
- Reps + carga (numérica o al cuerpo) determinan completitud; comentarios y
  observaciones no son obligatorios. ADR-0020 bloquea otro lote mientras existan
  series incompletas. La SPA identifica los faltantes.
- Un mensaje válido reabre Lista. Expirada exige «seguir»; confirmación congela
  el snapshot y una carga nueva no modifica una planilla confirmada.

## 4. Aclaraciones

Nombre de ejercicio realmente ambiguo en un alta: crear las filas con los datos
explícitos, preguntar la variante sin opciones cerradas. Los datos de serie sin
nombre se pueden aplicar mientras espera, sin contar reintento. Resolver el nombre
no pierde reps, peso ni notas. Dos respuestas incomprensibles abandonan la pregunta
con las marcas históricas de pendiente. Fallos de infraestructura no cuentan.

Ambigüedad de alcance, serie o valor: responder la pregunta abierta en la SPA y no
aplicar el mensaje. El alumno reenvía el dictado con el detalle aclarado; no se abre
otra aclaración persistida. Un dato simplemente ausente queda null, sin inventarlo.

## 5. Corpus y pruebas

- «sentadilla hack 4 series primera y segunda serie aproximaciones con 40kg y
  12 repeticiones tercera serie efectiva con 80kg y 10 repeticiones y cuarta y
  ultima serie con 100kg y 8 repeticiones» → reps 12/12/10/8, kg 40/40/80/100;
  comentarios aproximaciones/aproximaciones/efectiva/null. Si el proveedor no
  está configurado, no es un error del dictado: revisar la clave del proceso.

- «Press de banca 4 series con 10 repeticiones, las 2 primeras series de
  aproximación con 40 kg, la tercera serie efectiva con 80 kg y la cuarta y
  última serie con 120 kg» → filas 1..4, reps 10 cada una, pesos 40/40/80/120;
  comentarios aproximación/aproximación/efectiva/null; observaciones null.
- Misma frase con «entrada en calor» o «calentamiento» → conservar esa expresión
  en comentarios de las filas 1 y 2.
- «Sentí un tirón en el pectoral izquierdo en la segunda serie de press de banca»
  → actualizar solo Observaciones de la serie 2 de ese ejercicio.
- «Prensa 3 series de 12 con 60» / «Prensa 3x12 con 60» → 3 filas completas 12/60.
- «Sentadillas hack 4 series y 12 repeticiones, primera serie 40 kg» → reps 12
  en todas, peso 40 solo en primera, demás pesos null.
- «Press de banca 4 series» → cuatro filas con reps/peso/notas null.
- «Sillón, tercera serie» → una fila serie 3 sin datos.
- «Primera serie 12 con 20» → actualizar solo reps/peso de la fila 1 del lote vivo.
- «Segunda serie con 30» → preservar reps y notas; peso 30 kg.
- «Tercera serie 10 al cuerpo» → reps 10, peso null, unidad PesoCorporal.
- «Segunda serie 10 con 40 en libras» → reps 10, peso 18,14 kg.
- «Primera serie con 40 y segunda con 60» → dos actualizaciones atómicas.
- «Sexta serie con 20» en lote de cuatro → aviso, sin filas nuevas.
- «Sentí un tirón» sin serie → preguntar a qué serie corresponde; no elegir una.
- «Press 4 series» con variante incierta → filas y pregunta abierta.
- Contradicción «segunda con 40 o 60» → aclarar peso; no elegir ni aplicar.

## 6. RM declarado (ADR-0055)

- `es_rm` es independiente de comentarios y observaciones y aplica a cualquier
  ejercicio con carga externa. Reconocer «RM», «1RM», «uno RM», «erre eme»,
  «repetición máxima» y «mi máximo para una repetición». No identificar RM por
  ser la mayor carga, última serie o «efectiva». «Récord personal» sin indicar
  una repetición exige aclarar si es 1RM.
- True marca, false solo ante negación/remoción explícita («no fue RM», «quitá
  el RM de la segunda serie»), null no modifica. Una marca particular va en el
  detalle del ordinal; un `es_rm` raíz con más de una serie exige aclarar alcance.
- Los valores finales de una serie marcada deben ser una repetición y peso
  positivo en kg (admite dictar lb y convertir), sin tiempo ni peso corporal.
  Contradicción o datos necesarios ausentes: pregunta abierta en la SPA, sin
  aplicar ninguna fila del mensaje ni crear aclaración persistente adicional.
  En actualizaciones se validan también los datos existentes: omitir reps/peso
  no los borra. No agregar una repetición simplemente por decir RM.
- «Peso muerto 2 series primera 5 con 80 segunda una repetición con 140 kilos
  repetición máxima» → 5/80 sin RM, 1/140 con RM. Mismo comportamiento para
  sentadillas, remos, presses, curls y cualquier otro nombre libre con carga.
- «Press de banca 4 series primera aproximación 12 con 40 segunda aproximación
  10 con 50 tercera efectiva 8 con 60 cuarta efectiva una repetición con 170 RM»
  → solo la cuarta marcada; conserva «efectiva» como comentario.
- «Segunda serie no fue RM» → quita solo la marca, conserva reps/peso/notas.
- «Cuarta dos repeticiones con 170 RM» → aclarar contradicción; ninguna fila cambia.
- «Sentadilla una serie con 100 RM» → preguntar si fue una repetición; sin alta.
- Aumentar reps de una serie marcada a 2 sin quitar RM rechaza todo el mensaje.
- Fallo de infraestructura NLU conserva la marca igual que los demás datos.

## 7. Voz

Al enviar la captura, la SPA muestra un indicador animado e indeterminado hasta
que responda la API (ADR-0030). Bloquea envíos duplicados y deja el texto en modo
lectura. Con éxito actualiza la cuadrícula; ante error conserva el borrador y
habilita el reintento. El indicador se retira en ambos casos.

Las confirmaciones informativas se ocultan a los 8 segundos o al tocar/clicar
el mensaje (también con teclado). Las preguntas y las aclaraciones vivas no se
ocultan; los errores y alertas siguen visibles según ADR-0031. Solo cambia la
presentación, no los datos ya registrados.

Reconocimiento en navegador, es-AR, en una sesión de hasta 60 segundos desde el
clic. Según ADR-0028, el motor usa `continuous=false` y se reinicia entre frases
dentro del mismo plazo. El alumno puede detener antes. Cada ciclo mantiene su
último resultado final y lo entrega una sola vez al terminar; sus revisiones no
se concatenan. Los interinos solo se muestran. No se eliminan palabras repetidas
del texto ni repeticiones intencionales entre ciclos.
No se envía mientras escucha o finaliza. Cancelar al salir de pantalla o pasar
al fondo. Errores de permisos/red detienen; los finales ya reconocidos se conservan.
La API recibe texto, nunca audio. El motor del navegador puede usar su servicio
remoto de reconocimiento según navegador y configuración.
