using EventTix.BuildingBlocks.Domain;
using EventTix.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using BookingEntity = EventTix.Booking.Domain.Entities.Booking;

namespace EventTix.Booking.Infrastructure.Persistence;

/// <summary>
/// Database context for the Booking bounded context, managing EF Core sessions and entity mappings.
/// </summary>
public sealed class BookingDbContext : DbContext
{
    public BookingDbContext(DbContextOptions<BookingDbContext> options)
        : base(options)
    {
    }

    public DbSet<BookingEntity> Bookings => Set<BookingEntity>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BookingDbContext).Assembly);

        // OutboxMessage lives in the shared kernel (EventTix.BuildingBlocks), a different assembly
        // than this DbContext, so the assembly scan above does not pick up its configuration —
        // applied explicitly instead of switching to a second, easy-to-forget assembly scan.
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());

        base.OnModelCreating(modelBuilder);
    }
}
