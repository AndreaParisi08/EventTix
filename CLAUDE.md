# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build
dotnet build
dotnet build --configuration Release

# Run API
dotnet run --project src/Services/Booking/EventTix.Booking.Api

# Infrastructure (PostgreSQL 16, Redis 7, RabbitMQ 3.12, MockServer)
docker compose up -d
```

No test project exists yet. When tests are added, use `dotnet test` and `dotnet test --filter "FullyQualifiedName~<TestName>"` for single tests.

## Architecture

This is an event ticketing system built with **Clean Architecture + DDD**, targeting **.NET 10**. The only bounded context implemented is **Booking** (the core domain).

### Layer order (dependency direction: inward only)

```
EventTix.Booking.Api          → HTTP endpoints, DI wiring, middleware
EventTix.Booking.Application  → CQRS commands/queries, FluentValidation, use-case orchestration
EventTix.Booking.Infrastructure → EF Core (PostgreSQL), StackExchange.Redis, MassTransit/RabbitMQ
EventTix.Booking.Domain       → Aggregates, entities, value objects, domain events — zero external deps
```

Projects live under `src/Services/Booking/`.

### Domain model

- **Booking** is the root aggregate (`Entities/Booking.cs`). On creation it emits `BookingReservedDomainEvent` and sets a 5-minute hold window.
- **BookingStatus**: `Pending` → `Confirmed` | `Cancelled` | `Expired`. Expired bookings cannot be confirmed; confirmed bookings cannot be cancelled without a refund.
- **Value objects**: `SeatId` (uppercase-normalized string), `UserId` (non-empty GUID), `Money` (amount + currency, with `Money.EUR()` factory).
- Base types: `Entity<TId>` (ID equality), `AggregateRoot<TId>` (domain event collection).

### Key infrastructure patterns

**Distributed locking (Redlock):** Redis-based seat locks with 5-minute TTL. Fast-fail on contention → 409 Conflict. Prevents race conditions before any DB write.

**Transactional Outbox:** `OutboxMessages` + `Orders` written atomically in one transaction → guaranteed at-least-once delivery to RabbitMQ without 2PC.

**MassTransit Saga:** Centralized state machine for `Pending → PaymentProcessing → Confirmed/Failed`. Handles automatic seat release on payment failure.

**Webhook delivery:** Polly exponential backoff + circuit breaker, HMAC-SHA256 per-event signatures, Redis token-bucket rate limiting via Lua scripts.

### Other bounded contexts (designed, not yet implemented)

- **Catalog** – event/venue/seat management (supporting)
- **Outbox** – reliable event publishing (generic)
- **Webhook & Payment Saga** – integration layer

### Decision records

See `docs/adr/` for key decisions:
- `0001`: Saga orchestration chosen over choreography (explicit vs. implicit state)
- `0002`: Redis Redlock chosen over SQL locking

### Development status

The project is in early scaffolding. Domain layer has core classes. Application and Infrastructure layers are stubs (`Class1.cs` placeholders). API `Program.cs` has TODO sections for DI and middleware wiring.
