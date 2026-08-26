using System.Diagnostics;
using EventTix.Booking.Application.Commands.ReserveSeat;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace EventTix.Booking.IntegrationTests;

/// <summary>
/// Measures end-to-end latency of <see cref="ReserveSeatCommand"/> against real Postgres + Redis
/// (via <see cref="BookingApiTestFixture"/>), to check US-01's acceptance criterion of "under 50ms"
/// for a successful reservation.
///
/// Deliberately NOT wired as a hard pass/fail gate on every `dotnet test` run: latency numbers from
/// Testcontainers-backed infrastructure are sensitive to host/virtualization overhead (Docker running
/// inside Hyper-V on Windows adds real overhead a native Linux CI runner wouldn't have), so a strict
/// "&lt; 50ms or fail the build" assertion here would be flaky by construction — red on a busy dev
/// machine or a slower CI runner even when the handler itself hasn't regressed at all. Instead this
/// reports the measured percentiles (visible with `dotnet test --logger "console;verbosity=detailed"`)
/// and only fails on a generous outlier bound, which exists to catch actual regressions (e.g. an
/// accidentally-added blocking call or N+1 query), not environment noise.
///
/// Tagged "Benchmark" so it can be excluded from routine runs, keeping the fast correctness suite
/// (<see cref="ReserveSeatConcurrencyTests"/>) uncluttered: `dotnet test --filter Category!=Benchmark`.
/// Run this one deliberately when you want the latency numbers, not on every push.
/// </summary>
[Trait("Category", "Benchmark")]
public sealed class ReserveSeatLatencyBenchmark : IClassFixture<BookingApiTestFixture>
{
    // Sequential, not concurrent, on purpose: this measures the baseline single-request latency of
    // the happy path US-01's AC describes, not throughput under contention — that question is already
    // answered by ReserveSeatConcurrencyTests. Each call also targets its OWN seat, so no two calls
    // ever compete for the same Redis lock; contention overhead would only pollute this measurement.
    private const int WarmupIterations = 3;
    private const int MeasuredIterations = 30;

    // Generous on purpose — see class remarks. This is a regression tripwire, not a re-statement of
    // the AC itself; the AC is judged by the p50/p95 numbers printed to test output, read by a human.
    private const double MaxAcceptableP95Ms = 250;

    private readonly BookingApiTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public ReserveSeatLatencyBenchmark(BookingApiTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public async Task ReserveSeat_HappyPath_LatencyIsWithinExpectedBounds()
    {
        // Warm-up: excluded from the measurement. The very first calls pay one-time costs unrelated to
        // steady-state handler latency — JIT tiering, connection pool population, EF Core's internal
        // model caching — none of which the AC is actually asking about.
        for (var i = 0; i < WarmupIterations; i++)
            await SendReserveSeatAsync();

        var latenciesMs = new List<double>(MeasuredIterations);
        for (var i = 0; i < MeasuredIterations; i++)
            latenciesMs.Add(await SendReserveSeatAsync());

        latenciesMs.Sort();
        var p50 = Percentile(latenciesMs, 0.50);
        var p95 = Percentile(latenciesMs, 0.95);
        var max = latenciesMs[^1];

        _output.WriteLine(
            $"ReserveSeatCommand latency over {MeasuredIterations} sequential requests " +
            $"(warm, single-seat, no lock contention): " +
            $"p50={p50:F1}ms  p95={p95:F1}ms  max={max:F1}ms  — US-01 AC target: < 50ms.");

        Assert.True(p95 < MaxAcceptableP95Ms,
            $"p95 latency {p95:F1}ms exceeded the {MaxAcceptableP95Ms}ms regression tripwire " +
            "— this suggests an actual regression, not just environment noise.");
    }

    private async Task<double> SendReserveSeatAsync()
    {
        await using var scope = _fixture.Services.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var seatId = NewSeatId();
        var command = new ReserveSeatCommand(seatId, Guid.NewGuid(), 10m, $"idem-bench-{seatId}");

        var stopwatch = Stopwatch.StartNew();
        await sender.Send(command);
        stopwatch.Stop();

        return stopwatch.Elapsed.TotalMilliseconds;
    }

    /// <summary>
    /// SeatId's own validator caps it at 20 characters — same constraint as in ReserveSeatConcurrencyTests.
    /// </summary>
    private static string NewSeatId() => $"BENCH-{Guid.NewGuid():N}"[..20];

    private static double Percentile(IReadOnlyList<double> sortedValues, double percentile)
    {
        var rank = percentile * (sortedValues.Count - 1);
        var lowerIndex = (int)Math.Floor(rank);
        var upperIndex = (int)Math.Ceiling(rank);
        if (lowerIndex == upperIndex)
            return sortedValues[lowerIndex];

        var weight = rank - lowerIndex;
        return sortedValues[lowerIndex] * (1 - weight) + sortedValues[upperIndex] * weight;
    }
}
