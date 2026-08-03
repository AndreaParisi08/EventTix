namespace EventTix.Booking.Application.Abstractions;

using MediatR;

/// <summary>
/// Marker interface for MediatR commands that require idempotency protection backed by a unique key.
/// </summary>
/// <typeparam name="TResponse">The type of the response returned by the command execution.</typeparam>
public interface IIdempotentCommand<out TResponse> : IRequest<TResponse>
{
    /// <summary>
    /// Gets the unique idempotency key supplied by the client (e.g., HTTP header X-Idempotency-Key).
    /// </summary>
    string IdempotencyKey { get; }
}