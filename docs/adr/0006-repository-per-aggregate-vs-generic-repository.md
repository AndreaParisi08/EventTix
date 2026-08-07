# ADR-0006: Repository per Aggregate Root vs. Generic `IRepository<T>`

---

## Context and Problem Statement
The Booking context currently exposes a single, hand-written port — `IBookingRepository` (`AddAsync`, `GetByIdAsync`, `IsSeatReservedAsync`) — plus a thin `IUnitOfWork` wrapping `BookingDbContext.SaveChangesAsync`. Both are implemented in Infrastructure over EF Core.

EventTix is planned to grow well beyond this single slice: additional aggregates inside Booking (`Order`, `OutboxMessage`, saga state), and additional bounded contexts (Catalog, Webhook Delivery), each with its own DbContext and database schema. Multi-tenancy applies across all of them.

The question is whether to keep one explicitly-written repository interface per aggregate root, or to introduce a generic `IRepository<T>` contract with a shared `Repository<T>` implementation and per-entity subclasses — the pattern most commonly shown in .NET layered-architecture tutorials — in order to avoid perceived duplication as the number of entities grows.

Two constraints from existing decisions shape the answer:
* **ADR-0004** already assigns all high-frequency read paths to Dapper, leaving EF Core responsible only for aggregate writes.
* **ADR-0003** establishes Redis as the concurrency-control mechanism, which (see Consequences) shifts correctness guarantees onto database constraints rather than repository code.

---

## Decision Drivers
* Preservation of DDD aggregate boundaries — write access must funnel through aggregate roots so invariants in `Booking.Confirm()` / `Cancel()` cannot be bypassed.
* Explicit, reviewable SQL behaviour on the write path (tracking, `Include`, split queries) — no hidden query cost.
* A single, reliable place to enforce cross-cutting persistence concerns: transactional Outbox writes (EPIC-03), tenant isolation, auditing.
* Testability that reflects real behaviour — the seat-contention scenario is only meaningfully verifiable against a real PostgreSQL instance (Testcontainers), not against a mocked repository.
* Scalability of the codebase across many aggregates and several bounded contexts without creating deployment coupling between services.

---

## Considered Options
1. **Option 1 — Generic contract:** `IRepository<T>` in a shared project, generic `Repository<T>` implementation, per-entity subclasses adding specialised methods.
2. **Option 2 — Repository per aggregate root:** A hand-written, non-generic interface per aggregate, declared in that context's Application layer, implemented in its Infrastructure layer.
3. **Option 3 — No repositories:** Command handlers depend directly on `DbContext`; read handlers depend directly on Dapper.

---

## Decision Outcome
Chosen Option: **Option 2 (repository per aggregate root)**, with an optional `internal abstract RepositoryBase<TAggregate, TId>` in Infrastructure used purely to remove constructor boilerplate — never exposed as a public generic contract.
The decisive argument against Option 1 is that `DbContext` **is already a Unit of Work** and `DbSet<T>` **is already a generic repository** — both patterns are implemented natively by EF Core (identity map, change tracking, transactional `SaveChanges`). A generic `IRepository<T>` is therefore an abstraction of identical shape layered over an existing one: it adds code without adding capability.
Worse, it is actively harmful in a DDD context. A per-entity generic contract grants direct write access to non-root entities, allowing child entities to be mutated outside the aggregate root and bypassing the invariants the root exists to protect. Any generic repository useful enough to justify itself eventually exposes `IQueryable<T>` or `Expression<Func<T, bool>>`, which leaks EF Core's LINQ-provider semantics into the Application layer — at which point the abstraction protects nothing and merely obscures the SQL actually executed.
The two conventional justifications do not survive scrutiny here. *ORM portability* is a hypothetical that a generic repository would not deliver anyway, since provider differences live precisely in the surface it leaks. *Mockability* produces tests asserting that methods were called rather than that concurrent seat reservation behaves correctly; per ADR-0003 that scenario requires an integration test regardless.
Finally, ADR-0004 removes the only real motivation. With reads served by Dapper, the EF Core write model needs to load one aggregate by identity, mutate it, and persist it — roughly three methods per aggregate. The query variety that makes a generic repository attractive is, by construction, absent from the write side.
Option 3 was rejected because named ports keep the Application layer free of EF Core references and give the domain a place to express persistence intent in ubiquitous language (`IsSeatReservedAsync` reads as a domain question; an inline LINQ predicate does not).

---

## Positive and Negative Consequences

