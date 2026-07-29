# Sequence Diagram: High-Concurrency Seat Contention (US-01 Scenario 2)

This diagram illustrates how EventTix handles race conditions when two concurrent clients ("User A" and "User B") attempt to reserve the exact same seat (`SEAT-42A`) simultaneously.

<div align="center" style="background-color: #ffffff; padding: 20px; border-radius: 8px; color: #000000;">

```mermaid
%%{init: {'theme': 'neutral'}}%%
sequenceDiagram
    autonumber
    actor UserA as Buyer A
    actor UserB as Buyer B
    participant API as Booking Service API
    participant Redis as Redis (Redlock)
    participant DB as PostgreSQL DB

    Note over UserA, UserB: Concurrent requests for Seat-42A at exact same millisecond

    UserA->>API: POST /api/v1/bookings (Seat-42A)
    UserB->>API: POST /api/v1/bookings (Seat-42A)

    Note over API, Redis: 1. Race Condition Interception (In-Memory)
    API->>Redis: SET lock:seat:SEAT-42A (User A)
    Redis-->>API: OK (Lock Acquired)
    
    API->>Redis: SET lock:seat:SEAT-42A (User B)
    Redis-->>API: NIL (Lock Failed)

    Note over API, UserB: 2. Fast-Fail Execution (User B)
    API-->>UserB: 409 Conflict (Seat already reserved)

    Note over API, DB: 3. Atomic Transaction Write (User A)
    API->>DB: BEGIN TRANSACTION
    API->>DB: INSERT Order (Seat-42A, PENDING)
    API->>DB: INSERT OutboxMessage (OrderCreated)
    API->>DB: COMMIT TRANSACTION
    DB-->>API: Success
    API-->>UserA: 201 Created (Order Reserved)
```

</div>