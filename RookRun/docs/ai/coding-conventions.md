# Coding Conventions

## C# Data Types

- Prefer immutable `record` types for data carrier classes.
- Use classes when mutation or framework constraints require it.
- Keep request/response DTOs separated in `RookRun.Contracts`.

## Project Boundaries

- Keep API-layer DTO contracts separate from persistence/domain models.
- Avoid leaking object-store schema details into API contracts.
- Favor narrow interfaces between projects to keep dependencies clear.

## Exceptions

- Prefer typed exceptions for meaningful, actionable failure categories.
- Place reusable cross-project exception types in `RookRun.Common/Exceptions`.
- Include contextual data in exception messages to speed diagnosis.

## UI Composition

- Keep route pages focused on orchestration and layout.
- Extract complex sections into child components to reduce page complexity.
- Place component tests in `RookRun.Web.UnitTest` alongside relevant page test areas.

## Documentation in Code

- Document newly created classes with comments.
- Include comments for methods and private methods when introducing new classes.
- Keep comments concise and focused on intent/behavior.

## Testing

- Add or update tests with each behavior change.
- Prefer focused tests near the changed area before running broad suites.
