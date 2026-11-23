# Verification record

## Confirmación y gestión del historial — 2026-11-23

- ADR-0054 y ampliaciones en ADR-0017, 0019, 0024, 0046 y 0050 documentan los
  diálogos de confirmación, nombre opcional y eliminación lógica. Las reglas
  de dominio distinguen metadatos editables de las series congeladas.
- Migración nueva `AddTrainingHistoryMetadata`: columnas nullable `Name` y
  `DeletedUtc`, sin cambios en migraciones anteriores. Prueba de upgrade desde
  `AddAgreementAcceptances` con una planilla existente conserva sesión, snapshot
  y filas. EF no detecta cambios de modelo sin migración.
- Pruebas de API: normalización/vacío/límite de nombre, snapshot y PDF intactos,
  permisos entre cuentas, CSRF/acuerdo, historial tras eliminación y 404 en
  nuevos PDF/Excel/email/renombrados de la planilla eliminada. La sesión original
  continúa confirmada y los datos internos permanecen.
- Pruebas de interfaz: confirmación en dos pasos, cancelación sin POST ni pérdida
  del texto, doble clic, fallos recuperables, nombre/borrador, eliminación con
  cancelación y actualización de contador. El diálogo inicia en Cancelar,
  admite Escape y restaura foco; se usan dobles de dialog en JSDOM.
- La prueba automatizada no sustituye revisión visual en navegadores móviles
  ni certifica el despliegue de estos cambios en el dominio público.
- Resultado local: 259 pruebas backend correctas y 138 frontend correctas;
  lint y build de producción correctos, sin avisos de presupuesto. Tras ajustar
  el retorno de foco cuando desaparece la fila eliminada, se repitieron los
  13 casos de historial/diálogo y el build: correctos. Los 8 casos de historial
  y migración backend también se verificaron después del ajuste final de tests.

## Live PDF delivery reported by the operator — 2026-11-22

- After configuring the verified sender and Brevo API key, the operator confirmed
  receiving their training worksheet. Supplied screenshots show delivered events
  in Brevo and `planilla-3.pdf` received in Gmail from WorkoutAgentic, with the
  sender domain rewritten by Brevo.
- This is operator-provided evidence of a real PDF email delivery to that mailbox,
  rather than an automated test or an independently inspected inbox. Receipt of
  a real Excel attachment, delivery to other recipients and Railway deployment
  remain unverified. Earlier configuration/visual-review notes below describe
  their respective point-in-time checks.

## Single email action and missing-configuration diagnosis — 2026-11-22

- Backend: 240 tests passed, zero failed/skipped (`dotnet test -c Release -m:1
  -p:UseSharedCompilation=false`, outside the sandbox for testhost communication).
  Tests include status responses without secrets, authentication/agreement
  requirements and the existing SMTP/HTTPS delivery regressions.
- Frontend: 129 tests passed across 14 files (`npm test -- --watch=false`).
  `npm run lint` and the production `npm run build` passed without warnings.
  Removing the repeated email actions also removed the previous CSS budget warning.
- The report selector now has one centered email action before the format cards.
  Each card retains only its preview action with explicit spacing after the series
  count. Detail/print/confirmed views use the same self-contained email entry.
- The authenticated configuration check runs before asking for recipient/format.
  Missing configuration blocks submission, a failed check can be retried, and a
  pending check cannot send. Tests cover these states and the single-button order.
- Local checks inspected presence flags only, never secret values: Brevo provider,
  API key and sender were absent, as were the SMTP host/credentials/sender. The
  user confirmed they have no Brevo account yet. The observed 503 is explained by
  incomplete provider configuration, before attempting external delivery.
- No real email delivery has been enabled or certified. Account activation,
  authorized sender, API key and inbox checks for both attachments remain needed;
  see [email-setup.md](email-setup.md). Configuration status does not verify a
  provider's credentials, quotas or delivery.
- A temporary loopback server served the production frontend with synthetic review
  data, without using real accounts or sending email. The in-app browser timed out
  connecting to it, so visual review was not completed. The server was stopped.

## Historical report email and HTTPS delivery — 2026-11-22

- Backend: 237 tests passed, zero failed/skipped (`dotnet test -c Release -m:1
  -p:UseSharedCompilation=false`). Release was used because the existing Debug
  API process held its app host file. Test execution required leaving the sandbox
  so vstest could connect to testhost; the existing development API was not stopped.
