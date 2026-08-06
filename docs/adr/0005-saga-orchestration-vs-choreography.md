# ADR-0001: Saga Orchestration vs. Choreography for Booking Workflow

---

## Context and Problem Statement
In EventTix, reserving a seat and processing its payment spans multiple distributed boundaries (Booking Service, Payment Gateway Mock, Catalog Service). 
Using native Distributed Two-Phase Commits (2PC) is impossible due to network latency, tight coupling, and database availability constraints (CAP Theorem). 
We must guarantee eventual consistency and automated compensations (e.g., releasing a seat if payment fails).

---

## Decision Drivers
* Need for clear end-to-end observability of an order's lifecycle (`Pending`, `PaymentProcessing`, `Confirmed`, `Failed`).
* Ease of debugging distributed transactions in production.
* Explicit compensation logic execution (rollback triggers).
* Low mental overhead for developers inspecting system state.

---

## Considered Options
1. **Two-Phase Commit (2PC / XA Transactions):** Synchronous distributed blocking locks.
2. **Saga Choreography:** Pure event-driven workflow where services react to events without a central coordinator.
3. **Saga Orchestration:** Centralized State Machine coordinating messages and state transitions explicitly.

---

## Decision Outcome
Chosen Option: **Option 3 (Saga Orchestration)** using **MassTransit State Machine**, because centralizing order states within an explicit State Machine provides superior observability, deterministic state transitions, and straightforward debugging compared to event choreography.

---

## Positive and Negative Consequences

### Positive
* **Explicit State Visibility:** Order state is queryable and clearly modeled in PostgreSQL via EF Core state persistence.
* **Centralized Compensation:** Rollback workflows (e.g., releasing Redis locks upon payment rejection) are defined in one place.
* **Reduced Event Spaghetti:** Avoids complex circular event dependencies between independent services.

### Negative / Trade-offs
* **Centralized Dependency:** The orchestrator becomes a critical component.
* *Mitigation:* Mitigated by running redundant MassTransit worker instances on Kubernetes with state persisted in PostgreSQL.