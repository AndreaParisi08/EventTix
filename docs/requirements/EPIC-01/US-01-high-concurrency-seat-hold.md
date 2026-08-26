# US-01: US-01-high-concurrency-seat-hold

## Business Context
During high-demand ticket drops, thousands of users attempt to reserve the same limited set of seats simultaneously. The Booking Service must validate seat availability and acquire a temporary distributed lock to hold the seat, persisting the initial order state without risking database connection pool exhaustion.

---

## User Story
**As a** Ticket Buyer  
**I want to** select and hold a specific available seat for an event  
**So that** I can secure my reservation for 5 minutes and prepare for payment.

---

## Scope Note
Seat/event existence and state (`ACTIVE`) validation against the Catalog Context is explicitly **out of scope** for this story. The Booking Service treats `SeatId`/`EventId` as trusted, opaque identifiers until that integration exists. This validation is tracked under **EPIC-02**, specifically `US-05-low-latency-catalog-grpc-validation` (the Booking → Catalog gRPC call). Until US-05 is implemented, a reservation request for a non-existent seat or event is **not** rejected by this flow — this is a known, accepted gap for the current increment, not an oversight.

The distributed lock used to satisfy this story is a **single-node Redis lock**, not a multi-node Redlock quorum — see ADR-0003 for the full trade-off analysis and the accepted risk. This was a deliberate scope decision (2026-08-26), not an oversight.

The "< 50ms" acceptance criterion in Scenario 1 below is verified by a targeted latency measurement (`ReserveSeatLatencyBenchmark`, in `EventTix.Booking.IntegrationTests`, tagged `Category=Benchmark`) that reports p50/p95 for the happy path against real Postgres + Redis. Building the full load-testing harness implied by the AC's original framing — sustained concurrent load, k6 scripts, threshold-based CI gating, report artifacts — is explicitly **out of scope here** and tracked under **EPIC-06 / US-15** (`k6-load-testing-and-benchmarkdotnet-artifacts`). This story's AC is satisfied by a single-request latency measurement, not a load test.

---

## Acceptance Criteria (Gherkin)

### Scenario 1: Successful Seat Hold (Happy Path)
Given seat "SEAT-42A" is not currently locked in Redis
When buyer "USR-101" submits a reservation request for "SEAT-42A"
Then the system acquires a temporary distributed lock (single-node Redis lock — see ADR-0003) on "SEAT-42A" with a 5-seconds TTL
And an order is persisted in PostgreSQL with status "PENDING_PAYMENT"
And an "OrderCreated" domain event is captured in the transactional outbox in the SAME database transaction
And the system returns HTTP 201 Created with the reservation payload and expiration timestamp in under 50ms.