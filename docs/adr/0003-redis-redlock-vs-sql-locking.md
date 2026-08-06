# ADR-0003: Distributed Locking via Redis (Redlock) vs. Database Locking

---

## Context and Problem Statement
During high-demand ticket launches ("Click Days"), thousands of users attempt to reserve the exact same seat simultaneously. Relying solely on relational database locks (pessimistic `FOR UPDATE` or optimistic concurrency tokens) results in SQL connection pool exhaustion, table lock escalation, and severe latency degradation over 50ms.

---

## Decision Drivers
* Sub-millisecond lock resolution in high-concurrency bursts
* Database connection pool preservation
* Scale-out safety across stateless microservice instances

---

## Considered Options
1. **Option 1:** Database Pessimistic Locking (`SELECT FOR UPDATE` on PostgreSQL).
2. **Option 2:** Database Optimistic Concurrency (Version/ETag tracking with retries).
3. **Option 3:** In-Memory Distributed Lock (Redis Redlock).

---

## Decision Outcome
Chosen Option: **Option 3**, because settling contention in RAM rejects 99.9% of excess requests instantly with `409 Conflict` before touching the database connection pool.

---

## Positive and Negative Consequences

### Positive
* Concurrency contention is settled in RAM before touching PostgreSQL, returning sub-millisecond rejections.
* Redis single-threaded execution guarantees atomic seat acquisition.
* Automatic 5-minute TTL prevents permanent deadlocks if nodes crash.

### Negative / Trade-offs
* Introduces a hard dependency on Redis availability (mitigated via Redis Cluster / Quorum setup).