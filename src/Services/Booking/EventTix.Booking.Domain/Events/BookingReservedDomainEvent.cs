using EventTix.BuildingBlocks.Domain;

namespace EventTix.Booking.Domain.Events;

public sealed record BookingReservedDomainEvent(
    Guid BookingId,
    string SeatId,
    Guid UserId,
    decimal Amount,
    DateTime ReservedAt,
    DateTime ExpiresAt) : IDomainEvent;