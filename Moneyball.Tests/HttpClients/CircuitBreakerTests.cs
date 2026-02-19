using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Moneyball.Core.Interfaces.ExternalAPIs;
using Moneyball.Tests.HttpClients.TestInfrastructure;
using Polly.CircuitBreaker;
using RichardSzalay.MockHttp;
using System.Net;

namespace Moneyball.Tests.HttpClients;

// ---------------------------------------------------------------------------
// Circuit breaker — both clients
// ---------------------------------------------------------------------------

public class CircuitBreakerTests
{
    [Fact]
    public async Task SportsDataService_CircuitBreaker_Opens_AfterFiveConsecutiveFailures()
    {
        var mock = new MockHttpMessageHandler();
        mock.When("*").Respond(HttpStatusCode.InternalServerError);

        // Real TimeProvider — the breaker needs actual elapsed time to
        // track its sampling window.
        var service = ServiceProviderFactory
            .Build(mock)
            .GetRequiredService<ISportsDataService>();

        for (var i = 0; i < ResiliencePolicies.BreakerThreshold; i++)
        {
            try { await service.GetNBATeamsAsync(); } catch { /* expected during warm-up */ }
        }

        // Once open, the breaker throws BrokenCircuitException before
        // the request reaches the handler — so even GetNBATeamsAsync
        // (which normally swallows HTTP errors) will surface it.
        await FluentActions.Awaiting(() => service.GetNBATeamsAsync())
            .Should().ThrowAsync<BrokenCircuitException>("circuit should be open after 5 consecutive failures");
    }

    [Fact]
    public async Task OddsDataService_CircuitBreaker_Opens_AfterFiveConsecutiveFailures()
    {
        var mock = new MockHttpMessageHandler();
        mock.When("*").Respond(HttpStatusCode.InternalServerError);

        var service = ServiceProviderFactory
            .Build(mock)
            .GetRequiredService<IOddsDataService>();

        // GetOddsAsync swallows HTTP errors, but BrokenCircuitException is
        // thrown by Polly before the HttpClient call is made, so it propagates.
        for (var i = 0; i < ResiliencePolicies.BreakerThreshold; i++)
        {
            try { await service.GetOddsAsync($"sport-{i}"); } catch { /* expected */ }
        }

        await FluentActions.Awaiting(() => service.GetOddsAsync("basketball_nba"))
            .Should().ThrowAsync<BrokenCircuitException>("circuit should be open after 5 consecutive failures");
    }

    [Fact]
    public async Task SportsDataService_CircuitBreaker_Opens_On429s()
    {
        var mock = new MockHttpMessageHandler();
        mock.When("*").Respond(HttpStatusCode.TooManyRequests);

        var service = ServiceProviderFactory
            .Build(mock)
            .GetRequiredService<ISportsDataService>();

        for (var i = 0; i < ResiliencePolicies.BreakerThreshold; i++)
        {
            try { await service.GetNBATeamsAsync(); } catch { /* expected */ }
        }

        await FluentActions.Awaiting(() => service.GetNBATeamsAsync())
            .Should().ThrowAsync<BrokenCircuitException>("429 responses should count as failures and open the circuit");
    }

    [Fact]
    public async Task OddsDataService_CircuitBreaker_Opens_On429s()
    {
        var mock = new MockHttpMessageHandler();
        mock.When("*").Respond(HttpStatusCode.TooManyRequests);

        var service = ServiceProviderFactory
            .Build(mock)
            .GetRequiredService<IOddsDataService>();

        for (var i = 0; i < ResiliencePolicies.BreakerThreshold; i++)
        {
            try { await service.GetOddsAsync($"sport-{i}"); } catch { /* expected */ }
        }

        await FluentActions.Awaiting(() => service.GetOddsAsync("basketball_nba"))
            .Should().ThrowAsync<BrokenCircuitException>("429 responses should count as failures and open the circuit");
    }

    [Fact]
    public async Task SportsDataService_CircuitBreaker_DoesNotOpen_BelowThreshold()
    {
        var callCount = 0;
        var mock = new MockHttpMessageHandler();

        mock.When("*").Respond(() =>
        {
            callCount++;
            // 4 failures (below threshold of 5), then succeed
            return Task.FromResult(callCount <= 4
                ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });
        });

        var service = ServiceProviderFactory
            .Build(mock, new FakeTimeProvider())
            .GetRequiredService<ISportsDataService>();

        for (var i = 0; i < 4; i++)
        {
            try { await service.GetNBATeamsAsync(); } catch { /* expected */ }
        }

        // Circuit should still be closed — this call must reach the handler
        await FluentActions.Awaiting(() => service.GetNBATeamsAsync())
            .Should().NotThrowAsync<BrokenCircuitException>("circuit should remain closed after only 4 failures");
    }

    [Fact]
    public async Task OddsDataService_CircuitBreaker_DoesNotOpen_BelowThreshold()
    {
        var callCount = 0;
        var mock = new MockHttpMessageHandler();

        mock.When("*").Respond(() =>
        {
            callCount++;
            return Task.FromResult(callCount <= 4
                ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") });
        });

        var service = ServiceProviderFactory
            .Build(mock, new FakeTimeProvider())
            .GetRequiredService<IOddsDataService>();

        for (var i = 0; i < 4; i++)
        {
            try { await service.GetOddsAsync($"sport-{i}"); } catch { /* expected */ }
        }

        await FluentActions.Awaiting(() => service.GetOddsAsync("basketball_nba"))
            .Should().NotThrowAsync<BrokenCircuitException>("circuit should remain closed after only 4 failures");
    }
}