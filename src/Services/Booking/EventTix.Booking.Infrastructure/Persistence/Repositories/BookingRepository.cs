using EventTix.Booking.Application.Abstractions;
using EventTix.Booking.Domain.Enums;
using EventTix.Booking.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using BookingEntity = EventTix.Booking.Domain.Entities.Booking;

namespace EventTix.Booking.Infrastructure.Persistence.Repositories;

public sealed class BookingRepository : IBookingRepository
{
    private readonly BookingDbContext _dbContext;

    public BookingRepository(BookingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    //<inheritdoc/>
    public async Task AddAsync(BookingEntity booking, CancellationToken cancellationToken = default)
    {
        await _dbContext.Bookings.AddAsync(booking, cancellationToken);
    }

    //<inheritdoc/>
    public async Task<BookingEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Bookings
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    //<inheritdoc/>
    public async Task<bool> IsSeatReservedAsync(SeatId seatId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        // A seat is reserved if it is CONFIRMED or currently held in PENDING state before expiration
        return await _dbContext.Bookings
            .AnyAsync(b => b.SeatId == seatId &&
                (b.Status == BookingStatus.Confirmed ||
                (b.Status == BookingStatus.Pending && b.ExpiresAt > now)),
                cancellationToken);
    }
}