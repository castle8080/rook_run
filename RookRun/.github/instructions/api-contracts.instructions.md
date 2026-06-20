---
applyTo: "RookRun.Api/**/*.cs,RookRun.Contracts/**/*.cs,RookRun.Web/Services/**/*.cs"
---

# API and Contracts Guidance

- Keep request/response DTOs in `RookRun.Contracts`.
- Do not reuse persistence/domain types as external API contracts.
- Keep endpoint behavior and contract evolution backward compatible where practical.
- Update API and client-side usage together when contracts change.

See:

- `docs/ai/project-map.md`
- `docs/ai/coding-conventions.md`
- `docs/ai/exceptions-and-error-handling.md`
