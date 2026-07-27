# US-01: High-Concurrency Seat Reservation & Event Emission

## Business Context
During high-demand ticket drops, thousands of users attempt to reserve the same limited set of seats simultaneously. The system must prevent double-booking at all costs without exhausting database connection pools or causing cascading failures across services.

---

## User Story
**As a** Ticket Buyer  
**I want to** select and temporarily hold a specific seat for an event  
**So that** I can complete the payment process securely without another buyer acquiring the exact same seat.

---

## Acceptance Criteria (Gherkin)

### Scenario 1: Successful Seat Holding (Happy Path)
  Given seat "SEAT-42A" for event "EVT-100" is currently AVAILABLE
  When buyer "USR-101" submits a reservation request for "SEAT-42A"
  Then the system acquires a temporary distributed lock on "SEAT-42A" with a 5-minute TTL
  And an order is created in "PENDING_PAYMENT" status within the database
  And an "OrderCreated" event is written to the Outbox table in the SAME database transaction
  And the system returns HTTP 201 Created with the reservation details and expiration timestamp.

### Scenario 2: Concurrent Seat Contention (Race Condition Prevention)
  Given seat "SEAT-42A" has just been locked by buyer "USR-101"
  When buyer "USR-102" submits a reservation request for "SEAT-42A" milliseconds later
  Then the system FAILS to acquire the distributed lock for buyer "USR-102"
  And immediately rejects the request with HTTP 409 Conflict
  And NO database write operation or transaction is executed for "USR-102"
  And the database connection pool remains unaffected by the rejected request.

### Scenario 3: Guaranteed Event Dispatching (Transactional Outbox)
  Given an order for "SEAT-42A" was successfully written to the database with its Outbox record
  When the Outbox Publisher process executes
  Then the "OrderCreated" event is read from the Outbox table and published to RabbitMQ
  And the Outbox record is marked as "PROCESSED" within a local transaction
  And the event message is guaranteed to be delivered at least once to downstream consumers.

### Scenario 4: Rate-Limited Webhook Notification to Event Tenant
  Given the "OrderCreated" event is published to RabbitMQ for tenant "TENANT-500"
  When the Webhook Delivery Service consumes the event
  Then it checks the tenant's current rate limit bucket via Redis
  And if capacity is available, delivers an HTTP POST request with an HMAC-SHA256 signature to the tenant's endpoint
  And logs the delivery attempt timestamp, HTTP response status code, and latency.