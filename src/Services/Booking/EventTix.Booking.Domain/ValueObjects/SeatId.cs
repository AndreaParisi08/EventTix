namespace EventTix.Booking.Domain.ValueObjects;

public readonly record struct SeatId
{
    public string Value { get; }

    public SeatId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(value));
        Value = value.ToUpperInvariant();
    }

    public static SeatId From(string value) => new(value);
    public override string ToString() => Value;
}