namespace EventTix.Booking.Domain.Common;

/// <summary>
/// Represents a base Domain Entity that possesses a unique identity (<typeparamref name="TId"/>).
/// Two entities are considered equal if and only if they share the exact same identifier,
/// regardless of whether their internal state or properties differ.
/// </summary>
/// <typeparam name="TId">The type of the unique identifier for this entity.</typeparam>
public abstract class Entity<TId> where TId : notnull
{
    public TId Id { get; protected set; } = default!;

    public override bool Equals(object? obj)
    {
        if (obj is not Entity<TId> entity) return false;
        return EqualityComparer<TId>.Default.Equals(Id, entity.Id);
    }

    public override int GetHashCode() => EqualityComparer<TId>.Default.GetHashCode(Id);
}