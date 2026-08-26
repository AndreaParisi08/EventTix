# ADR-0003: Distributed Locking via Redis — Single-Node Lock vs. Database Locking

---

## Context and Problem Statement
During high-demand ticket launches ("Click Days"), thousands of users attempt to reserve the exact same seat simultaneously. Relying solely on relational database locks (pessimistic `FOR UPDATE` or optimistic concurrency tokens) results in SQL connection pool exhaustion, table lock escalation, and severe latency degradation over 50ms.

---

## Decision Drivers
* Sub-millisecond lock resolution in high-concurrency bursts
* Database connection pool preservation
* Scale-out safety across stateless microservice instances
* **This is a portfolio project covering many technologies (RabbitMQ, gRPC, Kubernetes, OAuth, saga orchestration, …) under a finite amount of time — implementation effort must be spent where it best demonstrates breadth and sound judgment, not on gold-plating a single component past what the story actually requires.**

---

## Considered Options
1. **Option 1:** Database Pessimistic Locking (`SELECT FOR UPDATE` on PostgreSQL).
2. **Option 2:** Database Optimistic Concurrency (Version/ETag tracking with retries).
3. **Option 3a:** True Redlock — quorum-based locking across N (typically 5) independent Redis master nodes, per Salvatore Sanfilippo's original algorithm.
4. **Option 3b:** Single-node Redis distributed lock — one Redis instance, atomic `SET NX PX` semantics (via `StackExchange.Redis` `LockTakeAsync`/`LockReleaseAsync`), no cross-node quorum.

---

## Decision Outcome
Chosen Option: **3b — single-node Redis distributed lock**, not the full Redlock quorum algorithm (3a).

Both 3a and 3b settle contention in RAM and reject excess requests before touching the database connection pool, which is the property that actually matters for this story's acceptance criteria. What 3a adds on top is resilience to a *single Redis node failing while holding locks* — at the cost of standing up and operating N independent Redis instances, plus quorum-acquisition logic (acquire on a majority of nodes within a tight clock-drift budget, handle partial failure, etc.).

For this codebase, that additional resilience is explicitly **not worth its implementation and operational cost**: correctness under normal operation (only one of many concurrent requests for the same seat wins) is fully demonstrated by 3b — see the automated concurrency test (`ReserveSeatConcurrencyTests`, `EventTix.Booking.IntegrationTests`), which proves exactly this property against a real Redis instance via Testcontainers. What 3b does *not* demonstrate is fault-tolerance if that one Redis instance disappears mid-lock. That gap is accepted knowingly (see Negative Consequences) rather than closed, because closing it would consume time better spent covering the other bounded contexts and patterns this portfolio still needs to demonstrate (saga orchestration, outbox publishing, gRPC, rate limiting, tracing).

**Note on naming:** earlier drafts of this ADR and of `docs/architecture/system-design.md` referred to the implementation as "Redlock." That was inaccurate — Redlock specifically names the multi-node quorum algorithm (Option 3a), which this project does not implement. The correct name for what's built is a *single-node Redis distributed lock*; documentation has been corrected accordingly.

---

## Positive and Negative Consequences

### Positive
* Concurrency contention is settled in RAM before touching PostgreSQL, returning fast rejections (`409 Conflict` via `SeatAlreadyLockedException`) instead of a DB round-trip.
* Redis single-threaded execution guarantees atomic seat acquisition — no race window between the `EXISTS` check and the `SET`.
* A 5-second TTL (matching US-01's acceptance criteria) prevents a permanently held lock if the process holding it crashes before releasing it.
* Zero operational overhead beyond the one Redis instance already required for caching/idempotency — no additional nodes to provision, monitor, or pay for.

### Negative / Trade-offs
* **Single point of failure, accepted as known tech debt.** If the single Redis instance becomes unavailable or is restarted mid-lock, in-flight locks are lost — a small window opens where a seat could theoretically be double-booked by two requests that both believe they hold the lock. Mitigated in practice by: (a) the final consistency check inside the same transaction as the booking insert, and (b) this being a portfolio/demo system, not a production ticketing platform processing real payments at scale.
* Not resilient to Redis node failure the way true Redlock (Option 3a) would be — this is the deliberate scope cut described above, not an oversight.
* Introduces a hard dependency on Redis availability for the reservation path in general (shared with Option 3a; inherent to any Redis-based locking approach).

---

## Upgrade Path (if this ever needed to become production-grade)
Documented for completeness, not planned work: moving from 3b to true Redlock (3a) would mean (1) provisioning N≥5 independent Redis master instances (not replicas of each other — independence is what makes quorum meaningful), (2) acquiring the lock against a majority with a bounded total acquisition time relative to the TTL, and (3) using a library that implements the full algorithm (e.g. `RedLock.net`) instead of a single `LockTakeAsync` call. `IDistributedLockService` in `EventTix.BuildingBlocks` already isolates the rest of the codebase behind an interface, so this would be a swap of `RedisDistributedLockService`'s internals, not a change to any caller.
