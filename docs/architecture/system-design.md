# EventTix — Architecture & System Design

This document provides a comprehensive overview of the architectural principles, patterns, and design decisions governing the **EventTix** platform.

---

## 1. Architectural Style: Clean Architecture & DDD

EventTix is built using **Domain-Driven Design (DDD)** principles and **Clean Architecture** (Ports & Adapters). 
Each microservice is strictly isolated into four distinct layers:

```mermaid
%%{init: {'theme': 'neutral'}}%%
flowchart TD
    subgraph Outer ["Outer Layers (Infrastructure & Entry Points)"]
        API["<b>EventTix.Booking.API</b><br/><i>HTTP, Minimal APIs, DTOs, Middleware</i>"]
        INF["<b>EventTix.Booking.Infrastructure</b><br/><i>EF Core, StackExchange.Redis, MassTransit</i>"]
    end

    subgraph Middle ["Use Case Layer"]
        APP["<b>EventTix.Booking.Application</b><br/><i>CQRS (MediatR), Commands/Queries, FluentValidation</i>"]
    end

    subgraph Core ["Core Domain (Zero External Dependencies)"]
        DOM["<b>EventTix.Booking.Domain</b><br/><i>Entities, Aggregates, Value Objects, Domain Events</i>"]
    end

    API -->|Triggers Use Cases| APP
    API -->|Configures DI| INF
    INF -->|Implements Interfaces| APP
    INF -->|Persists Aggregates| DOM
    APP -->|Executes Rules| DOM

    style DOM fill:#e3f2fd,stroke:#1565c0,stroke-width:2px
    style APP fill:#f3e5f5,stroke:#7b1fa2,stroke-width:1.5px
```

### Layer Responsibilities
* **Domain:** Contains pure enterprise business logic and domain models. Zero external dependencies.
* **Application:** Orchestrates use-cases using CQRS (Command Query Responsibility Segregation). Defines abstractions and interfaces.
* **Infrastructure:** Implements database connections, distributed locking, messaging, and third-party API clients.
* **API (Composition Root):** Configures Dependency Injection, HTTP routes, and API security.

---

## 2. Bounded Contexts

The domain is partitioned into 4 distinct Bounded Contexts:

| Bounded Context | Core Responsibility | Primary Storage |
|---|---|---|
| **Booking Context** | High-concurrency seat reservation and order creation | PostgreSQL + Redis (Redlock) |
| **Catalog Context** | Event, venue, and seat map topology management | PostgreSQL |
| **Outbox Context** | Reliable asynchronous domain event publishing | PostgreSQL (`OutboxMessages` table) |
| **Webhook / Saga Context** | Payment orchestration and organizer notification callbacks | RabbitMQ + State Machine |

---

## 3. Key Architectural Patterns & Decisions

1. **Distributed Concurrency Control (Redlock):** 
   Requests targeting the same seat resource are intercepted at the memory layer via Redis Redlock before reaching database connections.
   * See [ADR-0002: Redis Redlock vs SQL Locking](../adr/0002-redis-redlock-vs-sql-locking.md).
   * See [Sequence Diagram: Seat Contention](./diagrams/sequence-race-condition.md).

2. **Distributed Transaction Management (Saga Orchestration):** 
   Distributed operations across Booking and Payment Gateway are managed by a centralized MassTransit State Machine.
   * See [ADR-0001: Saga Orchestration vs Choreography](../adr/0001-saga-orchestration-vs-choreography.md).

3. **Transactional Outbox Pattern:** 
   Guarantees *At-Least-Once* delivery of Domain Events to RabbitMQ without requiring distributed 2PC transactions.

---

## 4. Architectural Decision Records (ADR Index)

* [ADR-0000: ADR Template](../adr/0000-template.md)
* [ADR-0001: Saga Orchestration vs. Choreography](../adr/0001-saga-orchestration-vs-choreography.md)
* [ADR-0002: Distributed Locking Strategy (Redis Redlock)](../adr/0002-redis-redlock-vs-sql-locking.md)