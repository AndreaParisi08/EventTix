namespace EventTix.Booking.Infrastructure.Persistence;

using EventTix.Booking.Application.Abstractions;

/// <summary>
/// Infrastructure implementation of the Unit of Work pattern using EF Core DbContext.
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly BookingDbContext _dbContext;

    public UnitOfWork(BookingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Persists all tracked changes to the underlying PostgreSQL database in a single atomic transaction.
    /// </summary>
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}