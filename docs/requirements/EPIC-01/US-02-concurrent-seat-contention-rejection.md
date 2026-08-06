# US-02: US-02-concurrent-seat-contention-rejection

## Business Context
When popular events go on sale, hundreds of requests target the exact same seat within milliseconds. The system must reject duplicate requests at the memory layer (Redis) immediately, protecting relational database connections and CPU resources from cascading failures.

---

## User Story
**As a** System Architect  
**I want** concurrent reservation requests for an already-locked seat to be rejected immediately at the Redis layer  
**So that** database connection pools remain unburdened and double-booking is strictly prevented.

---

## Acceptance Criteria (Gherkin)

### Scenario 1: Immediate Fast-Fail Response on Contention
Given seat "SEAT-42A" has been locked in Redis by buyer "USR-101"
When buyer "USR-102" submits a reservation request for "SEAT-42A" milliseconds later
Then the system FAILS to acquire the distributed lock on Redis for "USR-102"
And immediately rejects the request with HTTP 409 Conflict
And NO SQL query, EF Core DbContext, or database transaction is initiated for "USR-102"
And the database connection pool remains completely unaffected.