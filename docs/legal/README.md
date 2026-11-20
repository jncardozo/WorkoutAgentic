# Documentos de la prueba de QA

- `acuerdo-confidencialidad-qa.md`: borrador bilateral original para completar y
  revisar antes de una firma externa. **No se muestra en la app.**
- `condiciones-prueba-app-v1.txt`: texto que se muestra y acepta en la SPA,
  incorporado como recurso del backend. Los marcadores `{{OwnerName}}`,
  `{{OwnerAddress}}` y `{{ContactEmail}}` se completan desde configuración; no
  aparecen pendientes cuando están configurados los tres datos. `{{Version}}`
  se completa con la versión definida por el backend.

## Configuración y publicación

Los datos del responsable suministrados por el usuario están en la sección
`Legal` de `src/api/WorkoutAgentic.Api/appsettings.json`. Son datos visibles en el
acuerdo, no secretos. El despliegue puede reemplazarlos con `Legal__OwnerName`,
`Legal__OwnerAddress` y `Legal__ContactEmail`. No se permite aceptar mientras
falte uno de esos valores. Revisar su exactitud antes de distribuir la prueba.

La nueva migración `AddAgreementAcceptances` se aplica con las migraciones
pendientes durante el arranque normal de la API. No modifica los datos ni las
migraciones históricas. Respaldar la base como parte del despliegue habitual.

Tras desplegar SPA y API actualizadas, las cuentas nuevas y existentes sin
aceptación vigente se redirigen a `/acuerdo-qa` antes de cargar entrenamientos.
El texto también está disponible en `/legal/confidencialidad`. El autorregistro
sigue abierto: el acuerdo no limita el acceso a dos personas por sí solo.

## Cambiar las condiciones

Revisar el texto con asesoramiento legal, actualizar su versión en
`Features/Legal/AgreementDocument.cs` y reconstruir/publicar la API. No
editar registros existentes de `AgreementAcceptances`. El SHA-256 del texto
renderizado identifica exactamente lo aceptado; cualquier cambio exige una nueva
aceptación y conserva la anterior.

El documento descargable antes de aceptar contiene el texto vigente. Después de
aceptar, «Mi cuenta → Acuerdo de confidencialidad» ofrece una constancia privada
con el texto aceptado, nombre y correo de la cuenta al momento de aceptar, fecha
UTC, versión y huella. Los ejemplares y constancias personales no deben subirse
al repositorio.

Esta integración registra una aceptación explícita vinculada a una cuenta; no
certifica identidad ni constituye por sí sola una firma digital certificada.
Ver [ADR-0049](../adr/0049-aceptacion-obligatoria-acuerdo-qa.md).
