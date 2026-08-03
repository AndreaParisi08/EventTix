namespace EventTix.Booking.Application.Abstractions;

using EventTix.Booking.Domain.Entities;
using EventTix.Booking.Domain.ValueObjects;

/// <summary>
/// Defines persistence operations for the <see cref="Booking"/> aggregate.
/// </summary>
public interface IBookingRepository
{
    /// <summary>
    /// Adds a new booking aggregate to the persistence context.
    /// </summary>
    Task AddAsync(Booking booking, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a booking aggregate by its unique identifier.
    /// </summary>
    Task<Booking?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a seat is currently reserved (either confirmed or pending with an active hold).
    /// </summary>
    Task<bool> IsSeatReservedAsync(SeatId seatId, CancellationToken cancellationToken = default);
}