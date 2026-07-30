namespace EventTix.Booking.Application.Abstractions;

/// <summary>
/// Abstraction for persistence operations on the Booking Aggregate.
/// </summary>
public interface IBookingRepository
{
    Task AddAsync(Domain.Entities.Booking booking, CancellationToken cancellationToken = default);
    Task<Domain.Entities.Booking?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}