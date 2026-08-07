using EventTix.Booking.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

public class UnitOfWork<TContext> : IUnitOfWork
    where TContext : DbContext
{
    private readonly TContext _dbContext;

    public UnitOfWork(TContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}