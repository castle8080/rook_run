---
applyTo: "**/*.cs,**/*.csproj"
---

# .NET and C# Guidance

- Prefer immutable `record` types for data carrier classes.
- Keep API DTO contracts separate from internal domain and persistence models.
- Favor typed exceptions when callers can take meaningful, different actions.
- Add or update tests with behavior changes.
- Document newly created classes, including methods and private methods.

See:

- `docs/ai/coding-conventions.md`
- `docs/ai/exceptions-and-error-handling.md`
- `docs/ai/feature-workflow.md`
