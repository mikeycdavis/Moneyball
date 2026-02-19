using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moneyball.Infrastructure.ExternalAPIs.SportsRadar;
using Moneyball.Service.ExternalAPIs.DTO;
using Shouldly;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text.Json;

namespace Moneyball.Tests.ExternalAPIs.SportsRadar;

// ──────────────────────────────────────────────────────────────────────────────
// Helpers
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Builds a SportsDataService with a fully controlled HttpMessageHandler so we
/// can simulate any HTTP response without hitting the real network.
/// </summary>
internal static class SportsDataServiceFactory
{
    public const string FakeApiKey = "TEST_API_KEY";
    public const string FakeBaseUrl = "https://fake.sportradar.com/nba/v1/en";

    public static (SportsDataService Service, Mock<HttpMessageHandler> HandlerMock)
        Create(params (HttpStatusCode Status, object? Body)[] responses)
    {
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);

        // Queue up each response in order; after the last one repeat the last response
        var queue = new Queue<(HttpStatusCode, object?)>(responses);

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                var (status, body) = queue.Count > 1 ? queue.Dequeue() : queue.Peek();
                return BuildResponse(status, body);
            });

        var httpClient = new HttpClient(handlerMock.Object);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SportsData:ApiKey"] = FakeApiKey,
                ["SportsData:BaseUrl"] = FakeBaseUrl,
            })
            .Build();

        var logger = Mock.Of<ILogger<SportsDataService>>();

        return (new SportsDataService(httpClient, config, logger), handlerMock);
    }

    private static HttpResponseMessage BuildResponse(HttpStatusCode status, object? body)
    {
        if (body is null)
            return new HttpResponseMessage(status);

        var json = JsonSerializer.Serialize(body);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        return new HttpResponseMessage(status) { Content = content };
    }
}

// ──────────────────────────────────────────────────────────────────────────────
// Constructor / configuration tests
// ──────────────────────────────────────────────────────────────────────────────

public class SportsDataService_ConstructorTests
{
    [Fact]
        public void Constructor_MissingApiKey_ThrowsInvalidOperationException()
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>()) // no key
                .Build();

            var ex = Should.Throw<InvalidOperationException>(() =>
                new SportsDataService(
                    new HttpClient(),
                    config,
                    Mock.Of<ILogger<SportsDataService>>()));

            ex.Message.ShouldContain("SportsData:ApiKey");
        }

    [Fact]
    public void Constructor_MissingBaseUrl_UsesDefaultUrl()
    {
        // Should not throw — a hardcoded default exists in the implementation
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SportsData:ApiKey"] = "key",
                // BaseUrl intentionally omitted
            })
            .Build();

        var ex = Record.Exception(() =>
            new SportsDataService(
                new HttpClient(),
                config,
                Mock.Of<ILogger<SportsDataService>>()));

        ex.ShouldBeNull();
    }
}

// ──────────────────────────────────────────────────────────────────────────────
// GetNBAScheduleAsync tests
// ──────────────────────────────────────────────────────────────────────────────

public class SportsDataService_GetNBAScheduleAsyncTests
{
    // ── happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task SingleDay_SuccessResponse_ReturnsGames()
    {
        var expectedGames = new List<NBAGame>
        {
            new() { Id = "game-1", Status = "closed" },
            new() { Id = "game-2", Status = "closed" },
        };

        var body = new NBAScheduleResponse { Games = expectedGames };

        var (service, _) = SportsDataServiceFactory.Create((HttpStatusCode.OK, body));

        // Single day so only one HTTP call is made
        var date = new DateTime(2024, 11, 1);
        var result = (await service.GetNBAScheduleAsync(date, date)).ToList();

        result.Count.ShouldBe(2);
        result[0].Id.ShouldBe("game-1");
        result[1].Id.ShouldBe("game-2");
    }

    [Fact]
    public async Task MultipleDays_AggregatesGamesAcrossDays()
    {
        // Day 1 returns 2 games, Day 2 returns 1 game
        var body1 = new NBAScheduleResponse
        {
            Games = [new NBAGame { Id = "g1" }, new NBAGame { Id = "g2" }]
        };
        var body2 = new NBAScheduleResponse
        {
            Games = [new NBAGame { Id = "g3" }]
        };

        var (service, _) = SportsDataServiceFactory.Create(
            (HttpStatusCode.OK, body1),
            (HttpStatusCode.OK, body2));

        var start = new DateTime(2024, 11, 1);
        var end = new DateTime(2024, 11, 2);
        var result = (await service.GetNBAScheduleAsync(start, end)).ToList();

        result.Count.ShouldBe(3);
        result.ShouldContain(g => g.Id == "g1");
        result.ShouldContain(g => g.Id == "g3");
    }

