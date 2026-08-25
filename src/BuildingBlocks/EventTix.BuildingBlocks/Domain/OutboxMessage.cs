using System.Text.Json;

namespace EventTix.BuildingBlocks.Domain;

/// <summary>
/// A single domain event captured in the Transactional Outbox.
///
/// Written in the SAME database transaction as the aggregate that raised the event — via an
/// EF Core SaveChangesInterceptor that hooks into SavingChangesAsync before the
/// transaction commits (that interceptor is the next piece to add; this type only models the row).
/// A separate, later process (the Outbox publisher) polls unprocessed rows and
/// publishes them to RabbitMQ, marking them processed once delivered.
///
/// Deliberately NOT modeled as a rich domain concept: an outbox row is a durable fact
/// ("this happened, here is its exact shape at that instant"), not a business entity with its
/// own invariants. It is intentionally reusable across bounded contexts — its name carries no
/// ubiquitous-language word (Seat, Booking, Order, Tenant), satisfying the BuildingBlocks
/// admission rule from ADR-0006.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; private set; }

    /// <summary>
    /// UTC instant the underlying domain event actually occurred — NOT when this row was written.
    /// </summary>
    public DateTime OccurredOn { get; private set; }

    /// <summary>
    /// CLR type name of the serialized event, so a future publisher can deserialize/route it.
    /// </summary>
    public string Type { get; private set; } = default!;

    /// <summary>
    /// The domain event, serialized to JSON at capture time.
    /// </summary>
    public string Content { get; private set; } = default!;

    /// <summary>
    /// Set once a publisher has successfully delivered this message. Null while pending.
    /// </summary>
    public DateTime? ProcessedOn { get; private set; }

    private OutboxMessage()
    {
        // EF Core materialization constructor.
    }

    private OutboxMessage(Guid id, DateTime occurredOn, string type, string content)
    {
        Id = id;
        OccurredOn = occurredOn;
        Type = type;
        Content = content;
    }

    /// <summary>
    /// Captures a raised domain event as a durable outbox row, ready to be added to the same
    /// DbContext instance (and therefore the same transaction) as the aggregate that raised it.
    /// </summary>
    public static OutboxMessage FromDomainEvent(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var clrType = domainEvent.GetType();

        return new OutboxMessage(
            id: Guid.NewGuid(),
            occurredOn: domainEvent.OccurredOn,
            type: clrType.AssemblyQualifiedName ?? clrType.FullName ?? clrType.Name,
            content: JsonSerializer.Serialize(domainEvent, clrType));
    }

    /// <summary>Marks this row as delivered. Called by the future Outbox publisher (EPIC-03/US-07).</summary>
    public void MarkProcessed(DateTime processedOnUtc) => ProcessedOn = processedOnUtc;
}
