using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Moneyball.Core.Interfaces.ExternalAPIs;
using Moneyball.Tests.HttpClients.TestInfrastructure;
using RichardSzalay.MockHttp;
using Shouldly;
using System.Net;

namespace Moneyball.Tests.HttpClients;

// ---------------------------------------------------------------------------
// SportsDataService — retry on transient errors
//
// GetNBAScheduleAsync builds the full URL internally and checks
// IsSuccessStatusCode itself, so after retries are exhausted the service
// receives the final (failed) response and returns an empty list rather
// than throwing. We verify resilience by inspecting call counts.
// ---------------------------------------------------------------------------

public class SportsDataServiceRetryTests
{
    // Single-day range is used throughout to avoid the 1 s Task.Delay
    // firing multiple times. See note at the bottom of this file about
    // extracting Task.Delay for full testability.
    private static readonly DateTime TestDate = new(2024, 11, 1);

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)] // 500
    [InlineData(HttpStatusCode.BadGateway)]          // 502
    [InlineData(HttpStatusCode.ServiceUnavailable)]  // 503
    [InlineData(HttpStatusCode.GatewayTimeout)]      // 504
    [InlineData(HttpStatusCode.TooManyRequests)]     // 429
    public async Task GetNBASchedule_RetriesOn_TransientStatusCodes(HttpStatusCode statusCode)
    {
        var callCount = 0;
        var mock = new MockHttpMessageHandler();

        mock.When("*").Respond(() =>
        {
            callCount++;
            // Fail twice, then succeed so we can observe the retry count
            return Task.FromResult(callCount < 3
                ? new HttpResponseMessage(statusCode)
                : new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StringContent("{}") });
        });

        var service = ServiceProviderFactory
            .Build(mock, new FakeTimeProvider())
            .GetRequiredService<ISportsDataService>();

        await service.GetNBAScheduleAsync(TestDate, TestDate);

        callCount.ShouldBe(3, "policy should retry twice before succeeding on the third attempt");
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]          // 400
    [InlineData(HttpStatusCode.Unauthorized)]        // 401 — bad API key
    [InlineData(HttpStatusCode.Forbidden)]           // 403
    [InlineData(HttpStatusCode.NotFound)]            // 404 — no games that day
    [InlineData(HttpStatusCode.UnprocessableEntity)] // 422
    public async Task GetNBASchedule_DoesNotRetry_OnClientErrors(HttpStatusCode statusCode)
    {
        var callCount = 0;
        var mock = new MockHttpMessageHandler();

        mock.When("*").Respond(() =>
        {
            callCount++;
            return Task.FromResult(new HttpResponseMessage(statusCode));
        });

        var service = ServiceProviderFactory
            .Build(mock, new FakeTimeProvider())
            .GetRequiredService<ISportsDataService>();

        await service.GetNBAScheduleAsync(TestDate, TestDate);

        callCount.ShouldBe(1, "client errors must never trigger a retry");
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task GetNBAGameStatistics_RetriesOn_TransientStatusCodes(HttpStatusCode statusCode)
    {
        var callCount = 0;
        var mock = new MockHttpMessageHandler();

        mock.When("*").Respond(() =>
        {
            callCount++;
            return Task.FromResult(callCount < 3
                ? new HttpResponseMessage(statusCode)
                : new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StringContent("{}") });
        });

        var service = ServiceProviderFactory
            .Build(mock, new FakeTimeProvider())
            .GetRequiredService<ISportsDataService>();

        await service.GetNBAGameStatisticsAsync("abc-123");

        callCount.ShouldBe(3);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task GetNBATeams_RetriesOn_TransientStatusCodes(HttpStatusCode statusCode)
    {
        var callCount = 0;
        var mock = new MockHttpMessageHandler();

        mock.When("*").Respond(() =>
        {
            callCount++;
            return Task.FromResult(callCount < 3
                ? new HttpResponseMessage(statusCode)
                : new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StringContent("{}") });
        });

        var service = ServiceProviderFactory
            .Build(mock, new FakeTimeProvider())
            .GetRequiredService<ISportsDataService>();

        await service.GetNBATeamsAsync();

        callCount.ShouldBe(3);
    }

    [Fact]
    public async Task GetNBATeams_ExhaustsAllRetries_ThenThrows()
    {
        // GetNBATeamsAsync calls EnsureSuccessStatusCode(), so after all
        // retries are exhausted it will throw rather than return empty.
        var callCount = 0;
        var mock = new MockHttpMessageHandler();

        mock.When("*").Respond(() =>
        {
            callCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        });

        var service = ServiceProviderFactory
            .Build(mock, new FakeTimeProvider())
            .GetRequiredService<ISportsDataService>();

        await FluentActions.Awaiting(() => service.GetNBATeamsAsync())
            .Should().ThrowAsync<HttpRequestException>("EnsureSuccessStatusCode throws after all retries are exhausted");

        callCount.ShouldBe(4, "1 initial attempt + 3 retries");
    }

    [Fact]
    public async Task GetNBASchedule_ExhaustsAllRetries_ReturnsEmptyList()
    {
        // Unlike GetNBATeamsAsync, GetNBAScheduleAsync handles non-success
        // responses gracefully and returns an empty list rather than throwing.
        var callCount = 0;
        var mock = new MockHttpMessageHandler();

        mock.When("*").Respond(() =>
        {
            callCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        });

        var service = ServiceProviderFactory
            .Build(mock, new FakeTimeProvider())
            .GetRequiredService<ISportsDataService>();

        var result = await service.GetNBAScheduleAsync(TestDate, TestDate);

        callCount.ShouldBe(4, "1 initial attempt + 3 retries");
        result.Should().BeEmpty("no successful response means no games returned");
    }

    [Fact]
    public async Task GetNBAGameStatistics_ExhaustsAllRetries_ReturnsNull()
    {
        var callCount = 0;
        var mock = new MockHttpMessageHandler();

        mock.When("*").Respond(() =>
        {
            callCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        });

        var service = ServiceProviderFactory
            .Build(mock, new FakeTimeProvider())
            .GetRequiredService<ISportsDataService>();

        var result = await service.GetNBAGameStatisticsAsync("game-999");

        callCount.ShouldBe(4);
        result.Should().BeNull("service returns null when all retries fail");
    }
}

// ---------------------------------------------------------------------------
// NOTE — Task.Delay in GetNBAScheduleAsync
//
// The production code calls `await Task.Delay(1000)` after each date to
// respect the SportRadar rate limit. This makes multi-day tests slow (1 s
// per day). To make this fully testable without real delays, extract the
// delay behind a TimeProvider:
//
//   await Task.Delay(TimeSpan.FromSeconds(1), _timeProvider, ct);
//
// Then inject a FakeTimeProvider in tests and advance it manually. For now,
// tests that exercise the loop (GetNBASchedule_IteratesOncePerDay) use a
// 3-day range and accept the ~3 s overhead.
// ---------------------------------------------------------------------------