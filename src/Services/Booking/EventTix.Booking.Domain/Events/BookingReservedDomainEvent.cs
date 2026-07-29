using EventTix.Booking.Domain.Common;

namespace EventTix.Booking.Domain.Events;

public sealed record BookingReservedDomainEvent(
    Guid BookingId,
    string SeatId,
    Guid UserId,
    decimal Amount,
    DateTime ReservedAt,
    DateTime ExpiresAt) : IDomainEvent;