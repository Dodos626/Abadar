# ABADAR Development Progress

## Current Status

ABADAR has moved to Version 1: the matching engine is integrated with the ASP.NET Core API and PostgreSQL durable order/trade history. The React/Next.js administrator workspace can generate market activity and inspect the resulting history.

The engine retains deterministic per-symbol ownership while open books are recovered from PostgreSQL at backend startup. Kafka events, portfolio settlement, replay from an event stream, real-time market data, and observability remain future work.

## Completed Work

### Repository foundation

- Established an `apps/` layout with separate backend and web applications.
- Added a root `Makefile` for common development, validation, build, and Docker commands.
- Added `.gitignore` rules for .NET, Node.js, Next.js, environment, editor, and generated files.
- Added `.env.example` with configurable frontend and backend host ports and the local CORS origin.
- Documented startup commands and current endpoints in the root README.

### C# / .NET backend

Location: `apps/backend`

- Created a C# ASP.NET Core minimal API targeting .NET 10.
- Added environment-based configuration for the port, allowed CORS origin, and application version.
- Uses the ASP.NET Core generic host for graceful shutdown on container and process termination signals.
- Added Kestrel request-header and keep-alive timeouts.
- Added structured JSON request logging.
- Added request ID generation and `X-Request-ID` propagation.
- Added centralized exception handling and consistent JSON error responses.
- Added basic security headers and configurable CORS handling.
- Added liveness and readiness endpoints.
- Added a service information endpoint.
- Added a temporary in-memory simulated market snapshot for frontend integration.
- Added PostgreSQL through the Npgsql EF Core provider.
- Added an EF Core migration for the `users` table.
- Added unique case-insensitive email and username indexes.
- Added dependency-injected user, token, database-initializer, password-hasher, and SignalR event services.
- Added JWT bearer login and `Admin` / `User` role authorization.
- Added full user CRUD for administrators and self-profile read/update for standard users.
- Added ASP.NET Core password hashing; plaintext passwords are never stored.
- Added SignalR broadcasts for user create, update, and delete events.
- Added ASP.NET Core integration tests covering health, market data, routing, CORS, login, authorization, creation, and deletion.
- Added a multi-stage Docker image that runs as a non-root user.

Current backend endpoints:

```text
GET /api/v1
GET /api/v1/health/live
GET /api/v1/health/ready
GET /api/v1/markets
POST /api/v1/auth/login
GET /api/v1/users/me
PUT /api/v1/users/me
GET /api/v1/users
POST /api/v1/users
GET /api/v1/users/:id
PUT /api/v1/users/:id
DELETE /api/v1/users/:id
GET /api/v1/orders
GET /api/v1/trades
POST /api/v1/admin/simulations
POST /api/v1/admin/database/reset
HUB /hubs/users
```

The service is currently named `backend` rather than `gateway`. This is intentional: it directly implements application behavior and does not yet route requests to multiple downstream services. A dedicated API gateway can be introduced when ABADAR is split into independently deployable services.

### React/Next.js frontend

Location: `apps/web`

- Created a Next.js App Router application using React and TypeScript.
- Added a responsive exchange-themed interface.
- Added backend health and availability status.
- Added a simulated market snapshot table.
- Added loading, degraded-service, and empty states.
- Added a login interface with the seeded development credentials.
- Added role-aware user administration and self-profile editing.
- Added an administrator-only Version 1 market simulator with durable order/trade history and database reset controls.
- Added same-origin Next.js route handlers that proxy health, market, authentication, and user requests to the .NET backend.
- Added the SignalR browser client for real-time user updates.
- Configured runtime backend discovery through `BACKEND_URL`.
- Configured standalone Next.js output for container deployment.
- Added ESLint and strict TypeScript validation.
- Added a multi-stage Docker image that runs as a non-root user.

Current frontend proxy endpoints:

```text
GET /api/health
GET /api/markets
ALL /api/backend/[...path]
```

### Version 0 pure matching engine

Location: `apps/matching-engine`

- Added a dependency-free .NET 10 class library for the exchange domain and matching logic.
- Added small domain types for assets, trading pairs, orders, executions, price levels, and snapshots.
- Uses `decimal` for Version 0 price and quantity values; floating-point values are not used.
- Added an in-memory order book with sorted bid/ask price levels and FIFO linked lists per price.
- Added limit orders, market orders, partial fills, full fills, cancellation, and snapshots.
- Added deterministic order and execution sequences.
- Trades execute at the resting order's price.
- Reused order IDs are rejected, including after fill or cancellation.
- Added one `System.Threading.Channels` worker per symbol. Each worker is the sole owner of its mutable order book, so the order book does not need locks.
- Different symbols can process independently while commands for one symbol remain sequential.
- Added correctness and concurrency tests covering matching, price priority, FIFO, limits, cancellation, duplicate IDs, snapshots, sequencing, quantity preservation, and symbol isolation.
- Added a small Release-mode benchmark executable with warm-up, throughput, p50/p95/p99 processing time, and managed allocation output.
- Added single-line comments to the Version 0 types, functions, tests, and benchmark helpers so each addition states its purpose.

Version 0 commands:

```bash
make test-engine
make benchmark-engine
```

The benchmark is intentionally simple and dependency-free. It measures the pure synchronous order-book path, not HTTP, persistence, settlement, or end-to-end latency.

