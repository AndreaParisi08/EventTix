# ADR-0004: Hybrid Persistence Strategy (EF Core Writes, Dapper Reads)

---

## Context and Problem Statement
The Booking Engine requires strong transactional consistency and change tracking for complex Aggregate Roots when saving orders, but also requires ultra-fast read responses when thousands of users query seat availability maps simultaneously.

---

## Decision Drivers
* Domain invariant protection and Outbox transactional safety for writes
* Zero-overhead read performance for high-frequency seat queries
* Resource-efficient memory footprint during read bursts

---

## Considered Options
1. **Option 1:** Single ORM (EF Core) for both Reads and Writes.
2. **Option 2:** Hybrid Strategy (EF Core for Writes, Dapper for Reads).

---

## Decision Outcome
Chosen Option: **Option 2**, because it combines EF Core's rich domain aggregate tracking and transactional interceptors with Dapper's bare-metal SQL read performance.

---

## Positive and Negative Consequences

### Positive
* EF Core encapsulates aggregate domain invariants and manages Outbox event persistence in atomic transactions.
* Dapper executes raw SQL straight into lightweight DTOs, bypassing EF Core tracking and change detection overhead.
* Minimizes CPU and memory consumption during seat map querying bursts.

### Negative / Trade-offs
* SQL scripts for Dapper queries must be managed manually when database schemas change.