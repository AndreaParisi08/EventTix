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

---

## Acceptance Criteria (Gherkin)

### Scenario 1: Successful Seat Hold (Happy Path)
Given seat "SEAT-42A" is not currently locked in Redis
When buyer "USR-101" submits a reservation request for "SEAT-42A"
Then the system acquires a temporary distributed lock (Redlock) on "SEAT-42A" with a 5-seconds TTL
And an order is persisted in PostgreSQL with status "PENDING_PAYMENT"
And an "OrderCreated" domain event is captured in the transactional outbox in the SAME database transaction
And the system returns HTTP 201 Created with the reservation payload and expiration timestamp in under 50ms.