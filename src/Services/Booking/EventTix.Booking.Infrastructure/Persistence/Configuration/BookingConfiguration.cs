using EventTix.Booking.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BookingEntity = EventTix.Booking.Domain.Entities.Booking;

namespace EventTix.Booking.Infrastructure.Persistence;
/// <summary>
/// Entity Framework Core mapping configuration for the <see cref="BookingEntity"/> aggregate root.
/// </summary>
public sealed class BookingConfiguration : IEntityTypeConfiguration<BookingEntity>
{
    public void Configure(EntityTypeBuilder<BookingEntity> builder)
    {
        // Define table name
        builder.ToTable("bookings");

        // Primary Key
        builder.HasKey(b => b.Id);

        // Convert Strongly-Typed SeatId to string
        builder.Property(b => b.SeatId)
            .HasConversion(id => id.Value, value => SeatId.From(value))
            .HasColumnName("seat_id")
            .IsRequired();

        // Convert Strongly-Typed UserId to Guid
        builder.Property(b => b.UserId)
            .HasConversion(id => id.Value, value => UserId.From(value))
            .HasColumnName("user_id")
            .IsRequired();

        // Complex Value Object for Money
        builder.ComplexProperty(b => b.Price, priceBuilder =>
        {
            priceBuilder.Property(m => m.Amount)
                .HasColumnName("price_amount")
                .HasPrecision(18, 2)
                .IsRequired();

            priceBuilder.Property(m => m.Currency)
                .HasColumnName("price_currency")
                .HasMaxLength(3)
                .IsRequired();
        });

        // Map Enum to string for readability
        builder.Property(b => b.Status)
            .HasConversion<string>()
            .HasColumnName("status")
            .IsRequired();

        // Audit & Expiration Timestamps
        builder.Property(b => b.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(b => b.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        // Performance Index for IsSeatReservedAsync queries
        builder.HasIndex(b => new { b.SeatId, b.Status })
            .HasDatabaseName("ix_bookings_seat_id_status");
    }
}