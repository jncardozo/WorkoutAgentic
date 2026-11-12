# Engineering overview

WorkoutAgentic converts a trainee's natural-language workout notes into reviewable set records. Its current scope is a Spanish-language trainee SPA and a text-based API. Teacher workflows and WhatsApp integrations were removed from the runtime in [ADR-0018](adr/0018-alumno-solo-y-captura-en-navegador.md); older ADRs and migrations remain as history.

## Interpretation and state

```text
Typed text / browser speech recognition
  -> editable transcript in the Angular SPA
  -> POST /api/capturas/{alumnoId}/texto
  -> intent classification + entity extraction (Gemini structured output)
  -> domain validation and session update, or clarification/failure response
  -> editable set records
  -> explicit user confirmation
  -> immutable workout snapshot -> history / PDF / Excel / optional email
```

Speech recognition and language interpretation are separate stages. The application API receives text, not audio. Intent and entity interfaces share one provider analysis per message through a scoped Gemini adapter. Model output is interpreted by session/domain logic rather than being persisted directly as authoritative data.

The main rule is **never invent workout data**:

- Missing values remain missing; the trainee can supply them later by text, dictation, or grid editing.
- Ambiguous exercise variants prompt an open clarification. Ambiguous numeric values or targets are rejected for clarification without applying the message.
- Explicit common values can apply across a batch, while explicitly stated per-set details take precedence.
- Updates target existing sets; an ambiguous or nonexistent target does not silently create replacement rows.
- A provider/network/parse failure leaves the recorded workout unchanged.

See the Spanish [NLU specification](domain/nlu-captura.md) and [business rules](domain/business-rules.md) for exact intent, scope, and completeness rules.

## Editable records and confirmed snapshots

`RegistroParcial` represents an editable captured set. `PlanillaConfirmada` and its rows represent a separate frozen snapshot. Session states include `EnCurso`, `EsperandoAclaracion`, `Lista`, `Expirada`, and `Confirmada`.

Confirmation is an explicit trainee action and accepts an eligible session in `EnCurso` or `Lista` with at least one row and no pending clarification. Progress completion and confirmation are separate concepts. Incomplete sets block advancing to another exercise under the current capture rules; a progress indicator is not an automatic confirmation trigger.

Bulk editing validates the requested changes and original row values before applying them in a database transaction. Conflicts return HTTP 409 rather than overwriting a concurrently changed row. Confirmed reports are generated from snapshot rows, not from subsequently editable capture data.

## Implementation choices

| Choice | Reason and current trade-off |
| --- | --- |
| Angular standalone components and signals | Explicit reactive UI state and isolated capture, editing, progress, and report components |
| .NET Minimal APIs organized by feature | Keep capture, NLU, session, student, and worksheet responsibilities together |
| Browser speech recognition | The application backend remains text-only; browser/platform support and speech-service availability vary |
| Gemini JSON Schema output | Structured intent/entity extraction; provider availability, quotas, latency, and model behavior still require handling |
| Free-text exercise normalization | Accept natural exercise names without a closed catalog; ambiguous variants still need user clarification |
| SQLite + EF Core + WAL | Lightweight local persistence with versioned migrations; PostgreSQL is a possible scaling step, not the current provider |
| Explicit snapshot confirmation | Keep editable work distinct from confirmed history and export data |

## Reviewable engineering evidence

- Backend tests cover API integration, session behavior, interpretation responses, persistence, edits, and exports under `src/api/WorkoutAgentic.Api.Tests`.
- Frontend tests cover worksheet interactions, dictation lifecycle, grouping, editing, and API contracts under `src/web/src/app`.
- Domain rules describe colloquial Spanish input and ambiguity handling.
- Numbered [ADRs](adr/) preserve decisions and superseded approaches. Read the [current architecture](arquitectura.md) before assuming an older ADR still describes the runtime.

Automated tests use controlled provider responses. Passing them is evidence about application behavior, not a live-model accuracy benchmark or mobile speech certification.

## Current limits and candidate next work

The root SPA route uses demo student #1, and the API has no authentication or per-student authorization. Multi-user access control is a prerequisite for production deployment. Email requires separately configured SMTP. Web-app metadata supports installation but does not implement offline capture.

Useful next increments include authenticated student ownership, a larger Spanish interpretation evaluation corpus, provider quality/latency measurement, real-device dictation/install checks, and deployment preparation. These are proposed directions, not features claimed as complete or a committed release schedule. Automated routine generation remains outside the capture MVP.
