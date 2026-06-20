---
applyTo: "RookRun.Job/**/*.cs,RookRun.Strava/Sync/**/*.cs,RookRun.Api/Jobs/**/*.cs,RookRun.Cli/**/*.cs"
---

# Job and Synchronizer Guidance

- Distinguish transient failures from terminal failures.
- Use bounded retries and backoff for transient faults and throttling.
- Keep long-running steps restart-safe and idempotent.
- Prefer explicit race mitigation for read-modify-write flows.

See:

- `docs/ai/jobs-reliability-and-concurrency.md`
- `docs/ai/objectstore-paths-and-serialization.md`