    [Fact]
    public async Task SuccessResponse_NullGamesProperty_ReturnsEmptyList()
    {
        var body = new NBAScheduleResponse { Games = null! };

        var (service, _) = SportsDataServiceFactory.Create((HttpStatusCode.OK, body));

        var date = new DateTime(2024, 11, 1);
        var result = await service.GetNBAScheduleAsync(date, date);

        result.ShouldBeEmpty();
    }

    // ── URL construction ──────────────────────────────────────────────────────

    [Fact]
    public async Task RequestUrl_ContainsDateSegmentsAndApiKey()
    {
        var capturedUrls = new List<string?>();

        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage req, CancellationToken _) =>
            {
                capturedUrls.Add(req.RequestUri?.ToString());
                var body = new NBAScheduleResponse { Games = [] };
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(body),
                        System.Text.Encoding.UTF8,
                        "application/json")
                };
            });

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SportsData:ApiKey"] = "MY_KEY",
                ["SportsData:BaseUrl"] = "https://fake.api.com",
            })
            .Build();

        var service = new SportsDataService(
            new HttpClient(handlerMock.Object),
            config,
            Mock.Of<ILogger<SportsDataService>>());

        var date = new DateTime(2024, 3, 5);
        await service.GetNBAScheduleAsync(date, date);

        capturedUrls.ShouldHaveSingleItem();
        var url = capturedUrls[0]!;
        url.ShouldContain("/games/2024/03/05/schedule.json");
        url.ShouldContain("api_key=MY_KEY");
    }

    // ── non-success responses ─────────────────────────────────────────────────

    [Fact]
    public async Task NotFoundResponse_SkipsDay_ReturnsEmptyList()
    {
        var (service, _) = SportsDataServiceFactory.Create((HttpStatusCode.NotFound, null));

        var date = new DateTime(2024, 11, 1);
        var result = await service.GetNBAScheduleAsync(date, date);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task ServerErrorResponse_SkipsDay_ContinuesAndReturnsEmpty()
    {
        // 500 on day 1 should be logged/skipped, day 2 returns a game
        var body2 = new NBAScheduleResponse { Games = [new NBAGame { Id = "g2" }] };

        var (service, _) = SportsDataServiceFactory.Create(
            (HttpStatusCode.InternalServerError, null),
            (HttpStatusCode.OK, body2));

        var start = new DateTime(2024, 11, 1);
        var end = new DateTime(2024, 11, 2);
        var result = (await service.GetNBAScheduleAsync(start, end)).ToList();

        result.ShouldHaveSingleItem();
        result[0].Id.ShouldBe("g2");
    }

    // ── exception handling ────────────────────────────────────────────────────

    [Fact]
    public async Task HttpClientThrows_ExceptionPropagates()
    {
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network failure"));

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SportsData:ApiKey"] = "key",
            })
            .Build();

        var service = new SportsDataService(
            new HttpClient(handlerMock.Object),
            config,
            Mock.Of<ILogger<SportsDataService>>());

        var date = new DateTime(2024, 11, 1);
        await Should.ThrowAsync<HttpRequestException>(
            () => service.GetNBAScheduleAsync(date, date));
    }

    // ── edge cases ────────────────────────────────────────────────────────────

    [Fact]
    public async Task StartDateEqualsEndDate_MakesExactlyOneHttpRequest()
    {
        var requestCount = 0;
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                requestCount++;
                var body = new NBAScheduleResponse { Games = [] };
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(body),
                        System.Text.Encoding.UTF8,
                        "application/json")
                };
            });

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SportsData:ApiKey"] = "key",
            })
            .Build();

        var service = new SportsDataService(
            new HttpClient(handlerMock.Object),
            config,
            Mock.Of<ILogger<SportsDataService>>());

        var date = new DateTime(2024, 11, 1);
        await service.GetNBAScheduleAsync(date, date);

        requestCount.ShouldBe(1);
    }
}

// ──────────────────────────────────────────────────────────────────────────────
// GetNBAGameStatisticsAsync tests
// ──────────────────────────────────────────────────────────────────────────────

