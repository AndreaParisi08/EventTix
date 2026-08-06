# US-03: US-03-high-performance-seat-availability-read

## Business Context
Before making a reservation, thousands of frontend clients continuously poll or fetch the seat map and seat availability for an event. Object-Relational Mappers (ORMs) like EF Core add unnecessary tracking overhead and memory allocations for high-frequency read queries. Using Dapper for light, raw SQL queries guarantees maximum throughput and sub-millisecond latencies.

---

## User Story
**As a** Ticket Buyer / Frontend Client  
**I want** to fetch real-time seat availability for an event  
**So that** I can view which seats are free without causing performance bottlenecks on the backend.

---

## Acceptance Criteria (Gherkin)

### Scenario 1: High-Throughput Seat Map Read via Dapper
Given an event "EVT-100" with multiple sections and seats stored in PostgreSQL
When a client sends an HTTP GET request to "/api/v1/events/EVT-100/seats"
Then the query is executed using Dapper with un-tracked, optimized raw SQL
And the system returns HTTP 200 OK with the array of seat statuses ("AVAILABLE", "HELD", "SOLD")
And total query execution and serialization time completes under 10 milliseconds.