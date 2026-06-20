# Object Store Paths and Serialization

## Persistence Direction

- Prefer `IObjectStore` as the default persistence mechanism.
- Design repositories around low-cost cloud storage assumptions (Azure Blob friendly).

## Canonical Path Pattern

Use a stable, discoverable path shape:

`{domain}/{resource}/{partition}/{id}[/{subresource}].{format}.{compression}`

Examples:

- `activities/strava_detail/{activityId}.json.br`
- `activities/strava_images/{activityId}/{imageId}.{ext}`

Guidance:

- Use lowercase path segments and stable identifiers.
- Keep partition strategy explicit to avoid expensive list operations.
- Include a schema/version segment when a format is likely to evolve.

## Serialization

- Default structured object serialization: JSON + Brotli (`.json.br`).
- Keep serialization options centralized and consistent.
- Favor backward-compatible model changes where possible.

## Index and Cache Files

- Use alternate index files to accelerate common listing/filter queries.
- Treat indexes as derivable artifacts; source-of-truth remains canonical objects.
- Define index rebuild strategies for maintenance jobs.
- Document cache invalidation assumptions at repository level.

## Concurrency and Safety

- Prefer optimistic concurrency (ETag/if-match) when writes can race.
- Keep repository writes idempotent where feasible.
- For high-contention paths, combine retries with backoff and bounded attempts.