- Frontend: 126 tests passed across 14 files (`npm test -- --watch=false`).
  `npm run lint` and `npm run build` passed. The production build has a small
  stylesheet budget warning for `reporte-workspace.scss` (under 0.1 kB over the
  12 kB warning threshold); no budget limits were increased.
- SMTP integration checks use a receiver bound to loopback, real account cookies
  and CSRF. PDF and Excel attachments received via MIME match the selected
  historical snapshot's download, while the active session and snapshot remain
  intact. Missing configuration, rejected recipient, invalid inputs (including
  null format), and foreign-sheet access are covered.
- HTTPS delivery contract checks replace Brevo at the HTTP boundary. They verify
  fixed HTTPS destination, API-key header, recipient, exact base64 attachment,
  provider rejection/quota/network/timeout handling, and password recovery using
  the same transport without attachments. No external emails were sent.
- UI tests cover card/detail/print entry points, selected format, focus, validation,
  in-flight duplicate suppression, preserved input on errors, reset on sheet
  change and returning to history without losing the capture draft.
- Windows visual review could not proceed: Computer Use could not determine the
  Chrome URL confidently and stopped. Mobile/desktop visual verification is still
  pending; no claim of a completed manual browser review is made.
- No SMTP credentials were configured locally. Actual Brevo account activation,
  authorized sender/API key, provider connectivity from the deployment, inbox
  receipt and opening the received PDF/Excel remain to be verified. Railway
  Free/Trial/Hobby block SMTP; use the HTTPS configuration documented in ADR-0051.

## QA agreement acceptance — 2026-11-19

- Backend build passed (`dotnet build WorkoutAgentic.Api.Tests/WorkoutAgentic.Api.Tests.csproj
  --no-restore -p:UseSharedCompilation=false`). The final build initially retried
  copying the app host while the isolated review API was running; it completed
  after that process was stopped.
- Backend suite: 218 passed, zero failed or skipped (`dotnet test
  WorkoutAgentic.Api.Tests/WorkoutAgentic.Api.Tests.csproj --no-build --no-restore
  --verbosity minimal`). The runner required execution outside the sandbox for
  local process communication.
- Frontend suite: 113 passed across 13 files (`npm test -- --watch=false`), with
  worker processes running outside the sandbox. Frontend lint and production
  build passed. EF Core reports no pending model changes after the new migration.
- API checks cover training reads/writes blocked before acceptance, explicit
  consent, version/hash mismatch, CSRF, idempotence, persistence across login,
  separate accounts, private receipts, changed document and missing owner data.
- Browser review against an isolated temporary SQLite database verified the
  public document, login redirect to the agreement, full text with the supplied
  responsible-party details, unchecked consent, disabled continuation and logout
  through “No acepto, salir”. No agreement was accepted through the browser on
  behalf of the user; acceptance persistence was verified by automated tests
  with synthetic accounts. The temporary review servers were stopped afterward.
- This verifies the implementation locally. It does not certify legal review,
  email identity, invitation restrictions or a production deployment.

## Authentication increment — 2026-11-17

- Backend build: passed with zero warnings/errors. Identity migration adds tables
  without changing previous migrations or assigning existing demo students.
- Backend integration/domain suite: 209 tests passed, zero failed or skipped
  (`dotnet test --no-build -m:1 --verbosity minimal` after the final build).
  The runner required execution outside the sandbox for process communication.
- EF Core `migrations has-pending-model-changes`: no pending changes.
- Frontend lint: passed; 69 tests across eight files passed, including auth guards,
  account form errors and CSRF refresh. Production build passed.
- Real-cookie integration checks cover two separate accounts, cross-student
  capture/row/history/report access rejection, atomic registration, duplicate
  email, lockout, logout revocation, absolute expiry, rate limiting, preservation
  of historical students and password reset/replay rejection.
- Visual browser verification could not run: the in-app browser timed out against
  localhost and Windows Computer Use could not verify the Chrome URL. Physical
  iPhone/Samsung dictation/install and real SMTP delivery remain unverified.

The following original record remains historical evidence for the earlier demo.

Review date: **2026-11-12**. This is a point-in-time local verification record, not a CI status badge or a claim about future commits.

Environment: Windows, .NET SDK 10.0.401, Node.js 22.20.0, npm 11.17.0. Dependencies were already installed for this review; a clean `npm ci` installation was not repeated.

