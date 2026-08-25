using System.Text.Json.Serialization;
using EventTix.BuildingBlocks.Domain;

namespace EventTix.Booking.Domain.Events;

public sealed record BookingReservedDomainEvent(
    Guid BookingId,
    string SeatId,
    Guid UserId,
    decimal Amount,
    DateTime ReservedAt,
    DateTime ExpiresAt) : IDomainEvent
{
    // IDomainEvent.OccurredOn defaults to "DateTime.UtcNow read live", which is wrong for an
    // immutable fact about the past: reading it twice (e.g. once when raised, once later when the
    // Outbox interceptor serializes it) could return two different instants. This event already
    // carries the real occurrence instant as ReservedAt, so pin OccurredOn to it explicitly instead
    // of falling through to the interface's default implementation.
    [JsonIgnore] // Avoid persisting a redundant duplicate of ReservedAt in the outbox payload.
    public DateTime OccurredOn => ReservedAt;
}
