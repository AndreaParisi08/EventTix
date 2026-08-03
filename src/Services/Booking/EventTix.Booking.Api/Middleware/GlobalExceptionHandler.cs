using EventTix.Booking.Application.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace EventTix.Booking.Api.Middleware;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);

        if (exception is ValidationException validationEx)
        {
            var validationProblemDetails = CreateValidationProblemDetails(httpContext, validationEx);

            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await httpContext.Response.WriteAsJsonAsync(validationProblemDetails, cancellationToken);

            return true;
        }

        var (statusCode, title, detail) = exception switch
        {
            SeatAlreadyLockedException lockEx => (
                StatusCodes.Status409Conflict,
                "Seat Collision",
                lockEx.Message),

            InvalidOperationException invalidEx => (
                StatusCodes.Status409Conflict,
                "Business Rule Violation",
                invalidEx.Message),

            ArgumentException argEx => (
                StatusCodes.Status400BadRequest,
                "Invalid Request Payload",
                argEx.Message),

            _ => (
                StatusCodes.Status500InternalServerError,
                "Server Error",
                "An unexpected error occurred.")
        };

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }

    private static HttpValidationProblemDetails CreateValidationProblemDetails(
        HttpContext httpContext,
        ValidationException validationEx)
    {
        var errors = validationEx.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray());

        return new HttpValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation Error",
            Detail = "One or more validation rules failed.",
            Instance = httpContext.Request.Path
        };
    }
}