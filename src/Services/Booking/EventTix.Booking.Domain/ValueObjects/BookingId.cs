namespace EventTix.Booking.Domain.ValueObjects;

public readonly record struct BookingId(Guid Value)
{
    public static BookingId New() => new(Guid.NewGuid());
    public static BookingId From(Guid value) => new(value);
    public static implicit operator Guid(BookingId id) => id.Value;
}