# Verification record

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
