using Microsoft.EntityFrameworkCore;

namespace EventTix.BuildingBlocks.Infrastructure.Persistence;

public abstract class RepositoryBase<TEntity> where TEntity : class
{
    protected readonly DbContext DbContext;

    protected RepositoryBase(DbContext dbContext)
    {
        DbContext = dbContext;
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