---
applyTo: "RookRun.Api/**/*.cs,RookRun.Contracts/**/*.cs,RookRun.Cli/**/*.cs,RookRun.Common/**/*.cs,RookRun.Job/**/*.cs,RookRun.ObjectStore/**/*.cs,RookRun.Strava/**/*.cs,RookRun.Web/**/*.razor,RookRun.Web/**/*.razor.cs,RookRun.UnitTest/**/*.cs,RookRun.Web.UnitTest/**/*.cs"
---

# AI Code Review Instructions

Use these instructions when reviewing code changes in this repository.

## Review Goals

- Find correctness, security, reliability, and performance issues.
- Identify missing tests for behavior changes and edge cases.
- Flag changes that drift outside stated task scope.
- Prefer high-signal findings over broad style suggestions.

## Severity Model

Classify each finding:

- Critical: data loss/corruption, auth bypass, secret exposure, destructive behavior
- High: correctness bugs, race conditions, unsafe retry/idempotency behavior, broken API contracts
- Medium: maintainability risks likely to cause future bugs, performance regressions, weak test coverage
- Low: readability and minor cleanup suggestions

## Output Format

For each finding, include:

- Severity
- Location (file + symbol or line)
- Issue summary
- Why it matters (user or operational impact)
- Suggested fix (concrete and minimal)
- Confidence (high/medium/low)

## Repository-Specific Review Focus

- API DTO separation: ensure API contracts stay in `RookRun.Contracts` and are not replaced by internal persistence models.
- Object store persistence: prefer `IObjectStore` patterns and stable path conventions.
- Serialization conventions: default structured persistence should remain JSON + Brotli where applicable.
- Jobs and sync reliability: verify retry/backoff behavior is bounded and idempotency/concurrency safety is considered.
- UI composition: for non-trivial page changes, verify component extraction and corresponding UI tests.
- Exception handling: prefer typed exceptions where differentiated handling is required.

## Review Behavior

- Prioritize Critical and High findings first.
- Report only issues with plausible impact.
- Do not block on pure style nits unless they hide correctness or maintainability risk.
- Call out missing validation evidence when behavior changed but tests were not updated.
