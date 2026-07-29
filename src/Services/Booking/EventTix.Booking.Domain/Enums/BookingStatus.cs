namespace EventTix.Booking.Domain.Enums;

public enum BookingStatus
{
    Pending = 1,     // Position blocked waiting for payment (TTL 5 minutes)
    Confirmed = 2,   // Payment authorized by the Saga
    Cancelled = 3,   // Manually cancelled or rejected by the payment gateway
    Expired = 4      // Payment timeout expired (frees the seat)
}