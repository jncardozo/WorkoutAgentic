# Local setup and verification

Run commands from the repository root unless a step changes directories. Examples use PowerShell, matching the current development environment.

## Prerequisites

- Git and .NET SDK 10 (`dotnet --version`). The backend targets `net10.0`.
- Node.js 22.12 or newer in the 22.x line and npm (`node --version`, `npm --version`).
- A Gemini API key and a model available to that key that supports structured output. Model availability and quotas depend on the provider account.
- Chrome or Edge is recommended for evaluating browser dictation. Text entry works without a microphone.

## 1. Configure and start the API

```powershell
cd src/api
dotnet restore
dotnet user-secrets set "Gemini:ApiKey" "YOUR_GEMINI_API_KEY" --project WorkoutAgentic.Api
dotnet run --project WorkoutAgentic.Api --launch-profile http
```

Replace the placeholder locally; do not put a real key in a tracked file. The project already declares a `UserSecretsId`, so `user-secrets init` is unnecessary. The `http` profile selects Development and listens on `http://localhost:5259`.

The API applies pending EF Core migrations, enables SQLite WAL mode, and seeds the legacy exercise catalog on startup. No separate database server or manual migration command is needed. Exercise capture accepts normalized free-text names rather than requiring a catalog match.

The default model ID is defined in [`GeminiOptions.cs`](../src/api/WorkoutAgentic.Api/Features/Nlu/GeminiOptions.cs). If it is unavailable to your account, set an accessible model before restarting:

```powershell
dotnet user-secrets set "Gemini:ModeloNlu" "YOUR_SUPPORTED_MODEL_ID" --project WorkoutAgentic.Api
```

The API can start without a Gemini key, but interpretation will fail without usable credentials. There is no automatic offline interpreter.

## 2. Create an account through the SPA

After starting the frontend below, open `http://localhost:4200/registro` and enter
a synthetic name, an email, and a password of 12–128 characters. Registration
creates the access account and a new student atomically, then the SPA signs in.
Subsequent visits use `/login`; `/` opens the signed-in student's own worksheet.
Existing demo students are preserved and never assigned automatically to new accounts.

Registration is open for this test release and does not verify email ownership.
No professor or invitation is required. See [ADR-0038](adr/0038-autorregistro-y-autenticacion-de-alumnos.md).

## 3. Install and start the SPA

From the repository root, in another terminal:

```powershell
cd src/web
npm ci
npm start
```

Open `http://localhost:4200/registro` to create an account, or `/login` to sign in. Old links containing student IDs redirect to the signed-in student's worksheet.

The development proxy forwards `/api` to `http://localhost:5259`. If you change the API port, update `src/web/proxy.conf.json` to match and restart the SPA server. Development does not require an HTTPS certificate for this local HTTP profile. Microphone access requires a secure context such as localhost or HTTPS and a supported browser.

## Configuration reference

.NET reads its usual configuration sources; the application does **not** load `.env` automatically. [`.env.example`](../.env.example) lists current environment variable names as a reference, not as an executable configuration file.

| Setting | Purpose |
| --- | --- |
| `Gemini:ApiKey` / `Gemini__ApiKey` | API key; user-secrets in development, environment variable in deployment |
| `GEMINI_API_KEY` | Fallback alias, used only when `Gemini:ApiKey` is empty |
| `Gemini:ModeloNlu` / `Gemini__ModeloNlu` | Override the model ID |
| `ConnectionStrings:Sqlite` / `ConnectionStrings__Sqlite` | SQLite connection string; default is `Data Source=workoutagentic.db;Cache=Shared` |
| `CorreoSmtp:*` / `CorreoSmtp__*` | Email delivery for reports and password recovery |
| `Auth:PublicOrigin` / `Auth__PublicOrigin` | Trusted HTTPS origin for password reset links, e.g. `https://your-test-host.example` |
| `Auth:DataProtectionKeysPath` / `Auth__DataProtectionKeysPath` | Persistent restricted directory for cookie/token keys; defaults to `auth-keys` alongside SQLite |

