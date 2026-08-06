# EventTix — High-Concurrency Booking Engine & Multi-Tenant Webhook Infrastructure

> **A production-ready, event-driven .NET architecture designed to eliminate high-concurrency seat reservation race conditions and deliver resilient, multi-tenant webhooks with transactional guarantees.**

[![.NET 10.0](https://img.shields.io/badge/.NET-10.0-512BD4?style=for-the-badge&logo=dotnet)](https://dotnet.microsoft.com/)
[![Architecture](https://img.shields.io/badge/Architecture-Event--Driven%20%7C%20Saga%20%7C%20Outbox-blue?style=for-the-badge)](#system-architecture)
[![Docker](https://img.shields.io/badge/Docker-Compose%20Ready-2496ED?style=for-the-badge&logo=docker)](./docker-compose.yml)
[![License](https://img.shields.io/badge/License-MIT-green?style=for-the-badge)](#license)

---

## Executive Summary

During high-demand event launches ("ticket drops"), systems face extreme traffic spikes where thousands of users compete for the exact same resource simultaneously. Naive implementations result in double-booking, database connection pool exhaustion, or inconsistent data across services.

**EventTix** solves these production challenges using a zero-trust, event-driven microservices architecture:
1. **Sub-millisecond Race Condition Defense:** Uses Redis-based distributed locks (**Redlock**) to reject duplicate seat requests *before* touching the relational database.
2. **Guaranteed Transactional Consistency:** Implements the **Transactional Outbox Pattern** via EF Core Interceptors to guarantee event delivery without 2PC (Two-Phase Commit).
3. **Resilient Distributed Workflows:** Coordinates order execution, payment verification, and automatic compensation via **MassTransit Saga Orchestration**.
4. **Reliable Multi-Tenant Webhook Delivery:** Features a dedicated delivery engine with **Polly** retry policies, **HMAC-SHA256** signatures, and per-tenant **Redis Token Bucket Rate Limiting** executed via atomic Lua scripts.

---

## System Architecture

``` mermaid
flowchart TD
    Client([Client / Web App])
    
    subgraph Booking API Layer
        API[Minimal API Endpoint]
        MediatR[MediatR Handlers]
        Redlock[(Redis Redlock)]
    end
    
    subgraph Catalog Service
        gRPCServer[gRPC Catalog Service]
        CatalogDB[(PostgreSQL Catalog)]
    end
    
    subgraph Booking Persistence
        EFCore[EF Core DbContext]
        OutboxInterceptor[Outbox Interceptor]
        PostgreSQL[(PostgreSQL Booking DB)]
    end
    
    subgraph Asynchronous Processing
        OutboxWorker[Outbox Background Worker]
        RabbitMQ{{RabbitMQ Broker}}
        Saga[MassTransit Payment Saga]
    end
    
    subgraph Multi-Tenant Webhook Engine
        WebhookConsumer[Webhook Consumer]
        LuaRateLimiter[(Redis Lua Rate Limiter)]
        TenantServer([Tenant Webhook Endpoint])
    end

    %% Flow connections
    Client -->|1. POST /api/v1/bookings| API
    API --> MediatR
    MediatR -->|2. Fast Check| gRPCServer
    gRPCServer --> CatalogDB
    MediatR -->|3. Acquire Lock 5min TTL| Redlock
    MediatR -->|4. Atomic Save| EFCore
    EFCore --> OutboxInterceptor
    OutboxInterceptor -->|5. Order + Outbox Event| PostgreSQL
    
    OutboxWorker -->|6. Poll Outbox| PostgreSQL
    OutboxWorker -->|7. Publish Event| RabbitMQ
    RabbitMQ --> Saga
    RabbitMQ --> WebhookConsumer
    
    WebhookConsumer -->|8. Token Bucket Check| LuaRateLimiter
    WebhookConsumer -->|9. HMAC Signed HTTP POST| TenantServer
``` 

## 💻 Tech Stack

* **Framework:** .NET 10.0 (C#)
* **Data Access:** Entity Framework Core 10, Dapper, PostgreSQL 16
* **Inter-Service Communication:** gRPC / Protobuf, REST (Minimal APIs)
* **Messaging & Async:** RabbitMQ 3.12, MassTransit 8.x
* **Caching & Locking:** Redis 7.0 (StackExchange.Redis, Redlock.net)
* **Resilience & Security:** Polly (Exponential Backoff, Circuit Breaker), HMAC-SHA256 Signing
* **Observability & Testing:** OpenTelemetry, xUnit, FluentAssertions, Testcontainers, k6 (Load Testing)

---

## 🚀 Quick Start (Local Setup)

### Prerequisites
* [.NET 10.0 SDK](https://dotnet.microsoft.com/download)
* [Docker Desktop](https://www.docker.com/products/docker-desktop/)

### 1. Clone & Start Infrastructure
```bash
git clone [https://github.com/your-username/EventTix.git](https://github.com/your-username/EventTix.git)
cd EventTix

# Start PostgreSQL, Redis, RabbitMQ, and MockServer
docker compose up -d
```
> **Note on Migrations:** Pending EF Core database migrations are automatically applied on application startup in Development mode.  
> To apply them manually prior to launch, run:  
> `dotnet ef database update -p src/Services/Booking/EventTix.Booking.Infrastructure -s src/Services/Booking/EventTix.Booking.Api`

### 2. Run the Solution
```bash
dotnet run --project src/Services/Booking/EventTix.Booking.Api
```

### 3. Verify Services & Interactive Dashboards
Once the solution is running, you can access the API endpoints and infrastructure management tools:

* **Swagger UI:** `http://localhost:5243/swagger`
* **Health Checks:** `http://localhost:5243/health/read`
* **RabbitMQ Management:** `http://localhost:15672` (`eventtix` / `eventtix_dev_password`)
* **Mock Webhook Receiver / Payment Gateway:** `http://localhost:1080`

#### PostgreSQL Inspection (Optional)
To manually verify tables and schema via CLI:
```bash
docker exec -it eventtix-postgres psql -U eventtix -d eventtix_db
\dt
```

---

## 🧪 Testing Strategy

* **Manual E2E Verification:** Test real-world booking flows (PostgreSQL + Redis lock acquisition) by invoking Minimal API endpoints directly via Swagger UI or cURL.
* **Automated Unit & Integration Testing:** Execute test suites covering domain logic isolation and database/infrastructure interactions:
  ```bash
  dotnet test
  ```

---

## 📑 Architecture Decision Records (ADRs)

Key architectural choices are documented in detail within the [`/docs/adr`](./docs/adr) directory:

* [**ADR-0001:** Minimal APIs + CQRS (MediatR) vs. N-Tier Controllers](./docs/adr/0001-cqrs-minimal-apis-vs-ntier-controllers.md)
* [**ADR-0002:** Value Objects Implemented as Readonly Record Structs](./docs/adr/0002-value-objects-readonly-record-structs.md)
* [**ADR-0003:** Distributed Locking via Redis (Redlock) vs. Database Locking](./docs/adr/0003-redis-redlock-vs-sql-locking.md)
* [**ADR-0004:** Hybrid Persistence Strategy (EF Core Writes, Dapper Reads)](./docs/adr/0004-hybrid-orm-efcore-writes-dapper-reads.md)
---

## 🗺️ Epics & System Capabilities

* **[EPIC-01] Booking Engine & High Concurrency:** Sub-50ms seat hold, Redis Redlock, Dapper read-models.
* **[EPIC-02] Catalog Service & gRPC:** Low-latency venue topology & seat status validation via gRPC.
* **[EPIC-03] Transactional Outbox & Reliable Messaging:** Zero-data-loss event dispatching with Redis Leader Election.
* **[EPIC-04] Payment & MassTransit Saga Orchestration:** Distributed state machine with 5-min timeout compensation workflows.
* **[EPIC-05] Multi-Tenant Webhook Delivery Engine:** HMAC-SHA256 signed delivery, Polly retries & per-tenant Redis Lua rate limiting.
* **[EPIC-06] Infrastructure & Observability:** Distributed OpenTelemetry tracing & k6 load testing suite.

---

## 🎯 Project Management & Backlog

The complete project lifecycle, user story decomposition, and architectural tasks are managed on Notion:  
👉 **[View Public EventTix Notion Workspace](https://your-notion-link-here)**
