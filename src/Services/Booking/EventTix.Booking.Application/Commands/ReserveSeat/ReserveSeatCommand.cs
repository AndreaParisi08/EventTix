using EventTix.BuildingBlocks.Application.Abstractions;

namespace EventTix.Booking.Application.Commands.ReserveSeat
{
    public sealed record ReserveSeatCommand(
    string SeatId,
    Guid UserId,
    decimal Amount,
    string IdempotencyKey) : IIdempotentCommand<ReserveSeatResponse>;
}
