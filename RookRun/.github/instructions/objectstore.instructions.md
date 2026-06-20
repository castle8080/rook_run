---
applyTo: "RookRun.ObjectStore/**/*.cs,RookRun.Strava/Repositories/**/*.cs,RookRun.Strava/Sync/**/*.cs,RookRun.Job/**/*.cs"
---

# Object Store and Repository Guidance

- Prefer `IObjectStore` for persistence abstractions.
- Keep object paths stable, lowercase, and partition-aware.
- Default structured object persistence to JSON + Brotli.
- Use index/cache artifacts for read acceleration where justified.
- Use optimistic concurrency and idempotent write patterns where races are possible.

See:

- `docs/ai/objectstore-paths-and-serialization.md`
- `docs/ai/jobs-reliability-and-concurrency.md`
