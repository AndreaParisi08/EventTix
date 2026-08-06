# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

The repo root has `EventTix.slnx` (the newer XML-based solution format, not `.sln`), so `dotnet build`/`dotnet restore` from the repo root work and build all four projects.

```bash
# Build everything from the root via EventTix.slnx
dotnet build
dotnet build --configuration Release

# Or build a single project directly
dotnet build src/Services/Booking/EventTix.Booking.Api

# Run the API (auto-applies EF Core migrations on startup in Development)
dotnet run --project src/Services/Booking/EventTix.Booking.Api

# Infrastructure (PostgreSQL 16, Redis 7, RabbitMQ 3.12, MockServer)
docker compose up -d
# Also start the containerized API itself (built from src/Services/Booking/EventTix.Booking.Api/Dockerfile):
docker compose --profile full up -d

# EF Core migrations (run from the Api project so appsettings.json connection strings resolve)
dotnet ef migrations add <Name> --project src/Services/Booking/EventTix.Booking.Infrastructure --startup-project src/Services/Booking/EventTix.Booking.Api
dotnet ef database update --project src/Services/Booking/EventTix.Booking.Infrastructure --startup-project src/Services/Booking/EventTix.Booking.Api
```

No test project exists yet. When tests are added, use `dotnet test` and `dotnet test --filter "FullyQualifiedName~<TestName>"` for single tests. The README lists xUnit, FluentAssertions, Testcontainers, and k6 as the intended stack, but none are wired up yet.

Local infra endpoints once `docker compose up -d` is running:
- RabbitMQ management UI: `http://localhost:15672` (`eventtix` / `eventtix_dev_password`)
- Mock webhook/payment gateway (MockServer): `http://localhost:1080`
- Swagger UI is enabled only in Development, served at the API's root.

## Architecture

This is an event ticketing system built with **Clean Architecture + DDD**, targeting **.NET 10**. The only bounded context implemented is **Booking** (the core domain). Only one use case — reserving a seat — is wired end-to-end; everything else described below is either domain-modeled-but-unused or not yet built (see "Not yet implemented").

### Layer order (dependency direction: inward only)

```
EventTix.Booking.Api          → Minimal API endpoints, DI wiring, exception-handling middleware, health checks
EventTix.Booking.Application  → CQRS commands via MediatR, FluentValidation, pipeline behaviors, port interfaces
EventTix.Booking.Infrastructure → EF Core (PostgreSQL/Npgsql), StackExchange.Redis, repository + unit-of-work impls
EventTix.Booking.Domain       → Aggregates, entities, value objects, domain events — zero external deps
```

Projects live under `src/Services/Booking/`. Each layer has its own `DependencyInjection.cs` exposing an `Add*Services(...)` extension method, composed in `EventTix.Booking.Api/Program.cs`.

### Request flow (the one implemented path)

`POST /api/bookings` (`BookingEndpoints.cs`) builds a `ReserveSeatCommand` (falling back to a fresh GUID if the client omits `X-Idempotency-Key`) and dispatches it through MediatR. The pipeline runs, in registration order (`Application/DependencyInjection.cs`):

1. `ValidationPipelineBehavior<TRequest,TResponse>` — runs all FluentValidation validators for the request; throws `ValidationException` on failure.
2. `IdempotentCommandBehavior<TRequest,TResponse>` — only engages for requests implementing `IIdempotentCommand<TResponse>`. Looks up `idempotency:{key}` in Redis; if present, short-circuits and returns the cached JSON response instead of re-running the handler. Otherwise runs the handler and caches its response for 24h.

`ReserveSeatCommandHandler` then: acquires a Redis lock on `lock:seat:{seatId}` (`IDistributedLockService`, 5s TTL / 1s wait — throws `SeatAlreadyLockedException` on failure), checks `IBookingRepository.IsSeatReservedAsync`, creates the `Booking` aggregate, and persists it via `IBookingRepository.AddAsync` + `IUnitOfWork.SaveChangesAsync` (a thin wrapper over `BookingDbContext.SaveChangesAsync`).

`GlobalExceptionHandler` (registered as the single `IExceptionHandler`) maps exceptions to RFC 7807 `ProblemDetails`: `ValidationException` → 400 (field-level errors), `SeatAlreadyLockedException` → 409, `InvalidOperationException` → 409, `ArgumentException` → 400, everything else → 500.

### Domain model

- **Booking** is the root aggregate (`Domain/Entities/Booking.cs`). Created via `Booking.CreatePending(...)`, which emits `BookingReservedDomainEvent` and sets a 5-minute hold window (`ExpiresAt`). Domain events are collected on the aggregate but nothing currently dispatches/consumes them — there is no outbox or event publisher wired up yet.
- **BookingStatus**: `Pending` → `Confirmed` | `Cancelled` | `Expired`. `Confirm()` throws if the booking isn't `Pending` or the hold has expired; `Cancel()` throws if the booking is already `Confirmed`. Note: only `CreatePending` is ever called by application code today — `Confirm`/`Cancel` have no command/handler yet.
- **Value objects**: `SeatId` (uppercase-normalized string), `UserId` (non-empty GUID), `Money` (amount + currency, with `Money.EUR()` factory).
- Base types: `Entity<TId>` (ID equality), `AggregateRoot<TId>` (domain event collection).

### Key infrastructure patterns — status

- **Distributed locking:** implemented in `RedisDistributedLockService` using `IDatabase.LockTakeAsync`/`LockReleaseAsync` against a single Redis instance with polling backoff. This is single-node Redis locking, not the multi-node Redlock quorum algorithm the README/ADR-0002 describe — treat that as the target design, not the current implementation.
- **Transactional Outbox, MassTransit Saga, Webhook delivery (Polly/HMAC/rate limiting):** designed only (see ADRs), not implemented. There is no `OutboxMessages` table, no saga state machine, and no webhook delivery code in the repo yet.

### Other bounded contexts (designed, not yet implemented)

- **Catalog** – event/venue/seat management (supporting)
- **Outbox** – reliable event publishing (generic)
- **Webhook & Payment Saga** – integration layer

### Decision records

See `docs/adr/` for key decisions:
- `0001`: Saga orchestration chosen over choreography (explicit vs. implicit state)
- `0002`: Redis Redlock chosen over SQL locking

### Development status

Domain layer has the core `Booking` aggregate. Application and Infrastructure layers implement one full slice (reserve-seat) with the supporting MediatR pipeline, repository, and unit-of-work; everything outside that slice (saga, outbox, webhooks, Catalog context, `Confirm`/`Cancel` use cases) is still TODO. `Api/Program.cs` DI/middleware wiring is complete for what exists today (health checks at `/health/live` and `/health/ready`, Swagger in Development, global exception handling).
