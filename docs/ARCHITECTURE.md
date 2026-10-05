# Architecture

## Decomposition Rationale

The monolith was decomposed into three bounded contexts:

1. **Shortening API** — owns URL creation and user identity. It is the only service that writes to the `shortening_db` and is the authority for what short codes exist.

2. **Redirect Service** — hot path. It reads URLs (read-only projection of `shortening_db`) and emits click events asynchronously. Kept small and dependency-light so it can be scaled independently.

3. **Analytics Service** — owns the `analytics_db` and the click event domain. It consumes events from RabbitMQ and exposes read-only stats.

The separation follows **Database per Service**: each service has a well-defined schema boundary. The Redirect Service reads from `shortening_db` via a read-only EF Core context with no migrations and no write operations — it is a read replica consumer.

## Database Ownership

| Database | Owner | Readers |
|----------|-------|---------|
| `shortening_db` (table: `shortened_urls`, `users`) | Shortening API | Redirect Service (read-only, no EF migrations) |
| `analytics_db` (table: `click_events`) | Analytics Service | — |

The Redirect Service reads `shortening_db` directly for low-latency redirect resolution (after a Redis cache miss). This is a pragmatic trade-off over adding a REST hop; the access is explicitly read-only and uses `NoTracking` globally.

The Analytics Service does **not** have a direct foreign key to `shortened_urls`. It stores `ShortCode` as a plain string. When it needs the original URL (for the stats response), it calls the Shortening API's `/internal/urls/{code}` endpoint over HTTP. This preserves loose coupling at the cost of one extra HTTP call per stats request (mitigated by Polly retry).

## Message Flow

```
User                Redirect Service           RabbitMQ                Analytics Service
 |                       |                        |                           |
 |-- GET /{code} ------->|                        |                           |
 |                       |-- Redis GET (cache) -->|                           |
 |                       |<-- cache miss          |                           |
 |                       |-- PG SELECT ---------->|                           |
 |                       |<-- ShortenedUrl        |                           |
 |                       |-- Redis SET (cache) -->|                           |
 |<-- 302 Redirect ----  |                        |                           |
 |                       |-- BasicPublish ------->|                           |
 |                       |    click.recorded      |-- EventingBasicConsumer ->|
 |                       |                        |                           |-- INSERT click_events
```

### Exchange and Queue

- Exchange: `url-shortener` (topic, durable)
- Routing key: `click.recorded`
- Queue: `analytics-click-events` (durable, manual ack)

The Analytics consumer uses `prefetchCount: 1` and manual acknowledgement. A failed message is `BasicNack`'d with `requeue: false` to avoid infinite retry loops (configure a dead-letter exchange in production).

## Resilience

| Layer | Mechanism |
|-------|-----------|
| PostgreSQL | Polly retry (3x, exponential backoff) on transient `NpgsqlException` |
| Redis | Polly retry (3x, exponential backoff) on `RedisConnectionException` |
| HTTP (Analytics → Shortening API) | `Polly.Extensions.Http` `HandleTransientHttpError()` retry (3x) |
| RabbitMQ connections | `AutomaticRecoveryEnabled = true` on `ConnectionFactory` |
| Click event publishing | Non-fatal: wrapped in try/catch; redirect still completes on publish failure |

## Caching Strategy

Cache-aside pattern in the Redirect Service:

1. Check Redis for `url:{shortCode}`.
2. On hit: use cached value, publish click event, return 302.
3. On miss: query PostgreSQL, populate Redis with TTL (default: 1 hour), publish click event, return 302.
4. On update/delete (Shortening API): `RemoveAsync` evicts the cache key.

## Security

- JWT HS256 signed tokens issued by the Shortening API.
- The same `Jwt__SecretKey` is shared between Shortening API and Analytics Service for token validation.
- The `/internal/urls/{code}` endpoint on the Shortening API is unauthenticated — it is on the internal Docker network and not exposed externally.
- In production: place an API gateway / reverse proxy in front and block `/internal/*` from external traffic.

## Trade-offs

| Decision | Chosen | Alternative | Reason |
|----------|--------|-------------|--------|
| Shared DB for redirect reads | Direct PG read (read-only) | HTTP to Shortening API | Latency; one HTTP call per redirect is prohibitive at scale |
| Short code storage in analytics | ShortCode as string (no FK) | FK to `shortened_urls` | Database isolation; analytics DB must not depend on shortening schema |
| RabbitMQ client | `RabbitMQ.Client` v6 | MassTransit | Minimal dependency; full control over exchange/queue declarations |
| EF Core for Redirect Service | NoTracking read-only context | Raw Dapper | Consistency with rest of codebase; NoTracking is nearly as fast |
| JWT shared key | Symmetric HS256 | Asymmetric RS256 | Simpler setup for local/dev; upgrade to RS256 for production key rotation |
