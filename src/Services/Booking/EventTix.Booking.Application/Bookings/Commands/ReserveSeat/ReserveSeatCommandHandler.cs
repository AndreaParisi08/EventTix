using EventTix.Booking.Application.Abstractions;
using EventTix.Booking.Application.Exceptions;
using EventTix.Booking.Domain.ValueObjects;
using MediatR;

namespace EventTix.Booking.Application.Bookings.Commands.ReserveSeat;

public sealed class ReserveSeatCommandHandler : IRequestHandler<ReserveSeatCommand, ReserveSeatResponse>
{
    private readonly IDistributedLockService _lockService;
    private readonly IBookingRepository _bookingRepository;

    public ReserveSeatCommandHandler(
        IDistributedLockService lockService,
        IBookingRepository bookingRepository)
    {
        _lockService = lockService;
        _bookingRepository = bookingRepository;
    }

    public async Task<ReserveSeatResponse> Handle(ReserveSeatCommand request, CancellationToken cancellationToken)
    {
        var seatId = SeatId.From(request.SeatId);
        var userId = UserId.From(request.UserId);
        var price = Money.EUR(request.Amount);

        // 1. Construct the unique distributed lock resource key for the target seat
        string lockKey = $"lock:seat:{seatId.Value}";

        // 2. Attempt to acquire the distributed lock via Redis Redlock
        // WaitTime = TimeSpan.Zero enforces a fail-fast policy to eliminate database connection queuing
        await using var lockHandle = await _lockService.AcquireLockAsync(
            resourceKey: lockKey,
            expiryTime: TimeSpan.FromMinutes(5),
            waitTime: TimeSpan.Zero,
            cancellationToken: cancellationToken);

        if (lockHandle is null)
        {
            // Another thread/instance acquired the lock first; trigger a concurrency exception
            throw new SeatAlreadyLockedException(seatId.Value);
        }

        // 3. Instantiate the Domain Aggregate (initialized in PENDING state with a 5-minute hold)
        var booking = Domain.Entities.Booking.CreatePending(seatId, userId, price);

        // 4. Persist the new aggregate via the repository
        await _bookingRepository.AddAsync(booking, cancellationToken);
        await _bookingRepository.SaveChangesAsync(cancellationToken);

        // 5. Map the domain entity state to the output response DTO
        return new ReserveSeatResponse(
            booking.Id,
            booking.SeatId.Value,
            booking.UserId.Value,
            booking.Status.ToString(),
            booking.ExpiresAt);
    }
}