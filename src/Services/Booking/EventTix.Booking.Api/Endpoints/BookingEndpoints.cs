namespace EventTix.Booking.Api.Endpoints;

using EventTix.Booking.Application.Bookings.Commands.ReserveSeat;
using MediatR;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Defines Minimal API endpoint routes for booking-related operations.
/// </summary>
public static class BookingEndpoints
{
    /// <summary>
    /// Maps all booking endpoints to the ASP.NET Core routing pipeline.
    /// </summary>
    /// <param name="app">The endpoint route builder instance.</param>
    /// <returns>The updated <see cref="IEndpointRouteBuilder"/>.</returns>
    public static IEndpointRouteBuilder MapBookingEndpoints(this IEndpointRouteBuilder app)
    {
        // Create a route group for booking-related endpoints, with a common base path and tags for documentation
        // Tag used for grouping endpoints in Swagger/OpenAPI documentations
        var group = app.MapGroup("/api/bookings")
            .WithTags("Bookings");

        // Define a POST endpoint for reserving a seat, with detailed metadata for documentation and response types (Swagger)
        group.MapPost("/", ReserveSeatAsync)
            .WithName("ReserveSeat")
            .WithSummary("Reserves a specific seat for an event")
            .WithDescription("Acquires a temporary hold on a seat using distributed locking and idempotency protection.")
            .Produces<ReserveSeatResponse>(StatusCodes.Status201Created)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        return app;
    }

    /// <summary>
    /// Handles HTTP POST requests for reserving a seat.
    /// </summary>
    private static async Task<IResult> ReserveSeatAsync(
        [FromBody] ReserveSeatRequest request,
        [FromHeader(Name = "X-Idempotency-Key")] string? idempotencyKey,
        ISender sender,
        CancellationToken cancellationToken)
    {
        // Fallback to a new GUID if no idempotency key header was provided by the client
        string key = string.IsNullOrWhiteSpace(idempotencyKey)
            ? Guid.NewGuid().ToString()
            : idempotencyKey;

        var command = new ReserveSeatCommand(
            request.SeatId,
            request.UserId,
            request.Amount,
            key);

        // Dispatch command to MediatR pipeline
        ReserveSeatResponse response = await sender.Send(command, cancellationToken);

        return Results.Created($"/api/bookings/{response.BookingId}", response);
    }
}

/// <summary>
/// DTO representing the incoming HTTP request payload for reserving a seat.
/// </summary>
public sealed record ReserveSeatRequest(
    string SeatId,
    Guid UserId,
    decimal Amount);