# ADR-0001: Minimal APIs + CQRS (MediatR) vs. N-Tier Controllers

---

## Context and Problem Statement
In high-concurrency event-ticketing systems, API endpoints must process requests with sub-50ms latency while maintaining strict isolation of concerns. Traditional ASP.NET Core MVC Controllers coupled with monolithic Service classes ("God Services") lead to tight coupling, high memory overhead, and complex maintenance as the application grows.

---

## Decision Drivers
* Sub-50ms execution SLA requirement
* Single Responsibility Principle (SRP) and maintainability
* Fail-fast pipeline validation before touching handlers or database

---

## Considered Options
1. **Option 1:** Traditional N-Tier Architecture (Controller -> Service -> Repository).
2. **Option 2:** Minimal APIs + CQRS Pattern via MediatR + FluentValidation.

---

## Decision Outcome
Chosen Option: **Option 2**, because it decouples HTTP routing, validation, and domain execution into vertical slices, eliminating Controller overhead and allowing fail-fast pipeline validation.

---

## Positive and Negative Consequences

### Positive
* Each use case is fully encapsulated in its own file (Command, Handler, Validator).
* Request validation is executed automatically via MediatR `ValidationPipelineBehavior` before reaching the Handler or database.
* Bypasses MVC Controller instantiation overhead, improving memory footprint and throughput.

### Negative / Trade-offs
* Creates multiple small files per use case (mitigated by organizing code into feature slices).