using EventTix.Booking.Application.Abstractions;

namespace EventTix.Booking.Application.Bookings.Commands.ReserveSeat
{
    public sealed record ReserveSeatCommand(
    string SeatId,
    Guid UserId,
    decimal Amount,
    string IdempotencyKey) : IIdempotentCommand<ReserveSeatResponse>;
}
