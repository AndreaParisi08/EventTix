namespace EventTix.Booking.Domain.ValueObjects;

public readonly record struct Money(decimal Amount, string Currency)
{
    public static Money EUR(decimal amount) => new(amount, "EUR");
    public static Money Zero => new(0, "EUR");
}