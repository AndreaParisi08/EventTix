namespace EventTix.Booking.Domain.ValueObjects;

public sealed record Money(decimal Amount, string Currency)
{
    public static Money EUR(decimal amount) => new(amount, "EUR");
    public static Money Zero => new(0, "EUR");
}