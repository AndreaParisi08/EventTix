using MediatR;

namespace EventTix.Booking.Application.Bookings.Commands.ReserveSeat
{
    public sealed record ReserveSeatCommand(
    string SeatId,
    Guid UserId,
    decimal Amount) : IRequest<ReserveSeatResponse>;
}