### Version 1 exchange persistence and administration

- Referenced the matching-engine project from the backend and registered one singleton engine.
- Added durable `orders` and `trades` tables through an EF Core migration.
- Persisted every simulated order result, resting-order update, and execution.
- Added authenticated order and trade history endpoints.
- Added startup recovery for open and partially-filled orders, FIFO ordering, order sequences, and execution sequences.
- Added an administrator-only simulation endpoint with symbol, order range, price range, quantity range, sell percentage, market-order percentage, and seed controls.
- Added an administrator-only reset endpoint that preserves the calling administrator and deletes all other users and exchange data.
- Added a Next.js administrator simulator with history tables and guarded destructive reset controls.
- Batched simulation persistence in groups of 250 orders so 2,500–5,000 order runs do not issue one PostgreSQL commit per order.
- Extended the Next.js proxy timeout for market simulations to 120 seconds and now distinguish operation timeouts from an unavailable backend.
- Added concise one-line comments across the Version 1 backend models, contracts, endpoint mappings, exchange service methods, persistence configuration, migration, integration tests, matching-engine recovery additions, frontend API types, simulator components, administrator navigation, Dockerfile, Compose services, and root Docker ignore rules.
- Kept generated EF Core designer and model-snapshot files unchanged because they are regenerated from the annotated source model and should not be hand-edited.

Current Version 1 backend endpoints:

```text
GET  /api/v1/orders
GET  /api/v1/trades
POST /api/v1/admin/simulations
POST /api/v1/admin/database/reset
```

The simulator currently supports `BTC/USD`, `ETH/USD`, `SOL/USD`, and `ABR/USD`, bounded order/price/quantity ranges, buy/sell distribution, market-order distribution, and deterministic seeds. Database reset is administrator-only and preserves the authenticated administrator account while clearing all other users, durable orders, durable trades, and active in-memory books.

### Container orchestration

Location: `docker-compose.yml`

- Added `postgres`, `backend`, and `frontend` services.
- Added a named PostgreSQL data volume and database health check.
- Added an isolated Docker bridge network.
- Added configurable host port mappings.
- Added health checks for PostgreSQL, the backend, and the frontend.
- Configured the frontend to wait for a healthy backend.
- Configured the backend to wait for a healthy PostgreSQL database.
- Configured internal service discovery through `http://backend:8080`.
- Added restart policies.

The complete stack starts with:

```bash
cp .env.example .env
docker compose up --build
```

## Validation Completed

The current implementation has been validated with:

- .NET 10 restore and production build
- 12 ASP.NET Core integration tests, including durable restart recovery and multi-batch simulation
- EF Core migration generation
- NuGet transitive dependency vulnerability scan
- ESLint
- TypeScript type checking
- Next.js production build
- Production dependency audit
- Frontend-to-backend integration through Docker Compose
- Standalone Next.js production server test
- Backend readiness and market endpoint requests
- Git whitespace and patch validation
- Version 0 matching-engine Release build
- 17 matching correctness and concurrency tests
- Baseline pure-engine benchmark execution
- Recorded three baseline benchmark runs in `docs/benchmarks/version-0-baseline.md`

The .NET 10 SDK is installed in WSL under the current user's home directory and configured in the shell profile. The expanded PostgreSQL, backend, and frontend Compose stack has been built and validated with all services healthy.

## Deliberately Not Implemented Yet

The following components are intentionally not part of the bootstrap:

- Kafka or another event-streaming platform
- Redis
- Portfolio and balance management
- WebSocket market data
- Durable domain events
- Event replay and snapshots
- Prometheus metrics
- OpenTelemetry tracing
- Grafana dashboards
- Load and chaos testing

These capabilities should be introduced incrementally when their application requirements and ownership boundaries are defined, rather than adding infrastructure without active use cases.

## Recommended Next Milestone

Version 1 should now be hardened before moving to Kafka or portfolio settlement:

1. Add randomized invariants over durable order and trade streams.
2. Add order cancellation persistence and recovery tests through the HTTP API.
3. Make each engine mutation and its database write atomic through an explicit durability design, such as an append-only command log or transactional outbox.
4. Add balances, reservations, and conservation tests before exposing user-submitted trading.
5. Add real-time order-book and trade updates only after durable mutation semantics are documented.

Event streaming should follow once Version 1 recovery, idempotency boundaries, and financial invariants are explicit and tested.

## High-Level Progress Summary

| Area | Status |
| --- | --- |
| Repository structure | Complete |
| C# ASP.NET Core backend migration | Complete |
| Next.js frontend bootstrap | Complete |
| Frontend/backend integration | Complete |
| Docker Compose definition | Complete |
| Local code validation | Complete |
| PostgreSQL and EF Core | Complete |
| JWT login and role authorization | Complete |
| User CRUD and profile management | Complete |
| SignalR user updates | Complete |
| Docker runtime validation | Complete |
| Exchange domain model | Version 0 complete |
| Order book and matching engine | Version 0 complete |
| Matching correctness tests | Complete (17 passing) |
| Symbol-worker concurrency | Complete |
| Pure-engine baseline benchmark | Complete |
| Exchange persistence and recovery | Version 1 complete |
| Admin market simulator and database reset | Complete |
| Event streaming | Not started |
| Real-time WebSocket data | Not started |
| Production observability | Not started |
| Load, recovery, and chaos testing | Not started |