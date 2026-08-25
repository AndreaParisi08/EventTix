using EventTix.BuildingBlocks.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventTix.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// EF Core mapping configuration for <see cref="OutboxMessage"/>.
///
/// Lives in BuildingBlocks (a different assembly than any given service's DbContext), so it is
/// applied EXPLICITLY from each context's OnModelCreating — e.g.
/// modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration()); in
/// BookingDbContext — rather than relying on ApplyConfigurationsFromAssembly, which
/// only scans the calling assembly and would silently miss this type.
///
/// Uses Postgres' native jsonb column type for <see cref="OutboxMessage.Content"/>. This is
/// a conscious coupling to PostgreSQL, not an oversight: every bounded context in EventTix is
/// Postgres-backed by project-wide convention (see docs/architecture/bounded-contexts.md), so
/// BuildingBlocks is a shared kernel for "this system", not a database-agnostic library.
/// </summary>
public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .HasColumnName("id")
            .ValueGeneratedNever(); // Id is assigned in-process by OutboxMessage.FromDomainEvent.

        builder.Property(m => m.OccurredOn)
            .HasColumnName("occurred_on")
            .IsRequired();

        builder.Property(m => m.Type)
            .HasColumnName("type")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(m => m.Content)
            .HasColumnName("content")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(m => m.ProcessedOn)
            .HasColumnName("processed_on");

        // The future Outbox publisher (EPIC-03/US-07) polls "WHERE processed_on IS NULL ORDER BY
        // occurred_on" to deliver events in order; this index keeps that query cheap as the table grows.
        builder.HasIndex(m => new { m.ProcessedOn, m.OccurredOn })
            .HasDatabaseName("ix_outbox_messages_processed_on_occurred_on");
    }
}
