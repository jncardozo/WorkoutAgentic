# Verification record

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
