# Sequence Diagram: High-Concurrency Seat Contention (US-01 Scenario 2)

This diagram illustrates how EventTix handles race conditions when two concurrent clients ("User A" and "User B") attempt to reserve the exact same seat (`SEAT-42A`) simultaneously.

```mermaid
sequenceDiagram
    autonumber
    actor UserA as Buyer A
    actor UserB as Buyer B
    participant API as Booking Service API
    participant Redis as Redis (Redlock)
    participant DB as PostgreSQL DB
    participant Outbox as Outbox Table

    Note over UserA, UserB: Both users click "Reserve Seat-42A" at the exact same millisecond

    par Concurrent Request Execution
        UserA->>API: POST /api/v1/bookings { seatId: "SEAT-42A" }
    and
        UserB->>API: POST /api/v1/bookings { seatId: "SEAT-42A" }
    end

    rect rgb(220, 255, 220)
        Note over API, Redis: Race Condition Interception Layer
        API->>Redis: SET lock:seat:SEAT-42A NX PX 300000 (User A)
        Redis-->>API: OK (Lock Acquired for User A)
        
        API->>Redis: SET lock:seat:SEAT-42A NX PX 300000 (User B)
        Redis-->>API: NIL (Lock Failed for User B)
    end

    rect rgb(255, 220, 220)
        Note over API, UserB: Fast-Fail Execution (User B)
        API-->>UserB: HTTP 409 Conflict { error: "Seat already reserved" }
        Note over UserB, DB: Zero SQL queries or DB connections opened for User B!
    end

    rect rgb(220, 240, 255)
        Note over API, DB: Transactional Write (User A)
        API->>DB: BEGIN TRANSACTION
        API->>DB: INSERT INTO Orders (Id, SeatId, Status) VALUES (..., 'SEAT-42A', 'PENDING')
        API->>DB: INSERT INTO OutboxMessages (EventPayload, Status) VALUES ('OrderCreated', 'PENDING')
        API->>DB: COMMIT TRANSACTION
        DB-->>API: Success
        API-->>UserA: HTTP 201 Created { orderId: "...", expiresAt: "..." }
    end