# Jobs Reliability and Concurrency

## Reliability Principles

- Maintenance and sync jobs should tolerate transient cloud/service failures.
- Use bounded retries with delay, not unbounded loops.
- Prefer exponential backoff with jitter for throttling and contention.

## Retry Guidance

- Retry only transient failures (timeouts, throttling, temporary connectivity).
- Fail fast on non-transient errors (validation/data contract errors).
- Set maximum attempts and total retry window per operation.
- Log interim retry attempts as informational; log terminal failure as error.

## Idempotency

- Design job steps to be safe when repeated.
- Use idempotent writes or conflict-aware updates where possible.
- Be explicit about side effects that must not run twice.

## Concurrency and Race Mitigation

- Use optimistic concurrency checks (ETag/if-match) for read-modify-write paths.
- Segment work by partition key to reduce write contention.
- Use bounded local concurrency for external API calls to avoid bursts.

## Operational Guidance

- Keep progress/state checkpoints for long-running jobs.
- Support resume/restart without corrupting stored data.
- Emit enough telemetry to diagnose throttling, retries, and dead-letter outcomes.
