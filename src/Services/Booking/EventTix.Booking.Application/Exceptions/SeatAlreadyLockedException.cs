namespace EventTix.Booking.Application.Exceptions;

public sealed class SeatAlreadyLockedException : Exception
{
    public string SeatId { get; }

    public SeatAlreadyLockedException(string seatId)
        : base($"Seat '{seatId}' is currently being reserved by another user or is unavailable.")
    {
        SeatId = seatId;
    }
}