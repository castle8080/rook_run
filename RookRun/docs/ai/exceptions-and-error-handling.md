# Exceptions and Error Handling

## Goals

- Improve handling clarity by using meaningful exception categories.
- Preserve enough context for retries, alerts, and diagnostics.

## Exception Taxonomy Guidance

Prefer a small set of typed exception categories:

- Validation and input errors
- External dependency/transient failures
- Concurrency/precondition failures
- Not found and missing state
- Authorization/authentication failures

## Placement

- Shared reusable exception types: `RookRun.Common/Exceptions`
- Domain-specific exception types: domain project folder close to usage

## Handling Guidance

- Throw typed exceptions at boundaries where callers can take specific action.
- Avoid swallowing exceptions without adding value.
- Wrap low-level exceptions only when adding actionable context.
- Preserve original exception as inner exception when rethrowing with context.

## API and Job Behavior

- API layer should translate internal exceptions to appropriate HTTP semantics.
- Job/synchronizer flows should distinguish retriable vs terminal failures.
- Retries should not hide persistent faults; surface final failure clearly.
