using EventTix.BuildingBlocks.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EventTix.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// EF Core SaveChangesInterceptor implementing the write side of the Transactional Outbox pattern.
///
/// Runs automatically right before SaveChangesAsync sends SQL to the database — anything staged
/// here becomes part of the SAME transaction/batch as every other pending change on that DbContext.
/// That is the entire trick behind the Outbox pattern: it turns "write the order" and "record the
/// event to publish later" from two separate, independently-failable operations into a single
/// atomic database write, without needing a distributed transaction across Postgres and RabbitMQ.
///
/// Registered per-DbContext via AddDbContext(...).AddInterceptors(...) — see
/// EventTix.Booking.Infrastructure/DependencyInjection.cs (registration intentionally NOT wired up
/// yet as of this commit; this interceptor has no effect until it is).
/// </summary>
public sealed class OutboxSaveChangesInterceptor : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            CaptureDomainEventsAsOutboxMessages(eventData.Context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void CaptureDomainEventsAsOutboxMessages(DbContext context)
    {
        // Materialize the list first: ClearDomainEvents() below must not mutate a collection that
        // ChangeTracker.Entries<T>() is still lazily enumerating.
        var aggregatesWithEvents = context.ChangeTracker
            .Entries<IHasDomainEvents>()
            .Select(entry => entry.Entity)
            .Where(aggregate => aggregate.DomainEvents.Count > 0)
            .ToList();

        foreach (var aggregate in aggregatesWithEvents)
        {
            foreach (var domainEvent in aggregate.DomainEvents)
            {
                context.Set<OutboxMessage>().Add(OutboxMessage.FromDomainEvent(domainEvent));
            }

            // Prevents the same events from becoming duplicate outbox rows if this DbContext
            // instance is ever saved again later (e.g. a retry within the same request).
            aggregate.ClearDomainEvents();
        }
    }
}
