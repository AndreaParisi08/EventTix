namespace EventTix.Booking.Domain.Common;

/// <summary>
/// Represents a business-significant event that occurred within the domain.
/// Domain Events capture state changes and are dispatched to trigger asynchronous side-effects.
/// </summary>
public interface IDomainEvent
{
    DateTime OccurredOn => DateTime.UtcNow;
}