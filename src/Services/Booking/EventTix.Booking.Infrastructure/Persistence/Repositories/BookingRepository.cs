using EventTix.Booking.Application.Abstractions;
using EventTix.Booking.Domain.Entities;
using EventTix.Booking.Domain.Enums;
using EventTix.Booking.Domain.ValueObjects;
using EventTix.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using BookingEntity = EventTix.Booking.Domain.Entities.Booking;

namespace EventTix.Booking.Infrastructure.Persistence.Repositories;

public sealed class BookingRepository : RepositoryBase<BookingEntity>, IBookingRepository
{
    public BookingRepository(BookingDbContext dbContext) : base (dbContext)
    {
    }

    //<inheritdoc/>
    public async Task<BookingEntity?> GetByIdAsync(BookingId id, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<BookingEntity>()
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task<bool> IsSeatReservedAsync(SeatId seatId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        // A seat is reserved if it is CONFIRMED or currently held in PENDING state before expiration
        return await DbContext.Set<BookingEntity>()
            .AnyAsync(b => b.SeatId == seatId &&
                (b.Status == BookingStatus.Confirmed ||
                (b.Status == BookingStatus.Pending && b.ExpiresAt > now)),
                cancellationToken);
    }
}