| Check | Command / method | Result |
| --- | --- | --- |
| Backend build | `dotnet build -m:1 -p:UseSharedCompilation=false` in `src/api` | Passed; zero warnings and errors |
| Backend tests | `dotnet test --no-build -m:1 --verbosity minimal` in `src/api`, after the build | 192 passed, zero failed or skipped |
| Frontend lint | `npm run lint` in `src/web` | Passed |
| Frontend tests | `npm test -- --watch=false` in `src/web` | 61 passed across six files |
| Production frontend build | `npm run build` in `src/web`, with `NG_BUILD_MAX_WORKERS=2` for this environment | Passed after ADR-0037; initial bundle approximately 277.52 kB |
| Fresh database startup | API with `ConnectionStrings__Sqlite=Data Source=review-demo.db;Cache=Shared` | Migrations applied and catalog seeded; student created explicitly by API |
| Live text interpretation | Synthetic bench-press message in the SPA, using configured Gemini credentials | Three sets of 10 reps at 40 kg displayed |
| Edit and confirm | Change third load to 45 kg, save through the editor, then confirm | Corrected row displayed; frozen snapshot and report preview preserved 40/40/45 kg |

The sandbox prevented normal network access and some worker/test-process communication. Backend tests and local demo servers ran outside that sandbox. No real trainee data was used. The sample database and process logs are ignored local artifacts and are not included in the repository.

The production build originally failed while downloading Google Fonts. [ADR-0037](adr/0037-compilacion-sin-descarga-de-fuentes.md) disables build-time font inlining while preserving script/style optimization and the browser's external font stylesheet link.

The browser review covered typed input, live interpretation, bulk-editor save, confirmation, and report preview. It did not certify physical microphone capture, mobile installation, SMTP delivery, deployment security, or live-model accuracy across the full Spanish input domain. Automated NLU tests use controlled responses rather than paid provider calls.

# Verificación del despliegue Railway — 2026-11-23

- Backend: `dotnet test src/api/WorkoutAgentic.slnx --no-restore --verbosity quiet`:
  251 pruebas correctas, sin fallos ni omisiones. Incluye 11 casos nuevos de
  rutas SPA/404, salud HTTP, confianza explícita del proxy y cookies seguras.
  El runner requirió ejecución fuera del sandbox para su comunicación local.
- Frontend: lint correcto, 129 pruebas correctas y build de producción correcto,
  sin avisos de presupuesto; bundle inicial 306,14 kB.
- `dotnet publish` Release correcto. Angular copiado al `wwwroot` publicado.
  Arranque real en Production con SQLite nueva: `/health`, `/`, `/login`,
  bundle JS y CSRF responden 200; API/asset inexistentes responden 404.
  HTTP interno con esquema reenviado HTTPS no entra en bucle de redirección.
- Docker no está instalado en esta PC. Railway construyó correctamente la imagen
  Linux desde `dev`, commit `3e15eac`, despliegue
  `c247d8fd-a4e8-44ae-8243-a5757aad0958` en estado SUCCESS.
- Dominio real: `https://workoutagentic-production.up.railway.app`. Salud,
  navegación directa a login/registro/recuperación/acuerdo y CSRF: HTTP 200.
  API/asset inexistentes: 404. Cuenta técnica explícita `@example.test`:
  registro 201, login 204 y cuenta autenticada 200; cookies Secure/HttpOnly.
  Historial bloqueado con 403 antes del acuerdo. No se aceptó el acuerdo en
  nombre de nadie ni se crearon entrenamientos en esa cuenta técnica.
- Redeploy `5f6b3a7b-c927-459c-a259-310ac34289ff`: SUCCESS. La cookie emitida antes
  del redeploy sigue autenticando la misma cuenta después: SQLite y claves de
  Data Protection persistieron en el volumen `/data` de 500 MB, una réplica iad.
- Navegador real: `/login` muestra formulario y enlaces de cuenta en HTTPS.
- Gemini y Brevo configurados por variables privadas, sin incluir claves en Git.
  **Pendiente en producción:** aceptación personal del acuerdo, carga NLU,
  confirmación/historial, apertura de PDF/XLSX, recepción real del email y
  recuperación de contraseña. Las pruebas automatizadas locales y la recepción
  previa por Brevo no sustituyen esa verificación en el nuevo dominio.