public class SportsDataService_GetNBAGameStatisticsAsyncTests
{
    // ── happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ValidGameId_SuccessResponse_ReturnsStatistics()
    {
        var expected = new NBAGameStatistics { Id = "game-abc" };

        var (service, _) = SportsDataServiceFactory.Create((HttpStatusCode.OK, expected));

        var result = await service.GetNBAGameStatisticsAsync("game-abc");

        result.ShouldNotBeNull();
        result.Id.ShouldBe("game-abc");
    }

    // ── URL construction ──────────────────────────────────────────────────────

    [Fact]
    public async Task RequestUrl_ContainsGameIdAndApiKey()
    {
        string? capturedUrl = null;

        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage req, CancellationToken _) =>
            {
                capturedUrl = req.RequestUri?.ToString();
                var stats = new NBAGameStatistics { Id = "gid-999" };
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(stats),
                        System.Text.Encoding.UTF8,
                        "application/json")
                };
            });

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SportsData:ApiKey"] = "SECRET",
                ["SportsData:BaseUrl"] = "https://base.url",
            })
            .Build();

        var service = new SportsDataService(
            new HttpClient(handlerMock.Object),
            config,
            Mock.Of<ILogger<SportsDataService>>());

        await service.GetNBAGameStatisticsAsync("gid-999");

        capturedUrl.ShouldNotBeNull();
        capturedUrl.ShouldContain("/games/gid-999/statistics.json");
        capturedUrl.ShouldContain("api_key=SECRET");
    }

    // ── non-success responses ─────────────────────────────────────────────────

    [Fact]
    public async Task NotFoundResponse_ReturnsNull()
    {
        var (service, _) = SportsDataServiceFactory.Create((HttpStatusCode.NotFound, null));

        var result = await service.GetNBAGameStatisticsAsync("missing-game");

        result.ShouldBeNull();
    }

    [Fact]
    public async Task UnauthorizedResponse_ReturnsNull()
    {
        var (service, _) = SportsDataServiceFactory.Create((HttpStatusCode.Unauthorized, null));

        var result = await service.GetNBAGameStatisticsAsync("any-id");

        result.ShouldBeNull();
    }

    [Fact]
    public async Task ServerErrorResponse_ReturnsNull()
    {
        var (service, _) = SportsDataServiceFactory.Create(
            (HttpStatusCode.InternalServerError, null));

        var result = await service.GetNBAGameStatisticsAsync("game-123");

        result.ShouldBeNull();
    }

    // ── exception handling ────────────────────────────────────────────────────

    [Fact]
    public async Task HttpClientThrows_ExceptionPropagates()
    {
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Timeout"));

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SportsData:ApiKey"] = "key",
            })
            .Build();

        var service = new SportsDataService(
            new HttpClient(handlerMock.Object),
            config,
            Mock.Of<ILogger<SportsDataService>>());

        await Should.ThrowAsync<HttpRequestException>(
            () => service.GetNBAGameStatisticsAsync("any-id"));
    }
}

// ──────────────────────────────────────────────────────────────────────────────
// GetNBATeamsAsync tests
// ──────────────────────────────────────────────────────────────────────────────

public class SportsDataService_GetNBATeamsAsyncTests
{
    // ── happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task SuccessResponse_ReturnsAllTeamsFromAllConferencesDivisions()
    {
        var hierarchy = new NBAHierarchyResponse
        {
            Conferences =
            [
                new NBAConference
                {
                    Id   = "conf-east",
                    Name = "Eastern",
                    Divisions =
                    [
                        new NBADivision
                        {
                            Id    = "div-atlantic",
                            Name  = "Atlantic",
                            Teams = [new NBATeamInfo { Id = "t1", Name = "TeamA" }]
                        },
                        new NBADivision
                        {
                            Id    = "div-central",
                            Name  = "Central",
                            Teams = [new NBATeamInfo { Id = "t2", Name = "TeamB" }]
                        }
                    ]
                },
                new NBAConference
                {
                    Id   = "conf-west",
                    Name = "Western",
                    Divisions =
                    [
                        new NBADivision
                        {
                            Id    = "div-pacific",
                            Name  = "Pacific",
                            Teams =
                            [
                                new NBATeamInfo { Id = "t3", Name = "TeamC" },
                                new NBATeamInfo { Id = "t4", Name = "TeamD" },
                            ]
                        }
                    ]
                }
            ]
        };

        var (service, _) = SportsDataServiceFactory.Create((HttpStatusCode.OK, hierarchy));

        var result = (await service.GetNBATeamsAsync()).ToList();

        result.Count.ShouldBe(4);
        result.ShouldContain(t => t.Id == "t1");
        result.ShouldContain(t => t.Id == "t4");
    }

