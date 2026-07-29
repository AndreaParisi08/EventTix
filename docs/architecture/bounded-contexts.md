# EventTix — Bounded Contexts & Ubiquitous Language

This document defines the core boundaries of the EventTix platform using Domain-Driven Design (DDD) principles.

---

## 1. Bounded Context Overview

```mermaid
%%{init: {'theme': 'neutral'}}%%
flowchart TB
    subgraph Supporting["Supporting Domain"]
        CC["Catalog Context<br/>(Venues, Events & Seat Layouts)"]
    end

    subgraph Core["Core Domain"]
        BC["Booking Context<br/>(High-Concurrency & Orders)"]
    end

    subgraph Generic["Generic Subdomain"]
        OB["Outbox Context<br/>(Reliable Event Dispatcher)"]
    end

    subgraph Integration["Integration & Orchestration"]
        Saga["Payment & Webhook Saga<br/>(MassTransit State Machine)"]
    end

    subgraph External["External Systems"]
        Gateway["Payment Gateway (Mock)"]
        Organizer["Organizer Webhook Endpoints"]
    end

    %% Interactions
    CC -- "gRPC / Cache Sync (Read Seat Metadata)" --> BC
    BC -- "Transactional Write" --> OB
    OB -- "Asynchronous Events (RabbitMQ)" --> Saga
    Saga -- "HTTP / REST" --> Gateway
    Saga -- "HTTP Webhook" --> Organizer

    classDef coreStyle fill:#e1f5fe,stroke:#0288d1,stroke-width:2px;
    classDef sagaStyle fill:#fff3e0,stroke:#f57c00,stroke-width:2px;
    class BC coreStyle;
    class Saga sagaStyle;
 ```

---

## 2. Context Responsibilities

### A. Booking Context (Core Domain)
* **Responsibility:** High-concurrency seat reservation, handling contention, generating temporary reservation holds, and persisting orders.
* **Storage:** PostgreSQL (`Orders`, `Bookings`) + Redis (`Redlock` key-value pairs).
* **Key Aggregates:** `Booking`, `Order`.

### B. Catalog Context (Supporting Domain)
* **Responsibility:** Managing venues, seat layouts, event metadata, and base ticket prices.
* **Storage:** PostgreSQL (`Events`, `Venues`, `Seats`).
* **Key Aggregates:** `Event`, `Venue`.

### C. Outbox Context (Generic Subdomain)
* **Responsibility:** Guaranteeing *At-Least-Once* delivery of domain events from PostgreSQL to RabbitMQ without distributed 2PC transactions.
* **Storage:** PostgreSQL (`OutboxMessages` table).

### D. Webhook & Payment Saga Context (Integration Domain)
* **Responsibility:** Orchestrating payment authorization with external gateways and dispatching HTTP webhooks to organizers upon order completion.
* **Infrastructure:** MassTransit State Machine persisted in PostgreSQL + RabbitMQ queues.

---

## 3. Ubiquitous Language (Domain Terms)

| Term | Context | Definition |
|---|---|---|
| **Seat** | Catalog | Physical seat definition (Section, Row, Number) and base pricing tier. |
| **Seat** | Booking | A contention target identified by `SeatId` subject to ephemeral memory locks. |
| **Lock** | Booking | A temporary 5-minute Redis key preventing concurrent double-booking of a seat. |
| **Reservation** | Booking | An unconfirmed order in `PENDING` state awaiting payment processing. |
| **Saga** | Webhook/Payment | An orchestrated workflow managing asynchronous state transitions across services. |