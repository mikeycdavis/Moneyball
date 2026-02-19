using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moneyball.Infrastructure.ExternalAPIs.Odds;
using Moneyball.Tests.HttpClients.TestInfrastructure;
using RichardSzalay.MockHttp;
using Shouldly;
using System.Net;

namespace Moneyball.Tests.HttpClients;

// ---------------------------------------------------------------------------
// OddsDataService — typed client behavior
// ---------------------------------------------------------------------------

public class OddsDataServiceBehaviorTests
{
    [Fact]
    public async Task GetOdds_BuildsCorrectUrl_WithDefaults()
    {
        var capturedUri = (Uri?)null;
        var mock = new MockHttpMessageHandler();

        mock.When("*").Respond(req =>
        {
            capturedUri = req.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent("[]") };
        });

        var (service, config) = DirectFactory.BuildOddsDataService(mock);
        var baseUrl = config["OddsAPI:BaseUrl"];
        var apiKey = config["OddsAPI:ApiKey"];

        await service.GetOddsAsync("basketball_nba");

        capturedUri.ShouldNotBeNull();
        capturedUri!.ToString().Should()
            .StartWith($"{baseUrl}/sports/basketball_nba/odds")
            .And.Contain($"apiKey={apiKey}")
            .And.Contain("regions=us")
            .And.Contain("markets=h2h")
            .And.Contain("oddsFormat=american");
    }

    [Fact]
    public async Task GetOdds_BuildsCorrectUrl_WithCustomRegionAndMarket()
    {
        var capturedUri = (Uri?)null;
        var mock = new MockHttpMessageHandler();

        mock.When("*").Respond(req =>
        {
            capturedUri = req.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent("[]") };
        });

        var (service, _) = DirectFactory.BuildOddsDataService(mock);

        await service.GetOddsAsync("americanfootball_nfl", region: "uk", market: "spreads");

        capturedUri!.ToString().Should()
            .Contain("regions=uk")
            .And.Contain("markets=spreads");
    }

    [Fact]
    public async Task GetOdds_ReturnsEmptyResponse_WhenApiReturnsEmptyBody()
    {
        var mock = new MockHttpMessageHandler();
        mock.When("*").Respond(HttpStatusCode.OK, new StringContent(string.Empty));

        var (service, _) = DirectFactory.BuildOddsDataService(mock);
        var result = await service.GetOddsAsync("basketball_nba");

        result.Should().NotBeNull();
        result.Data.Should().BeEmpty("empty body should yield an empty OddsResponse");
    }

    [Fact]
    public async Task GetOdds_ReturnsEmptyResponse_OnNonSuccessStatusCode()
    {
        var mock = new MockHttpMessageHandler();
        mock.When("*").Respond(HttpStatusCode.Forbidden);

        var (service, _) = DirectFactory.BuildOddsDataService(mock);
        var result = await service.GetOddsAsync("basketball_nba");

        result.Should().NotBeNull("service returns an empty OddsResponse rather than throwing");
        result.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task GetOdds_ReadsRemainingRequestsHeader_WithoutThrowing()
    {
        var mock = new MockHttpMessageHandler();

        mock.When("*").Respond(() =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent("[]") };
            response.Headers.Add("x-requests-remaining", "498");
            return Task.FromResult(response);
        });

        var (service, _) = DirectFactory.BuildOddsDataService(mock);

        // The service logs the header value — verify this doesn't throw
        await FluentActions.Awaiting(() => service.GetOddsAsync("basketball_nba"))
            .Should().NotThrowAsync("remaining-requests header parsing must be resilient");
    }

    [Fact]
    public async Task OddsDataService_ThrowsOnMissingApiKey()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OddsAPI:BaseUrl"] = "https://api.the-odds-api.com/v4"
                // ApiKey deliberately omitted
            })
            .Build();

        await FluentActions.Awaiting(() => Task.FromResult(
                new OddsDataService(
                    new MockHttpMessageHandler().ToHttpClient(),
                    config,
                    NullLogger<OddsDataService>.Instance)))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*OddsAPI:ApiKey*");
    }
}