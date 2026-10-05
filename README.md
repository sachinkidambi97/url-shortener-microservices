# URL Shortener Microservices

A production-quality URL shortening system built as three independent microservices on .NET 8, communicating via Redis (cache), PostgreSQL (persistent storage), and RabbitMQ (async event messaging).

## Architecture Overview

```
                        ┌─────────────────────────────────────────────────┐
                        │                   CLIENTS                        │
                        └────────────┬───────────────┬────────────────────┘
                                     │               │
                          ┌──────────▼──┐    ┌───────▼──────┐   ┌─────────▼──────┐
                          │ Shortening  │    │   Redirect   │   │  Analytics     │
                          │    API      │    │   Service    │   │   Service      │
                          │  :8081      │    │    :8082     │   │    :8083       │
                          │  (JWT auth) │    │  (no auth)   │   │  (JWT auth)    │
                          └──────┬──────┘    └──────┬───────┘   └────────┬───────┘
                                 │                  │                    │
                    ┌────────────┴────┐   ┌─────────┴──────┐  ┌─────────┴───────┐
                    │  shortening_db  │   │  shortening_db │  │  analytics_db   │
                    │  (read+write)   │   │  (read-only)   │  │  (read+write)   │
                    └─────────────────┘   └────────────────┘  └─────────────────┘
                                                 │                        ▲
                                        ┌────────┴────────┐               │
                                        │  Redis Cache    │               │
                                        │  (cache-aside)  │               │
                                        └─────────────────┘               │
                                                 │                        │
                                        ┌────────┴────────┐               │
                                        │    RabbitMQ     ├───────────────┘
                                        │ click.recorded  │  (async event)
                                        └─────────────────┘
```

## Services

| Service | Port | Description |
|---------|------|-------------|
| **Shortening API** | 8081 | Register/login users; shorten, update, delete URLs; internal endpoint for service-to-service |
| **Redirect Service** | 8082 | Resolves short codes to original URLs (302/404/410); publishes click events to RabbitMQ |
| **Analytics Service** | 8083 | Consumes RabbitMQ click events; exposes stats per short code via JWT-protected endpoint |

## Infrastructure

| Component | Port | Purpose |
|-----------|------|---------|
| PostgreSQL 16 | 5432 | Persistent storage (two databases) |
| Redis 7 | 6379 | Cache-aside for URL resolution |
| RabbitMQ 3.13 | 5672 / 15672 | Async click event messaging (management UI on 15672) |

## Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (includes Docker Compose)
- No other local dependencies required

## Quick Start

```bash
git clone <repo-url>
cd url-shortener-microservices
docker compose up --build
```

Wait ~30 seconds for all services to become healthy. Then:

- Shortening API Swagger: http://localhost:8081/swagger
- Analytics API Swagger: http://localhost:8083/swagger
- RabbitMQ Management: http://localhost:15672 (guest/guest)

## API Usage Examples

### 1. Register a user (Shortening API)

```bash
curl -X POST http://localhost:8081/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email": "user@example.com", "password": "secret123"}'
```

Response:
```json
{
  "token": "eyJhbGci...",
  "email": "user@example.com",
  "expiresAt": "2026-10-05T12:00:00Z"
}
```

### 2. Shorten a URL

```bash
TOKEN="eyJhbGci..."

curl -X POST http://localhost:8081/api/shorten \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"url": "https://example.com/very/long/path"}'
```

Response:
```json
{
  "shortCode": "aBc1234",
  "shortUrl": "http://localhost:8081/aBc1234"
}
```

### 3. Redirect (no auth)

```bash
curl -L http://localhost:8082/aBc1234
# → 302 redirect to https://example.com/very/long/path
```

### 4. Get click statistics

```bash
curl http://localhost:8083/api/stats/aBc1234 \
  -H "Authorization: Bearer $TOKEN"
```

Response:
```json
{
  "shortCode": "aBc1234",
  "originalUrl": "https://example.com/very/long/path",
  "totalClicks": 3,
  "lastClickedAt": "2026-10-04T08:30:00Z",
  "recentClicks": [...]
}
```

### 5. Bulk shorten

```bash
curl -X POST http://localhost:8081/api/shorten/bulk \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "items": [
      {"url": "https://example.com/page1"},
      {"url": "https://example.com/page2"}
    ]
  }'
```

### 6. Update a URL

```bash
curl -X PUT http://localhost:8081/api/urls/aBc1234 \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"url": "https://example.com/new-path"}'
```

### 7. Delete a URL

```bash
curl -X DELETE http://localhost:8081/api/urls/aBc1234 \
  -H "Authorization: Bearer $TOKEN"
```

## Endpoint Reference

### Shortening API (:8081)

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/api/auth/register` | None | Register new user |
| POST | `/api/auth/login` | None | Login and get JWT |
| POST | `/api/shorten` | Bearer | Shorten a URL |
| POST | `/api/shorten/bulk` | Bearer | Bulk shorten (up to 100) |
| GET | `/api/urls` | Bearer | List your shortened URLs |
| PUT | `/api/urls/{code}` | Bearer | Update a URL (owner only) |
| DELETE | `/api/urls/{code}` | Bearer | Delete a URL (owner only) |
| GET | `/internal/urls/{code}` | None | Internal: resolve code (used by Analytics) |
| GET | `/health` | None | Health check |

### Redirect Service (:8082)

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/{code}` | None | Redirect to original URL (302/404/410) |
| GET | `/health` | None | Health check |

### Analytics Service (:8083)

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/api/stats/{code}` | Bearer | Get click statistics for a short code |
| GET | `/health` | None | Health check |

## Configuration

All configuration is via environment variables in `docker-compose.yml`. Key variables:

| Variable | Service(s) | Description |
|----------|-----------|-------------|
| `Jwt__SecretKey` | Shortening, Analytics | JWT signing key (min 32 chars in production) |
| `ConnectionStrings__DefaultConnection` | All | PostgreSQL connection string |
| `ConnectionStrings__Redis` | All | Redis connection string |
| `RabbitMQ__Host` | Redirect, Analytics | RabbitMQ hostname |
| `ShorteningApi__BaseUrl` | Analytics | Shortening API internal URL |

## Development

To run locally without Docker (requires Postgres, Redis, RabbitMQ running):

```bash
# Shortening API
cd src/ShorteningApi/UrlShortener.ShorteningApi
dotnet run

# Redirect Service
cd src/RedirectService/UrlShortener.RedirectService
dotnet run

# Analytics Service
cd src/AnalyticsService/UrlShortener.AnalyticsService
dotnet run
```

Each service auto-migrates its database on startup.
