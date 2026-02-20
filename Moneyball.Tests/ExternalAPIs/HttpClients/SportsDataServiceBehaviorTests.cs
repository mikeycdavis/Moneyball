using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moneyball.Infrastructure.ExternalAPIs.SportsRadar;
using Moneyball.Tests.ExternalAPIs.HttpClients.TestInfrastructure;
using RichardSzalay.MockHttp;
using Shouldly;
using System.Net;

namespace Moneyball.Tests.ExternalAPIs.HttpClients;

// ---------------------------------------------------------------------------
// SportsDataService — typed client behaviour
// Uses DirectFactory to bypass the resilience pipeline so these tests
// are purely about what the service does with request/response shapes.
// ---------------------------------------------------------------------------

public class SportsDataServiceBehaviorTests
{
    [Fact]
    public async Task GetNBASchedule_BuildsCorrectUrl_ForSingleDay()
    {
        var capturedUri = (Uri?)null;
        var mock = new MockHttpMessageHandler();

        mock.When("*").Respond(req =>
        {
            capturedUri = req.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent("{}") };
        });

        var (service, config) = DirectFactory.BuildSportsDataService(mock);
        var baseUrl = config["SportsData:BaseUrl"];
        var apiKey = config["SportsData:ApiKey"];
        var testDate = new DateTime(2024, 11, 15);

        await service.GetNBAScheduleAsync(testDate, testDate);

        capturedUri.ShouldNotBeNull();
        capturedUri!.ToString().Should()
            .Be($"{baseUrl}/games/2024/11/15/schedule.json?api_key={apiKey}",
                "URL must follow SportRadar's documented format");
    }

    [Fact]
    public async Task GetNBASchedule_IteratesOncePerDay_ForDateRange()
    {
        var capturedUris = new List<Uri?>();
        var mock = new MockHttpMessageHandler();

        mock.When("*").Respond(req =>
        {
            capturedUris.Add(req.RequestUri);
            return new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent("{}") };
        });

        var (service, _) = DirectFactory.BuildSportsDataService(mock);
        var start = new DateTime(2024, 11, 1);
        var end = new DateTime(2024, 11, 3); // 3 days

        await service.GetNBAScheduleAsync(start, end);

        capturedUris.Should().HaveCount(3, "one request per day in the date range");
        capturedUris.Select(u => u!.ToString())
            .Should().Contain(s => s.Contains("2024/11/01"))
            .And.Contain(s => s.Contains("2024/11/02"))
            .And.Contain(s => s.Contains("2024/11/03"));
    }

    [Fact]
    public async Task GetNBASchedule_ReturnsEmptyList_WhenResponseIsEmpty()
    {
        var mock = new MockHttpMessageHandler();
        mock.When("*").Respond(HttpStatusCode.OK,
            new StringContent(string.Empty));

        var (service, _) = DirectFactory.BuildSportsDataService(mock);

        var result = await service.GetNBAScheduleAsync(
            new DateTime(2024, 11, 1), new DateTime(2024, 11, 1));

        result.Should().BeEmpty("empty response body should yield no games");
    }

    [Fact]
    public async Task GetNBASchedule_HandlesNotFound_Gracefully()
    {
        // 404 means no games scheduled that day — not an error worth retrying.
        // Service should swallow it and return empty.
        var mock = new MockHttpMessageHandler();
        mock.When("*").Respond(HttpStatusCode.NotFound);

        var (service, _) = DirectFactory.BuildSportsDataService(mock);

        var act = () => service.GetNBAScheduleAsync(
            new DateTime(2024, 11, 1), new DateTime(2024, 11, 1));

        await act.Should().NotThrowAsync(
            "404 represents 'no games today' and should be handled gracefully");
    }

    [Fact]
    public async Task GetNBAGameStatistics_BuildsCorrectUrl()
    {
        var capturedUri = (Uri?)null;
        var mock = new MockHttpMessageHandler();

        mock.When("*").Respond(req =>
        {
            capturedUri = req.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent("{}") };
        });

        var (service, config) = DirectFactory.BuildSportsDataService(mock);
        var baseUrl = config["SportsData:BaseUrl"];
        var apiKey = config["SportsData:ApiKey"];

        await service.GetNBAGameStatisticsAsync("game-abc-123");

        capturedUri.ShouldNotBeNull();
        capturedUri!.ToString().Should()
            .Be($"{baseUrl}/games/game-abc-123/statistics.json?api_key={apiKey}");
    }

    [Fact]
    public async Task GetNBAGameStatistics_ReturnsNull_OnNonSuccessResponse()
    {
        var mock = new MockHttpMessageHandler();
        mock.When("*").Respond(HttpStatusCode.NotFound);

        var (service, _) = DirectFactory.BuildSportsDataService(mock);
        var result = await service.GetNBAGameStatisticsAsync("unknown-game");

        result.Should().BeNull("service returns null when the game is not found");
    }

    [Fact]
    public async Task GetNBATeams_BuildsCorrectUrl()
    {
        var capturedUri = (Uri?)null;
        var mock = new MockHttpMessageHandler();

        mock.When("*").Respond(req =>
        {
            capturedUri = req.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent("{}") };
        });

        var (service, config) = DirectFactory.BuildSportsDataService(mock);
        var baseUrl = config["SportsData:BaseUrl"];
        var apiKey = config["SportsData:ApiKey"];

        await service.GetNBATeamsAsync();

        capturedUri.ShouldNotBeNull();
        capturedUri!.ToString().Should()
            .Be($"{baseUrl}/league/hierarchy.json?api_key={apiKey}");
    }

    [Fact]
    public async Task GetNBATeams_ThrowsOnNonSuccess()
    {
        // GetNBATeamsAsync calls EnsureSuccessStatusCode — callers must handle this.
        var mock = new MockHttpMessageHandler();
        mock.When("*").Respond(HttpStatusCode.InternalServerError);

        var (service, _) = DirectFactory.BuildSportsDataService(mock);

        var act = () => service.GetNBATeamsAsync();
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task SportsDataService_ThrowsOnMissingApiKey()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SportsData:BaseUrl"] = "https://api.sportradar.com/nba/trial/v8/en"
                // ApiKey deliberately omitted
            })
            .Build();

        await FluentActions.Awaiting(() => Task.FromResult(
                new SportsDataService(
                    new MockHttpMessageHandler().ToHttpClient(),
                    config,
                    NullLogger<SportsDataService>.Instance)))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*SportsData:ApiKey*");
    }
}