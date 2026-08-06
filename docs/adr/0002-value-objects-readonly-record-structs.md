# ADR-0002: Value Objects Implemented as Readonly Record Structs

---

## Context and Problem Statement
Domain-Driven Design (DDD) uses Value Objects (e.g., `SeatId`, `UserId`, `Money`) to encapsulate domain concepts without identity. In high-concurrency bursts (5,000+ RPS), instantiating thousands of reference-type objects per second stresses the .NET Garbage Collector (GC Generation 0), introducing micro-pauses that violate our <50ms SLA.

---

## Decision Drivers
* Zero Heap Allocations under high RPS
* Garbage Collector Gen 0 pause elimination
* Automatic compile-time value equality semantics

---

## Considered Options
1. **Option 1:** Standard `class` or `record class` (Reference Types).
2. **Option 2:** `readonly record struct` (Value Types).

---

## Decision Outcome
Chosen Option: **Option 2**, because stack-allocated structs eliminate Heap memory allocations and GC Gen 0 overhead entirely during peak reservation traffic.

---

## Positive and Negative Consequences

### Positive
* Value Objects are allocated on the Stack or inline within the aggregate root, reducing GC Gen 0 pressure to zero.
* Eliminates .NET 64-bit Object Header overhead (16 bytes saved per instance).
* Generates structural value equality (`==`, `Equals`) automatically at compile time.

### Negative / Trade-offs
* Developers must avoid boxing (e.g., casting struct to `object`), which would defeat the zero-allocation purpose.