### Positive
* **Aggregate boundaries are structurally enforced.** Only aggregate roots have repositories, so there is no supported route to persist a child entity independently of its root.
* **Every write-path query is explicit and reviewable.** Indexing decisions and tracking behaviour are visible at the call site instead of hidden behind a generic predicate.
* **Cross-cutting concerns land in the correct layer.** Outbox materialisation of domain events, audit stamping and tenant filtering are implemented via `SaveChangesInterceptor` and EF Core global query filters on the DbContext — not in repositories. This is essential because Dapper read paths (ADR-0004) never traverse repository code, so any concern enforced there would silently not apply to reads.
* **Per-context DbContext isolation stays clean.** Each bounded context owns its own DbContext and schema; no shared persistence contract has to be parameterised over context type.
* **Interfaces read as domain vocabulary,** keeping the Application layer aligned with the ubiquitous language defined in `docs/architecture/bounded-contexts.md`.

### Negative / Trade-offs
* **Some structural repetition** across aggregates (constructor, `Add`, `GetByIdAsync`).
  * *Mitigation:* an `internal abstract RepositoryBase<TAggregate, TId>` in `Infrastructure/Persistence/Repositories`. Introduced only once 4–5 aggregates exist — below that threshold it does not pay for itself. It shares implementation, never contract.
* **More files as the aggregate count grows.**
  * *Mitigation:* accepted deliberately. File count is a poor proxy for complexity; each file here is small, single-purpose and independently reviewable.
* **No compile-time guarantee that a repository is not written for a non-root entity.**
  * *Mitigation:* constrain repository implementations to `where TAggregate : AggregateRoot<TId>`, making the rule enforceable by the type system.
* **Redis locking alone does not make the write path correct,** and no repository shape can fix that. A TTL-based lock (ADR-0003) can expire mid-transaction under GC pause or database latency, admitting a second writer for the same seat.
  * *Mitigation:* correctness is enforced in PostgreSQL via a partial unique index on active bookings; the Redis lock is retained as a fast-fail efficiency mechanism, not as the source of truth.

    ```sql
    CREATE UNIQUE INDEX ux_bookings_active_seat
      ON bookings ("SeatId")
      WHERE "Status" IN (0, 1); -- Pending, Confirmed
    ```

    `DbUpdateException` carrying a unique-violation is translated to HTTP 409 by `GlobalExceptionHandler`.

---

## Implementation Notes

**Placement.** Repository interfaces are ports belonging to their consumer and live in `EventTix.<Context>.Application/Abstractions`. Implementations live in `EventTix.<Context>.Infrastructure/Persistence/Repositories`. Neither is ever promoted to a shared project.

**Shared building blocks.** Technical primitives are extracted to `src/BuildingBlocks/` (`.Domain` for `Entity<TId>` / `AggregateRoot<TId>` / `IDomainEvent`; `.Application` for `IUnitOfWork` and pipeline behaviours; `.Persistence` for interceptors, EF conventions and `UnitOfWork<TContext>`). The admission rule is strict: **if a type's name contains a word from the ubiquitous language (Seat, Booking, Order, Tenant), it does not belong in BuildingBlocks.** This preserves the distinction between shared *infrastructure* (acceptable) and a shared *kernel* of domain concepts (a distributed-monolith anti-pattern). Once a second bounded context exists, BuildingBlocks are consumed as versioned NuGet packages rather than ProjectReferences, so a breaking change does not force lock-step redeployment of every service.

**Unit of Work.** `IUnitOfWork` remains a thin `SaveChangesAsync` contract and is generalised to `UnitOfWork<TContext>`. Its architectural significance materialises with EPIC-03: the interceptor registered on the DbContext converts `AggregateRoot.DomainEvents` into `OutboxMessages` rows within the same `SaveChanges` transaction, satisfying US-01's atomicity requirement. Repositories play no part in that flow.

**Follow-up cleanup.**
* `IBookingRepository.AddAsync` performs no asynchronous work (`DbSet.Add` is synchronous unless a HiLo value generator is configured) — change to `void Add`.
* `IsSeatReservedAsync` is a read concern currently hosted on a write port. It is retained as a deliberate write-model invariant check, documented as such, and must not become the template for general query methods on repositories — those belong to Dapper query handlers per ADR-0004.

---

## Related Decisions
* **ADR-0003** — Redis Redlock vs. SQL locking (concurrency control; motivates the partial unique index above).
* **ADR-0004** — Hybrid persistence: EF Core writes, Dapper reads (removes query variety from the write side).
* **ADR-0005** — Saga orchestration vs. choreography (saga state persistence uses the same DbContext and interceptor pipeline).