    [Fact]
    public async Task SuccessResponse_EmptyConferences_ReturnsEmptyList()
    {
        var hierarchy = new NBAHierarchyResponse { Conferences = [] };

        var (service, _) = SportsDataServiceFactory.Create((HttpStatusCode.OK, hierarchy));

        var result = await service.GetNBATeamsAsync();

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task SuccessResponse_NullConferences_ReturnsEmptyList()
    {
        var hierarchy = new NBAHierarchyResponse { Conferences = null! };

        var (service, _) = SportsDataServiceFactory.Create((HttpStatusCode.OK, hierarchy));

        var result = await service.GetNBATeamsAsync();

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task SuccessResponse_DivisionWithNullTeams_SkipsDivision()
    {
        var hierarchy = new NBAHierarchyResponse
        {
            Conferences =
            [
                new NBAConference
                {
                    Divisions =
                    [
                        new NBADivision { Teams = null! },                        // null teams
                        new NBADivision { Teams = [new NBATeamInfo { Id = "t1" }] }
                    ]
                }
            ]
        };

        var (service, _) = SportsDataServiceFactory.Create((HttpStatusCode.OK, hierarchy));

        var result = (await service.GetNBATeamsAsync()).ToList();

        result.ShouldHaveSingleItem();
        result[0].Id.ShouldBe("t1");
    }

    [Fact]
    public async Task SuccessResponse_ConferenceWithNullDivisions_SkipsConference()
    {
        var hierarchy = new NBAHierarchyResponse
        {
            Conferences =
            [
                new NBAConference { Divisions = null! },
                new NBAConference
                {
                    Divisions =
                    [
                        new NBADivision { Teams = [new NBATeamInfo { Id = "t5" }] }
                    ]
                }
            ]
        };

        var (service, _) = SportsDataServiceFactory.Create((HttpStatusCode.OK, hierarchy));

        var result = (await service.GetNBATeamsAsync()).ToList();

        result.ShouldHaveSingleItem();
        result[0].Id.ShouldBe("t5");
    }

    // ── URL construction ──────────────────────────────────────────────────────

    [Fact]
    public async Task RequestUrl_ContainsHierarchyEndpointAndApiKey()
    {
        string? capturedUrl = null;

        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage req, CancellationToken _) =>
            {
                capturedUrl = req.RequestUri?.ToString();
                var hierarchy = new NBAHierarchyResponse { Conferences = [] };
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(hierarchy),
                        System.Text.Encoding.UTF8,
                        "application/json")
                };
            });

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SportsData:ApiKey"] = "MYKEY",
                ["SportsData:BaseUrl"] = "https://base.url",
            })
            .Build();

        var service = new SportsDataService(
            new HttpClient(handlerMock.Object),
            config,
            Mock.Of<ILogger<SportsDataService>>());

        await service.GetNBATeamsAsync();

        capturedUrl.ShouldNotBeNull();
        capturedUrl.ShouldContain("/league/hierarchy.json");
        capturedUrl.ShouldContain("api_key=MYKEY");
    }

    // ── non-success / exception handling ─────────────────────────────────────

    [Fact]
    public async Task NonSuccessResponse_ThrowsHttpRequestException()
    {
        // GetNBATeamsAsync calls EnsureSuccessStatusCode(), so non-2xx must throw
        var (service, _) = SportsDataServiceFactory.Create(
            (HttpStatusCode.InternalServerError, null));

        await Should.ThrowAsync<HttpRequestException>(
            () => service.GetNBATeamsAsync());
    }

    [Fact]
    public async Task HttpClientThrows_ExceptionPropagates()
    {
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("Timed out"));

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SportsData:ApiKey"] = "key",
            })
            .Build();

        var service = new SportsDataService(
            new HttpClient(handlerMock.Object),
            config,
            Mock.Of<ILogger<SportsDataService>>());

        await Should.ThrowAsync<TaskCanceledException>(
            () => service.GetNBATeamsAsync());
    }
}