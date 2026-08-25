namespace EventTix.BuildingBlocks.Domain;

/// <summary>
/// Non-generic contract exposing an aggregate's pending domain events.
///
/// Exists purely so infrastructure code (the Outbox interceptor) can ask EF Core's ChangeTracker
/// for "anything with pending domain events" generically, without referencing any specific
/// aggregate type. AggregateRoot already exposes DomainEvents/ClearDomainEvents, but it
/// is a generic class — C# cannot pattern-match against an open generic like
/// "entity is AggregateRoot". 
/// </summary>
public interface IHasDomainEvents
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    void ClearDomainEvents();
}
