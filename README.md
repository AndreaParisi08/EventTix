# EventTix — High-Concurrency Booking Engine & Multi-Tenant Webhook Infrastructure

> **A production-ready, event-driven .NET architecture designed to eliminate high-concurrency seat reservation race conditions and deliver resilient, multi-tenant webhooks with transactional guarantees.**

[![.NET 9.0](https://img.shields.io/badge/.NET-9.0-512BD4?style=for-the-badge&logo=dotnet)](https://dotnet.microsoft.com/)
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
    
``` 

## 💻 Tech Stack

* **Framework:** .NET 9.0 (C#)
* **Data Access:** Entity Framework Core 9, Dapper, PostgreSQL 16
* **Messaging & Async:** RabbitMQ 3.12, MassTransit 8.x
* **Caching & Locking:** Redis 7.0 (StackExchange.Redis, Redlock.net)
* **Resilience & Security:** Polly (Exponential Backoff, Circuit Breaker), HMAC-SHA256 Signing
* **Testing:** xUnit, FluentAssertions, Testcontainers, k6 (Load Testing)

---

## 🚀 Quick Start (Local Setup)

### Prerequisites
* [.NET 9.0 SDK](https://dotnet.microsoft.com/download)
* [Docker Desktop](https://www.docker.com/products/docker-desktop/)

### 1. Clone & Start Infrastructure
```bash
git clone [https://github.com/your-username/EventTix.git](https://github.com/your-username/EventTix.git)
cd EventTix
```

# Start PostgreSQL, Redis, RabbitMQ, and MockServer
docker compose up -d

### 2. Verify Services
Once running, you can access the local infrastructure management dashboards:
* **RabbitMQ Management:** `http://localhost:15672` (Guest / Guest)
* **Mock Webhook Receiver:** `http://localhost:1080`

### 3. Run the Solution
```bash
dotnet run --project src/Services/EventTix.Booking.API
```

## 📑 Architecture Decision Records (ADRs)

Key architectural choices are documented in detail within the [`/docs/adr`](./docs/adr) directory:

* [**ADR-001:** Saga Orchestration vs. Choreography for Booking Workflow](./docs/adr/0001-saga-orchestration-vs-choreography.md)
* [**ADR-002:** Distributed Locking via Redis (Redlock) vs. Database Pessimistic Locking](./docs/adr/0002-redis-redlock-vs-sql-locking.md) *(Coming Next)*