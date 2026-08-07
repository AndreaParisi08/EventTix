using EventTix.Booking.Application.Abstractions;
using StackExchange.Redis;

namespace EventTix.Booking.Infrastructure.Locking
{
    public sealed class RedisDistributedLockService : IDistributedLockService
    {
        private readonly IConnectionMultiplexer _redis;

        /// <summary>
        /// Provides an implementation of the distributed lock service 
        /// to ensure thread-safe concurrency control across distributed instances.
        /// </summary>
        public RedisDistributedLockService(IConnectionMultiplexer redis)
        {
            _redis = redis;
        }

        /// <summary>
        /// Attempts to acquire an exclusive distributed lock on a specified resource key.
        /// </summary>
        /// <param name="resourceKey">The unique key identifying the resource to lock (e.g., seat ID).</param>
        /// <param name="expiryTime">The time-to-live (TTL) duration after which the lock automatically expires in Redis.</param>
        /// <param name="waitTime">The maximum time window to retry acquiring the lock before giving up.</param>
        /// <param name="cancellationToken">A token to observe while waiting for lock availability.</param>
        /// <returns>
        /// An <see cref="IAsyncDisposable"/> lock handle if acquired successfully; otherwise, <c>null</c>.
        /// </returns>
        public async Task<IAsyncDisposable?> AcquireLockAsync(
            string resourceKey,
            TimeSpan expiryTime,
            TimeSpan waitTime,
            CancellationToken cancellationToken = default)
        {
            var db = _redis.GetDatabase();
            var lockValue = Guid.NewGuid().ToString();
            var timeoutAt = DateTime.UtcNow.Add(waitTime);

            while (!cancellationToken.IsCancellationRequested)
            {
                // Atomically acquire the lock using SET resourceKey lockValue expiryTime
                bool acquired = await db.LockTakeAsync(resourceKey, lockValue, expiryTime);

                if (acquired)
                {
                    return new RedisLockHandle(db, resourceKey, lockValue);
                }

                // Exit immediately if no wait time was specified or if the timeout has elapsed
                if (waitTime == TimeSpan.Zero || DateTime.UtcNow >= timeoutAt)
                {
                    break;
                }

                // Wait briefly before retrying (polling backoff strategy)
                try
                {
                    await Task.Delay(50, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            return null;
        }

        /// <summary>
        /// Represents an active distributed lock handle that atomically releases 
        /// the Redis lock key upon asynchronous disposal.
        /// </summary>
        private sealed class RedisLockHandle : IAsyncDisposable
        {
            private readonly IDatabase _db;
            private readonly string _key;
            private readonly string _value;
            private bool _disposed;

            public RedisLockHandle(IDatabase db, string key, string value)
            {
                _db = db;
                _key = key;
                _value = value;
            }

            /// <summary>
            /// Releases the held lock key in Redis
            /// </summary>
            public async ValueTask DisposeAsync()
            {
                if (_disposed) return;
                _disposed = true;

                // Atomically release the lock using an internal Lua script that verifies value ownership
                await _db.LockReleaseAsync(_key, _value);
            }
        }
    }
}