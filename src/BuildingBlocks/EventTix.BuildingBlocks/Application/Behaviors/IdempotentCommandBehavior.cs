namespace EventTix.BuildingBlocks.Application.Behaviors;

using EventTix.BuildingBlocks.Application.Abstractions;
using MediatR;
using StackExchange.Redis;
using System.Text.Json;

/// <summary>
/// MediatR pipeline behavior that intercepts commands implementing <see cref="IIdempotentCommand{TResponse}"/>
/// to guarantee idempotent execution using Redis storage.
/// </summary>
public class IdempotentCommandBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IIdempotentCommand<TResponse>
{
    private readonly IConnectionMultiplexer _redis;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);

    public IdempotentCommandBehavior(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2016:Inoltrare il parametro 'CancellationToken' ai metodi", Justification = "<In sospeso>")]
    public async Task<TResponse> Handle(
        TRequest request, 
        RequestHandlerDelegate<TResponse> next, 
        CancellationToken cancellationToken)
    {
        // Bypass idempotency check if key is empty or whitespace
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            return await next();
        }

        var db = _redis.GetDatabase();
        string cacheKey = $"idempotency:{request.IdempotencyKey}";

        // 1. Check if the request was already processed
        var cachedResponse = await db.StringGetAsync(cacheKey);
        if (!cachedResponse.IsNull)
        {
            // Return cached result directly without executing the handler
            var deserializedResult = JsonSerializer.Deserialize<TResponse>(cachedResponse.ToString()!)!;
            if (deserializedResult is not null)
            {
                // Return cached response directly without executing the command handler!
                return deserializedResult;
            }
        }

        // 2. Execute the actual command handler (ReserveSeatCommandHandler)
        var response = await next();

        // 3. Persist the response in Redis for future retries
        if (response is not null)
        {
            string jsonResponse = JsonSerializer.Serialize(response);
            await db.StringSetAsync(cacheKey, jsonResponse, CacheDuration);
        }

        return response;
    }
}