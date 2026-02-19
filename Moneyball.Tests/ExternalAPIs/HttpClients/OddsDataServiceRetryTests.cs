using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Moneyball.Core.Interfaces.ExternalAPIs;
using Moneyball.Tests.ExternalAPIs.HttpClients.TestInfrastructure;
using RichardSzalay.MockHttp;
using Shouldly;
using System.Net;

namespace Moneyball.Tests.ExternalAPIs.HttpClients;

// ---------------------------------------------------------------------------
// OddsDataService — retry on transient errors
//
// GetOddsAsync returns an empty OddsResponse on failure rather than
// throwing, so we verify resilience through call counts and return values.
// ---------------------------------------------------------------------------

public class OddsDataServiceRetryTests
{
    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task GetOdds_RetriesOn_TransientStatusCodes(HttpStatusCode statusCode)
    {
        var callCount = 0;
        var mock = new MockHttpMessageHandler();

        mock.When("*").Respond(() =>
        {
            callCount++;
            return Task.FromResult(callCount < 3
                ? new HttpResponseMessage(statusCode)
                : new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StringContent("[]") });
        });

        var service = ServiceProviderFactory
            .Build(mock, new FakeTimeProvider())
            .GetRequiredService<IOddsDataService>();

        await service.GetOddsAsync("basketball_nba");

        callCount.ShouldBe(3, "should retry twice before succeeding on the third attempt");
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    public async Task GetOdds_DoesNotRetry_OnClientErrors(HttpStatusCode statusCode)
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
            .GetRequiredService<IOddsDataService>();

        await service.GetOddsAsync("basketball_nba");

        callCount.ShouldBe(1, "client errors must never trigger a retry");
    }

    [Fact]
    public async Task GetOdds_ExhaustsAllRetries_ReturnsEmptyOddsResponse()
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
            .GetRequiredService<IOddsDataService>();

        var result = await service.GetOddsAsync("basketball_nba");

        callCount.ShouldBe(4, "1 initial attempt + 3 retries");
        result.Should().NotBeNull();
        result.Data.Should().BeEmpty("no successful response means no odds returned");
    }

    [Fact]
    public async Task GetOdds_SucceedsOnFinalRetry_ReturnsData()
    {
        var callCount = 0;
        var mock = new MockHttpMessageHandler();

        mock.When("*").Respond(() =>
        {
            callCount++;
            return Task.FromResult(callCount < 4
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StringContent("[]") });
        });

        var service = ServiceProviderFactory
            .Build(mock, new FakeTimeProvider())
            .GetRequiredService<IOddsDataService>();

        var result = await service.GetOddsAsync("basketball_nba");

        callCount.ShouldBe(4, "should succeed on the 4th attempt (3rd retry)");
        result.Should().NotBeNull();
    }
}