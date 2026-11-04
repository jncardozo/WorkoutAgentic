# Arquitectura — captura del alumno

> Alcance vigente: SPA Angular para el alumno y API .NET. El navegador
> reconoce la voz; el backend trabaja solo con texto. WhatsApp y el módulo de
> profesor están retirados del producto de este sprint (ADR-0018).

## 1. Componentes

```
Alumno
  └─ SPA Angular 21
       ├─ reconocimiento de voz en el navegador
       ├─ edición de texto y cuadrícula
       ├─ confirmación, exportación Excel/PDF y envío por correo
       └─ POST /api/capturas/{alumnoId}/texto
                    │
                    ▼
             API .NET (src/api)
       ├─ CapturaTextoNlu: intención + entidades (Gemini JSON Schema)
       ├─ SesionCapturaServicio: estado, parciales y aclaraciones
       ├─ PlanillaApi: consulta, edición, borrado, confirmación y exportación
       └─ SQLite (EF Core, WAL)
```

El navegador envía texto reconocido o escrito. No se sube audio a la API.
La aplicación no expone login, roles ni vistas de profesor en este sprint. La
SPA abre directamente la planilla y usa temporalmente el alumno de prueba #1;
los dispositivos comparten ese historial hasta implementar autenticación.

## 2. Backend

| Slice | Responsabilidad |
|---|---|
| `CapturaTexto` | Validar el texto y el alumno, invocar el NLU y aplicar el análisis a la sesión. |
| `Nlu` | Clasificar intención y extraer entidades; normalizar el ejercicio como texto libre. |
| `CapturaSesion` | Mantener estados de sesión, crear filas y pedir aclaraciones abiertas cuando haga falta. |
| `PlanillaApi` | Leer la sesión y el historial de planillas confirmadas, editar/agregar/eliminar parciales, congelar snapshots y exportar/enviar planillas. |
| `Alumnos` | Dar de alta explícitamente una cuenta de alumno por nombre. |
| `Data` | Persistir con EF Core y SQLite. |

El NLU no usa catálogo para aceptar ejercicios. Si la interpretación falla, la
API responde sin cambiar los registros. Las aclaraciones se responden desde la
misma SPA.

## 3. Flujo de captura y estados

1. El alumno escribe o dicta en la SPA; el navegador presenta el texto reconocido
   en el borrador para que pueda revisarlo.
2. La SPA envía el texto a `/api/capturas/{alumnoId}/texto`.
3. Gemini devuelve intención y entidades estructuradas; el nombre de ejercicio
   queda como texto libre normalizado.
4. `SesionCapturaServicio` crea o actualiza `RegistroParcial`, o devuelve una
   aclaración que la SPA muestra.
5. El alumno revisa y confirma desde la cuadrícula. La API crea un snapshot
   inmutable y pasa la sesión a `Confirmada`.

La sesión persiste estados `EnCurso`, `EsperandoAclaracion`, `Lista`,
`Expirada` y `Confirmada`. Solo `EnCurso` y `Lista` admiten edición y confirmación;
la confirmación requiere filas y ninguna aclaración viva.

## 4. Persistencia

Las entidades activas incluyen `Alumno`, `SesionCaptura`, `RegistroParcial`,
`AclaracionPendiente`, `PlanillaConfirmada` y `PlanillaFila`. Los registros
parciales son editables; las filas confirmadas se guardan como snapshot separado.
Los ejercicios se guardan con nombre normalizado libre.

Las migraciones históricas se conservan sin editar. Por eso una base existente
puede contener tablas o columnas antiguas de WhatsApp/profesor; el runtime actual
no expone endpoints ni las consulta. No se borran esos datos durante este cambio.

## 5. SPA Angular 21 (`src/web`)

La SPA abre directamente la captura por texto/voz y la cuadrícula editable con
confirmación. En este MVP usa internamente el alumno de prueba #1, sin pedir al
alumno que ingrese un número. La voz usa la API de reconocimiento del navegador y
el resultado final se agrega al mismo borrador que el texto manual. El backend
recibe solo texto.

La interfaz permite crear y editar ejercicios con nombres libres, completar
repeticiones y peso, borrar filas individualmente o en bloque, y confirmar la
cuadrícula. Una planilla confirmada se muestra congelada al alumno y permite
descargar Excel/PDF o enviar uno de esos archivos a una dirección indicada por
el alumno. El envío usa SMTP configurado fuera del repositorio. La SPA indica
qué series están incompletas y no permite iniciar otro ejercicio hasta cargar
repeticiones y peso (o «al cuerpo») en todas las series pendientes (ADR-0020).

## 6. Decisiones vigentes

| Decisión | Elección |
|---|---|
| Captura de voz | Reconocimiento del navegador (ADR-0011); no se envía audio. |
| Interpretación | Gemini con salida JSON Schema; normalización a texto libre (ADR-0016). |
| Persistencia | SQLite en WAL con EF Core. |
| Confirmación | Disponible desde `EnCurso` o `Lista` (ADR-0017). |
| Exportación | Excel y PDF generados desde el snapshot confirmado; correo por SMTP configurable (ADR-0019). |
| Canales y roles | Solo SPA del alumno en este sprint; WhatsApp y módulo de profesor retirados (ADR-0018). |

## 7. Verificación

- API: `dotnet build` desde `src/api`.
- SPA: `npm run build` desde `src/web`.
- Suites existentes: `dotnet test` y `npm test` desde sus proyectos.

Los flujos a cubrir son captura por texto, interpretación libre, aclaración,
edición, eliminación individual/masiva y confirmación inmutable.
