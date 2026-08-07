using EventTix.Booking.Application.Abstractions;
using EventTix.Booking.Application.Exceptions;
using EventTix.Booking.Domain.ValueObjects;
using MediatR;

namespace EventTix.Booking.Application.Commands.ReserveSeat;

/// <summary>
/// Handles the <see cref="ReserveSeatCommand"/> to orchestrate high-concurrency seat reservations.
/// Leverages distributed locking to prevent seat contention before persisting the aggregate.
/// </summary>
public sealed class ReserveSeatCommandHandler : IRequestHandler<ReserveSeatCommand, ReserveSeatResponse>
{
    private readonly IDistributedLockService _lockService;
    private readonly IBookingRepository _bookingRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ReserveSeatCommandHandler(
        IDistributedLockService lockService,
        IBookingRepository bookingRepository,
        IUnitOfWork unitOfWork)
    {
        _lockService = lockService;
        _bookingRepository = bookingRepository;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Executes the seat reservation workflow under concurrency protection.
    /// </summary>
    /// <param name="request">The incoming command containing seat, user, and pricing information.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A <see cref="ReserveSeatResponse"/> containing the details of the reserved booking.</returns>
    /// <exception cref="SeatAlreadyLockedException">
    /// Thrown when the targeted seat is already locked by another process or user.
    /// </exception>
    public async Task<ReserveSeatResponse> Handle(ReserveSeatCommand request, CancellationToken cancellationToken)
    {
        var seatId = SeatId.From(request.SeatId);
        var userId = UserId.From(request.UserId);
        var price = Money.EUR(request.Amount);

        // Lock resource key for the target seat
        string lockKey = $"lock:seat:{seatId.Value}";

        // 1. Acquire distributed lock for the specific seat (TTL: 5s, Wait: 1s)
        await using var lockHandle = await _lockService.AcquireLockAsync(
            resourceKey: lockKey,
            expiryTime: TimeSpan.FromSeconds(5),
            waitTime: TimeSpan.FromSeconds(1),
            cancellationToken: cancellationToken);

        if (lockHandle is null)
        {
            throw new SeatAlreadyLockedException(seatId.Value);
        }

        // 2. Is the seat already booked in DB?
        bool isAlreadyReserved = await _bookingRepository.IsSeatReservedAsync(seatId, cancellationToken);
        if (isAlreadyReserved)
        {
            throw new InvalidOperationException($"Seat '{seatId.Value}' is no longer available.");
        }

        // 3. Instantiate the Domain Aggregate (initialized in PENDING state with a 5-minute hold)
        var booking = Domain.Entities.Booking.CreatePending(seatId, userId, price);

        // 4. Persist the new aggregate via the repository and save changes atomically
        await _bookingRepository.AddAsync(booking, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);


        // 5. Map the domain entity state to the output response DTO
        return new ReserveSeatResponse(
            booking.Id,
            booking.SeatId.Value,
            booking.UserId.Value,
            booking.Status.ToString(),
            booking.ExpiresAt);
    }
}