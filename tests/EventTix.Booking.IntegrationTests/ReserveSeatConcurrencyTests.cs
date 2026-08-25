using EventTix.Booking.Application.Commands.ReserveSeat;
using EventTix.Booking.Application.Exceptions;
using EventTix.Booking.Domain.ValueObjects;
using EventTix.Booking.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EventTix.Booking.IntegrationTests;

/// <summary>
/// Proves the core value proposition of US-01: under real concurrent load, exactly one request for
/// a given seat succeeds and every other request is rejected — never two, never zero (a stuck lock).
/// This is deliberately an integration test (real Postgres + Redis via <see cref="BookingApiTestFixture"/>),
/// not a mocked unit test: mocking the lock service would only prove the handler calls the mock in
/// the expected order, not that concurrent requests are actually safe against each other.
/// </summary>
public sealed class ReserveSeatConcurrencyTests : IClassFixture<BookingApiTestFixture>
{
    private const int ConcurrentRequests = 20;

    private readonly BookingApiTestFixture _fixture;

    public ReserveSeatConcurrencyTests(BookingApiTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ReserveSeat_WhenManyRequestsRaceForTheSameSeat_OnlyOneSucceeds()
    {
        var seatId = NewSeatId(); // unique per run: never collides with another test/run in the shared container.

        var outcomes = await Task.WhenAll(
            Enumerable.Range(0, ConcurrentRequests)
                .Select(i => SendReserveSeatAsync(seatId, index: i)));

        var successCount = outcomes.Count(o => o.Succeeded);
        successCount.Should().Be(1, "exactly one of many concurrent requests for the same seat must win");

        var failures = outcomes.Where(o => !o.Succeeded).ToList();
        failures.Should().HaveCount(ConcurrentRequests - 1);

        // Every loser must fail for one of the two EXPECTED contention reasons — either it never
        // even got the Redis lock, or it got the lock a moment too late and found the seat already
        // booked in Postgres. Anything else (a null ref, a DB constraint blow-up, ...) is a real bug,
        // not contention, and should fail this test loudly rather than being lumped in as "a failure".
        // FluentAssertions' OnlyContain takes an Expression<Func<T,bool>>, not a plain delegate, so
        // it can print the predicate's source in failure messages. C#'s "or" pattern combinator
        // (unlike a plain "is Type" check) cannot be translated into an expression tree — hence two
        // separate "is" checks joined by || instead of one "is A or B".
        failures.Should().OnlyContain(o =>
            o.Exception is SeatAlreadyLockedException || o.Exception is InvalidOperationException);

        // Ground truth: check the database directly rather than trusting only what the handler
        // reported — this catches a bug where the handler's return value lies about what actually
        // got persisted.
        await using var verificationScope = _fixture.Services.CreateAsyncScope();
        var dbContext = verificationScope.ServiceProvider.GetRequiredService<BookingDbContext>();

        var seatIdValue = SeatId.From(seatId);
        var bookingsForSeat = await dbContext.Bookings
            .Where(b => b.SeatId == seatIdValue)
            .ToListAsync();

        bookingsForSeat.Should().HaveCount(1,
            "the database must reflect exactly one booking for the contested seat, " +
            "regardless of what each individual handler call reported");
    }

    /// <summary>
    /// Sends one ReserveSeatCommand from its OWN DI scope — mirroring how ASP.NET Core gives every
    /// real HTTP request its own scope, and therefore its own BookingDbContext instance (registered
    /// Scoped). Without a fresh scope per call, these concurrent Task.WhenAll calls would share ONE
    /// DbContext instance across threads — DbContext is not thread-safe, so that would blow up with
    /// an unrelated "a second operation was started on this context" error instead of actually
    /// testing seat contention. This per-scope isolation is also exactly why an in-process lock
    /// (a C# `lock` statement) could never solve this problem in the real app: separate scopes on
    /// separate threads (or separate machines) can't coordinate with each other in-process — only an
    /// external, shared coordinator like Redis can.
    /// </summary>
    private async Task<ReservationOutcome> SendReserveSeatAsync(string seatId, int index)
    {
        await using var scope = _fixture.Services.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        // Each call needs its OWN idempotency key: the pipeline's IdempotentCommandBehavior caches
        // responses in Redis by key, so reusing one key across calls would short-circuit most of them
        // before they ever reached the seat lock — a completely different (and irrelevant) code path.
        var command = new ReserveSeatCommand(seatId, Guid.NewGuid(), 10m, $"idem-{seatId}-{index}");

        try
        {
            var response = await sender.Send(command);
            return ReservationOutcome.Success(response);
        }
        catch (Exception ex)
        {
            return ReservationOutcome.Failure(ex);
        }
    }

    /// <summary>
    /// SeatId's own validator caps it at 20 characters, so the unique suffix has to stay short too.
    /// </summary>
    private static string NewSeatId() => $"CONC-{Guid.NewGuid():N}"[..20];

    private sealed record ReservationOutcome(bool Succeeded, ReserveSeatResponse? Response, Exception? Exception)
    {
        public static ReservationOutcome Success(ReserveSeatResponse response) => new(true, response, null);

        public static ReservationOutcome Failure(Exception exception) => new(false, null, exception);
    }
}
