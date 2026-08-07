namespace EventTix.Booking.Application.Abstractions;

/// <summary>
/// Abstraction for distributed locking mechanisms (e.g., Redis Redlock).
/// </summary>
public interface IDistributedLockService
{
    /// <summary>
    /// Attempts to acquire an exclusive distributed lock for a specific resource.
    /// </summary>
    /// <returns>An IAsyncDisposable handling lock release, or null if acquisition failed.</returns>
    Task<IAsyncDisposable?> AcquireLockAsync(
        string resourceKey,
        TimeSpan expiryTime,
        TimeSpan waitTime,
        CancellationToken cancellationToken = default);
}