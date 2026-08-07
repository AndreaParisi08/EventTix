namespace EventTix.Booking.Application.Commands.ReserveSeat
{
    public sealed record ReserveSeatResponse(
    Guid BookingId,
    string SeatId,
    Guid UserId,
    string Status,
    DateTime ExpiresAt);
}
