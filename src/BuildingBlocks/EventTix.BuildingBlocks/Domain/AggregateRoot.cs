namespace EventTix.BuildingBlocks.Domain;

/// <summary>
/// Represents an Aggregate Root in Domain-Driven Design (DDD).
/// An Aggregate Root is the primary entry point to an aggregate boundary, responsible for
/// maintaining transactional consistency, enforcing domain invariants, and managing domain events.
/// All external operations on objects within the aggregate must pass through this root entity.
/// </summary>
/// <typeparam name="TId">The type of the unique identifier for this aggregate root.</typeparam>
public abstract class AggregateRoot<TId> : Entity<TId> where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>
    /// Gets a read-only collection of domain events raised by this aggregate root.
    /// </summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>
    /// Registers a new domain event to be dispatched after persistence.
    /// </summary>
    /// <param name="domainEvent">The domain event instance to add.</param>
    protected void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    /// <summary>
    /// Clears all recorded domain events. Typically invoked by the Outbox processor or DbContext after dispatching.
    /// </summary>
    public void ClearDomainEvents() => _domainEvents.Clear();
}