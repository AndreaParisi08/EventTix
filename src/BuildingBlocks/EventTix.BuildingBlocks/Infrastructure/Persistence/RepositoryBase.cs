using EventTix.BuildingBlocks.Domain;
using Microsoft.EntityFrameworkCore;

public abstract class RepositoryBase<TEntity, TId, TContext>
    where TEntity : Entity<TId>
    where TContext : DbContext
{
    protected readonly TContext DbContext;

    protected RepositoryBase(TContext dbContext)
    {
        DbContext = dbContext;
    }

    public async Task<TEntity?> GetByIdAsync(TId id, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<TEntity>()
            .FirstOrDefaultAsync(e => e.Id!.Equals(id), cancellationToken);
    }

    public void Add(TEntity entity)
    {
        DbContext.Set<TEntity>().Add(entity);
    }

    public void Update(TEntity entity)
    {
        DbContext.Set<TEntity>().Update(entity);
    }

    public void Remove(TEntity entity)
    {
        DbContext.Set<TEntity>().Remove(entity);
    }
}