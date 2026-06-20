# Copilot Instructions

Use this file as the global, concise guidance for AI coding tasks in this repository.

## Working Approach

- Ask clarifying questions before implementation unless the request is trivial.
- Break work into small targeted steps and test as steps complete.
- Keep work notes up to date in `var/tasks/{task}.md`.
- Run an internal review pass with static analysis, security, and clean-code checks.

## Core Conventions

- Prefer immutable records for data carrier types.
- Keep API DTO contracts separated from internal domain/persistence models.
- Prefer typed exceptions when they improve handling and caller behavior.
- Prefer `IObjectStore`-based persistence and stable object path conventions.
- Break complex UX pages into smaller components and cover UI behavior with tests.

## Project-Specific Rules

- In `RookRun.UnitTest`, Strava-related test files should live under a Strava folder/namespace.
- Document newly created classes with comments, including methods and private methods.

## Architecture and Workflow Docs

- `docs/ai/README.md`
- `docs/ai/project-map.md`
- `docs/ai/coding-conventions.md`
- `docs/ai/exceptions-and-error-handling.md`
- `docs/ai/objectstore-paths-and-serialization.md`
- `docs/ai/jobs-reliability-and-concurrency.md`
- `docs/ai/feature-workflow.md`

## Path-Specific Instructions

- `.github/instructions/dotnet.instructions.md`
- `.github/instructions/api-contracts.instructions.md`
- `.github/instructions/objectstore.instructions.md`
- `.github/instructions/jobs.instructions.md`
- `.github/instructions/web-ui.instructions.md`
- `.github/instructions/tests.instructions.md`
- `.github/instructions/ai-review.instructions.md`