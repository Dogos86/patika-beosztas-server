# AGENTS.md

## Project mission

Build a secure Hungarian pharmacy scheduling, leave-request, sickness-reporting and absence-management system with typed and voice AI assistance.

## Source of truth

Read and follow all files under `docs/`, `contracts/`, and the active phase prompt. Do not invent business rules when documents are ambiguous. Record unresolved decisions in `docs/OPEN_DECISIONS.md`.

## Language

- User interface and user-facing validation: Hungarian.
- Code identifiers, API fields, class names, comments and tests: English.
- Domain terminology may be documented bilingually where helpful.

## Target architecture

- ASP.NET Core .NET 10 Web API.
- PostgreSQL with EF Core migrations.
- Domain/Application/Infrastructure/Contracts/API separation.
- OpenAPI is the source of truth for generated frontend API types.
- SignalR can be added after core REST workflows are stable.
- PWA frontend first; Capacitor later.
- AI and speech providers behind replaceable interfaces.

## Security invariants

- Authorization is enforced server-side on every mutation and sensitive query.
- Never trust an employee ID sent by an ordinary employee client for self-service operations.
- Application permissions and professional employee roles are separate.
- Permissions are additive; an admin can also be a schedulable employee.
- Never expose service/admin secrets to the frontend.
- Never store a medical diagnosis.
- AI never writes directly to the database.
- Every AI write requires schema validation, authorization, business validation, preview and explicit confirmation.
- Important mutations create immutable audit events.
- Use least privilege and organization scoping.
- Do not implement custom cryptography or a home-grown token format.

## Data and time

- Organization boundary on every business record.
- Default business timezone: `Europe/Budapest`.
- Store unambiguous timestamps; use UTC for instants and explicit local date/time models for recurring business rules.
- Scheduling grid is 30 minutes.
- Employee preferences may be minute-precise.
- Use optimistic concurrency for editable records.
- Inactive locations may retain history/rules but are excluded from active coverage and autofill.

## Development rules

- Prefer small, reviewable commits.
- Add tests with every business rule.
- Do not change legacy scheduling behavior without characterization tests.
- Keep the legacy code read-only until the audit identifies extraction boundaries.
- Run build, unit tests and integration tests before reporting success.
- Do not claim a command passed unless output confirms it.
- Update OpenAPI/contracts and docs when behavior changes.
- Do not combine broad refactors with a business feature.
- Use migrations for schema changes.
- Seed data only in development/test environments.
- Do not add production infrastructure credentials to the repo.

## AI command handling

- Model output must match a versioned JSON schema.
- Reject unknown actions and fields.
- Resolve names to IDs in application code, never trust model-provided database IDs.
- Ask for clarification on ambiguous names, dates, locations or schedules.
- Use idempotency keys for write commands.
- Do not automatically retry a non-idempotent write.
- Raw voice audio is not stored by default.
- Relative dates must be resolved against an explicit current date and `Europe/Budapest`, then shown as concrete dates in preview.

## Pull request checklist

- Acceptance criteria met.
- Tests added and passing.
- Authorization and organization-boundary tests included.
- No secrets.
- Migration reviewed.
- User-facing text remains Hungarian.
- Audit behavior verified.
- API contract and docs updated.