To isolate a demo from an existing local database, set the connection string **in the terminal that starts the API**, before running it:

```powershell
$env:ConnectionStrings__Sqlite = 'Data Source=review-demo.db;Cache=Shared'
dotnet run --project WorkoutAgentic.Api --launch-profile http
```

Run this from `src/api`. Relative database paths resolve under the API content root (`src/api/WorkoutAgentic.Api`). SQLite database, WAL, and SHM files are ignored by Git.

Email is optional. Configure `CorreoSmtp:Host`, `Port`, `EnableSsl`, `UserName`, `Password`, `FromAddress`, and `FromName` using user-secrets or environment variables. The defaults are port 587 and SSL enabled. Downloads and previews do not require SMTP. An unconfigured mail server produces an explicit unavailable response; no email is sent during the demo walkthrough.

## Verification

### Sharing a link with testers

Expose the SPA and `/api` through a **single HTTPS origin**, using a trusted
reverse proxy or tunnel. A tunnel's own login is separate from the app's account
login: testers must be allowed through that outer access control as well.
Only localhost may use HTTP during development. For a production environment,
the cookie always requires HTTPS. A TLS-terminating reverse proxy must forward
requests using the deployment's configured trust rules; do not trust arbitrary
forwarded client headers. Test the URL with both devices before sending it.

Persist and back up SQLite **and** the `auth-keys` directory (or configured
`Auth:DataProtectionKeysPath`). Protect that directory with OS permissions;
file-system key persistence alone does not encrypt it at rest. Do not publish
or commit keys. Losing them invalidates cookies and pending reset links.

Password recovery uses `CorreoSmtp` and `Auth:PublicOrigin`. Set the latter to
the actual HTTPS origin through user-secrets or `Auth__PublicOrigin`. The app
never builds reset links from a client-supplied Host. Missing SMTP/origin prevents
delivery; the generic response deliberately does not disclose account existence.
Reset links expire in one hour. Logout and password reset revoke the account's
sessions on all devices. Sessions have an eight-hour absolute lifetime.

The API contract is now `POST /api/alumnos` with `{name,email,password}`;
name-only creation is no longer available. Script/API clients must first GET
`/api/auth/csrf` with a cookie jar, then send the `XSRF-TOKEN` cookie value as
`X-XSRF-TOKEN` on every mutation. Login: POST `/api/auth/login` with
`{email,password}`. After login/logout refresh `/api/auth/csrf` before mutations.
GET `/api/auth/me` returns `{studentId,name,email}` for the session. Angular
handles this handshake and never stores passwords or access tokens in browser storage.

Backend, from `src/api`:

```powershell
dotnet build
dotnet test
```

Frontend, from `src/web`:

```powershell
npm run lint
npm test -- --watch=false
npm run build
```

The backend tests substitute the NLU provider and use test databases; frontend tests use mocked API/browser interfaces. These suites do not validate a live Gemini account or physical microphone. See the [demo walkthrough](demo.md) for manual evaluation.

Production builds keep the external font stylesheet link instead of downloading it at build time ([ADR-0037](adr/0037-compilacion-sin-descarga-de-fuentes.md)). The browser still needs access to Google Fonts to display the web fonts; existing fallback fonts apply if that service is unavailable.

## Troubleshooting and limits

- **Login required:** register a new account or sign in. Historical demo students have no automatically assigned credentials.
- **API unreachable:** check the backend is listening on port 5259 and use the SPA development proxy.
- **Interpretation unavailable:** check the key, model access, quota, and outbound access to the Gemini API. The UI should report failure without applying invented workout values.
- **Dictation unavailable:** use text entry, or test a supported browser and microphone permission. The browser's speech service may itself require network access; audio is not sent to the WorkoutAgentic API.
- **Restricted .NET build environment:** if MSBuild worker or compiler-server startup fails, try `dotnet build -m:1 -p:UseSharedCompilation=false` and `dotnet test -m:1 -p:UseSharedCompilation=false`.

The API requires authentication and student ownership. Evaluation should use synthetic data. Public rollout still requires deployment preparation, email verification and review of abuse controls.
