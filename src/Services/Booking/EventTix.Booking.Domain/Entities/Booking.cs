using EventTix.Booking.Domain.Enums;
using EventTix.Booking.Domain.Events;
using EventTix.Booking.Domain.ValueObjects;
using EventTix.BuildingBlocks.Domain;

namespace EventTix.Booking.Domain.Entities;

public sealed class Booking : AggregateRoot<Guid>
{
    public UserId UserId { get; private set; } 
    public SeatId SeatId { get; private set; } 
    public Money Price { get; private set; } 
    public BookingStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }

    private Booking() { }

    private Booking(Guid id, SeatId seatId, UserId userId, Money price, TimeSpan holdDuration)
    {
        Id = id;
        SeatId = seatId;
        UserId = userId;
        Price = price;
        Status = BookingStatus.Pending;
        CreatedAt = DateTime.UtcNow;
        ExpiresAt = CreatedAt.Add(holdDuration);

        AddDomainEvent(new BookingReservedDomainEvent(
            Id,
            SeatId.Value,
            UserId.Value,
            Price.Amount,
            CreatedAt,
            ExpiresAt)
        );
    }

    public static Booking CreatePending(SeatId seatId, UserId userId, Money price, TimeSpan? holdWindow = null)
    {
        var duration = holdWindow ?? TimeSpan.FromMinutes(5);
        return new Booking(Guid.NewGuid(), seatId, userId, price, duration);
    }

    public void Confirm()
    {
        if (Status != BookingStatus.Pending)
            throw new InvalidOperationException($"Cannot confirm booking in state '{Status}'. Only PENDING bookings can be confirmed.");

        if (DateTime.UtcNow > ExpiresAt)
            throw new InvalidOperationException("Cannot confirm booking because the hold reservation time has expired.");

        Status = BookingStatus.Confirmed;
    }

    public void Cancel()
    {
        if (Status == BookingStatus.Confirmed)
            throw new InvalidOperationException("Cannot cancel an already CONFIRMED booking directly without a refund process.");

        Status = BookingStatus.Cancelled;
    }
}