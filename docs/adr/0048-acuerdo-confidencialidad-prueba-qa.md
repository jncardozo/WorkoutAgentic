# ADR 0048 — Acuerdo de confidencialidad para la prueba cerrada de QA

- **Estado:** Borrador documental creado a pedido del usuario; integración y
  restricciones de acceso propuestas, pendientes de implementación.
- **Fecha:** 2026-11-19.

> **Actualización del 19 de noviembre de 2026:** el usuario solicitó implementar
> la aceptación dentro de la aplicación. El [ADR-0049](0049-aceptacion-obligatoria-acuerdo-qa.md)
> concreta ese flujo y reemplaza la recomendación de gestionar la aceptación
> únicamente fuera de la app. Las secciones siguientes conservan el contexto de
> la propuesta original.

## Contexto

Dos QA de Argentina probarán WorkoutAgentic exclusivamente desde el navegador,
sin acceso al repositorio. Se necesita un acuerdo de confidencialidad y una
recomendación sobre dónde presentarlo en la aplicación.

El registro y login existentes están documentados en ADR-0038. El autorregistro
es abierto y el correo no se verifica. Crear una cuenta o iniciar sesión no
demuestra que se haya firmado un acuerdo ni que el correo pertenezca a un QA
previamente identificado. Actualmente tampoco hay persistencia de aceptación
del acuerdo ni un bloqueo de acceso vinculado a él.

## Decisión documental

- Mantener el modelo sin datos personales en
  [docs/legal/acuerdo-confidencialidad-qa.md](../legal/acuerdo-confidencialidad-qa.md).
- Preparar una copia por QA, con la identidad real de las partes, plazo de prueba,
  canales autorizados y jurisdicción completados. WorkoutAgentic es el nombre
  del proyecto y no sustituye la identificación de la persona que contrata.
- El texto es un borrador para revisión legal, no un acuerdo ya celebrado.
  Propone tres años de reserva después del cierre y continuidad para secretos
  comerciales mientras reúnan las condiciones legales. Ese plazo debe validarse.
- Mantener los ejemplares completos y firmados en almacenamiento privado con
  acceso restringido, fuera de Git y de los recursos públicos de la SPA. El
  repositorio conserva solamente el modelo y su historial de cambios.

## Recomendación para esta prueba de dos personas

Completar y firmar el documento con cada QA antes de habilitar la evaluación;
entregarle su copia y conservar la constancia de firma. Así se puede comenzar
con una gestión manual sin introducir de inmediato un sistema de contratación
electrónica dentro de la app.

El despliegue de prueba debe restringirse efectivamente a las personas
autorizadas mediante un mecanismo elegido y verificado para ese entorno. No
basta con mantener el enlace en privado ni con ocultar el botón de registro.
El autorregistro abierto actual requiere resolver esa restricción antes de
presentar la prueba como acceso cerrado. Este ADR no implementa invitaciones,
listas de permitidos ni revocación administrativa.

## Ubicación propuesta en la SPA

1. **Login y registro:** enlace visible «Acuerdo de confidencialidad de la prueba»
   para consultar el modelo aprobado antes de entrar. El acceso al texto no debe
   obligar a iniciar sesión ni publicar ejemplares con DNI, domicilios o firmas.
2. **Si se incorpora aceptación dentro de la app:** pantalla dedicada después de
   autenticar a un QA autorizado y antes del primer acceso a la planilla. Permitir
   leer el documento completo, guardar una copia y aceptar mediante una acción
   explícita, sin casilla premarcada. Rechazar debe permitir salir sin acceder a
   los entrenamientos. También debe cubrir cuentas ya creadas.
3. **Mi cuenta:** entrada permanente «Acuerdo de confidencialidad», con la versión
   correspondiente al QA y una copia descargable. Si contiene información
   personal, su entrega debe requerir autenticación y autorización del servidor.

Las rutas candidatas son `/legal/confidencialidad` para el modelo aprobado y
`/acuerdo-qa` para la eventual pantalla de aceptación. **No existen actualmente.**
No presentar el borrador incompleto como texto definitivo ni mezclar el NDA con
el aviso de privacidad o con términos generales para futuros alumnos.

## Requisitos si se implementa aceptación electrónica

- Identificar a las partes y vincular al QA con una cuenta previamente
  autorizada; el correo sin verificar actual no es suficiente para dar por
  acreditada su identidad.
- Definir con asesoramiento legal cómo se identifica y manifiesta la voluntad
  de ambas partes. Un clic o una casilla no deben anunciarse como firma digital
  en el sentido de la Ley 25.506.
- Conservar del lado del servidor el identificador de cuenta, la versión
  definitiva, una copia inmutable del texto aceptado, fecha y hora UTC generadas
  por el servidor y evidencia del acto de aceptación. Un hash ayuda a identificar
  el documento, pero no prueba por sí solo quién lo aceptó. Si se firma fuera de
  la app, vincular la referencia al ejemplar firmado y restringir su consulta.
- Recoger solo los datos de evidencia necesarios y definir su conservación,
  acceso e información al participante. No registrar contraseñas, cookies ni
  tokens como evidencia.
- Exigir autorización en la API antes de acceder a capturas, planillas, historial,
  exportaciones y correo. Un guard de Angular o un valor en localStorage no
  sustituyen ese control. Mantener disponibles las operaciones necesarias para
  consultar el acuerdo, completar el proceso o cerrar sesión.
- No considerar firmado un documento por el solo hecho de leerlo, registrarse o
  continuar navegando. Entregar la copia y constancia correspondientes.
- Conservar versiones anteriores; los cambios materiales requieren definir una
  nueva aceptación, sin sustituir silenciosamente el texto ya acordado.
- Incorporar una nueva migración si se persisten aceptaciones; nunca modificar
  migraciones aplicadas. Cubrir acceso directo a la API sin aceptación, rechazo,
  cuentas existentes, otra cuenta y versiones del acuerdo.

## Alcance de esta entrega

Se agregan únicamente el modelo del acuerdo y este ADR. No se modifica el
frontend, backend, esquema de datos ni configuración del despliegue. La
recomendación de ubicación no constituye una funcionalidad implementada.

## Fuentes oficiales y límites

Consultadas el 19 de noviembre de 2026:

- [Ley 24.766, artículos 1 a 3 y 11](https://www.argentina.gob.ar/normativa/nacional/41094/texto):
  condiciones de protección de información confidencial y acciones ante usos
  contrarios a las prácticas comerciales honestas. No otorga exclusividad sobre
  una idea de producto ni impide el desarrollo independiente.
- [Ley 25.506, artículos 2, 5, 7 y 8](https://www.argentina.gob.ar/normativa/nacional/ley-25506-70749/actualizacion):
  distingue firma electrónica y digital. Si se desconoce una firma electrónica,
  quien la invoca debe acreditar su validez; no corresponde prometer a una
  casilla de aceptación las presunciones previstas para la firma digital.

Las cláusulas y plazos del borrador son propuestas contractuales para este caso,
no requisitos textuales impuestos por esas leyes. La modalidad de firma,
jurisdicción, titularidad y aplicación concreta requieren revisión profesional.
