using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moneyball.Core.DTOs.ExternalAPIs.SportsRadar;
using Moneyball.Infrastructure.ExternalAPIs.SportsRadar;
using Moq;
using Moq.Protected;
using Shouldly;
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

/// <summary>
/// Unit tests for SportsDataService.GetNBAOddsAsync method.
/// Tests cover successful responses, error handling, null scenarios, and edge cases.
/// Uses Moq for HttpClient mocking and FluentAssertions for readable assertions.
/// </summary>
public class SportsDataService_GetNBAOddsAsyncTests
{
    private readonly Mock<HttpMessageHandler> _mockHttpHandler;
    private readonly Mock<ILogger<SportsDataService>> _mockLogger;
    private readonly SportsDataService _service;

    /// <summary>
    /// Test fixture setup - initializes mocks and creates service instance.
    /// Runs before each test method.
    /// </summary>
    public SportsDataService_GetNBAOddsAsyncTests()
    {
        // Create mock HTTP message handler to intercept HTTP requests
        _mockHttpHandler = new Mock<HttpMessageHandler>();

        // Create HttpClient with mocked handler
        var httpClient = new HttpClient(_mockHttpHandler.Object);

        // Mock configuration to provide test API keys and base URLs
        var mockConfiguration = new Mock<IConfiguration>();
        _mockLogger = new Mock<ILogger<SportsDataService>>();

        // Setup test configuration values
        mockConfiguration.Setup(c => c["SportsData:ApiKey"]).Returns("test-api-key");
        mockConfiguration.Setup(c => c["SportsData:BaseUrl"]).Returns("https://api.test.com");

        // Create service instance with mocked dependencies
        _service = new SportsDataService(httpClient, mockConfiguration.Object, _mockLogger.Object);
    }

    /// <summary>
    /// Tests successful API response with complete odds data.
    /// Verifies that the method correctly deserializes and returns odds from multiple bookmakers.
    /// </summary>
    [Fact]
    public async Task GetNBAOddsAsync_SuccessfulResponse_ReturnsOddsData()
    {
        // Arrange - Create test odds data with multiple markets and bookmakers
        var testGameId = "sr:match:12345678";
        var testOdds = CreateTestNBAOddsResponse(testGameId);

        // Setup HTTP handler to return successful response with test data
        SetupHttpResponse(HttpStatusCode.OK, testOdds);

        // Act - Call the method under test
        var result = await _service.GetNBAOddsAsync(testGameId);

        // Assert - Verify the result matches expected data
        result.Should().NotBeNull("the API returned valid odds data");
        result.Sport_Event_Id.Should().Be(testGameId, "the response should match the requested game ID");
        result.Markets.Should().HaveCount(3, "test data includes moneyline, spread, and totals markets");

        // Verify moneyline market exists and has bookmakers
        result.Markets.Should().Contain(m => m.Name == "1x2", "moneyline market should be present");
        var moneylineMarket = result.Markets.First(m => m.Name == "1x2");
        moneylineMarket.Bookmakers.Should().NotBeEmpty("moneyline should have bookmaker odds");
    }

    /// <summary>
    /// Tests that the method returns null when API returns 404 Not Found.
    /// This is expected when odds are not yet available for a scheduled game.
    /// </summary>
    [Fact]
    public async Task GetNBAOddsAsync_NotFoundStatus_ReturnsNull()
    {
        // Arrange - Setup HTTP response with 404 status
        SetupHttpResponse(HttpStatusCode.NotFound, null);

        // Act
        var result = await _service.GetNBAOddsAsync("sr:match:99999999");

        // Assert - Should return null rather than throwing exception
        result.Should().BeNull("404 indicates odds are not available for this game");

        // Verify warning was logged
        VerifyLogWarning("Failed to fetch odds");
    }

    /// <summary>
    /// Tests that the method returns null when API returns empty content.
    /// Verifies graceful handling of malformed or empty API responses.
    /// </summary>
    [Fact]
    public async Task GetNBAOddsAsync_EmptyResponse_ReturnsNull()
    {
        // Arrange - Setup successful status but empty content
        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("")  // Empty string content
            });

        // Act
        var result = await _service.GetNBAOddsAsync("sr:match:12345678");

        // Assert
        result.Should().BeNull("empty content should be handled gracefully");

        // Verify warning was logged about empty content
        VerifyLogWarning("API returned empty content");
    }

    /// <summary>
    /// Tests that the method returns null when deserialized odds data has no markets.
    /// Verifies handling of valid JSON with empty markets array.
    /// </summary>
    [Fact]
    public async Task GetNBAOddsAsync_NoMarketsInResponse_ReturnsNull()
    {
        // Arrange - Create odds response with empty markets
        var emptyOdds = new NBAOddsResponse
        {
            Sport_Event_Id = "sr:match:12345678",
            Markets = [] // Empty markets list
        };

        SetupHttpResponse(HttpStatusCode.OK, emptyOdds);

        // Act
        var result = await _service.GetNBAOddsAsync("sr:match:12345678");

        // Assert
        result.Should().BeNull("odds response with no markets should return null");

        // Verify info log about no odds data
        VerifyLogInformation("No odds data returned");
    }

    /// <summary>
    /// Tests that HTTP request exceptions are properly propagated.
    /// Verifies that network errors are logged and re-thrown.
    /// </summary>
    [Fact]
    public async Task GetNBAOddsAsync_HttpRequestException_ThrowsAndLogsError()
    {
        // Arrange - Setup handler to throw HTTP request exception
        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act & Assert - Verify exception is thrown
        var act = async () => await _service.GetNBAOddsAsync("sr:match:12345678");

        await act.Should().ThrowAsync<HttpRequestException>()
            .WithMessage("Network error", "the original exception should be preserved");

        // Verify error was logged
        VerifyLogError("HTTP error fetching odds");
    }

    /// <summary>
    /// Tests that general exceptions are properly handled and logged.
    /// Verifies that unexpected errors are logged with context.
    /// </summary>
    [Fact]
    public async Task GetNBAOddsAsync_GeneralException_ThrowsAndLogsError()
    {
        // Arrange - Setup handler to throw general exception
        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Unexpected error"));

        // Act & Assert
        await FluentActions.Awaiting(async () => await _service.GetNBAOddsAsync("sr:match:12345678"))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Unexpected error");

        // Verify error was logged with game ID context
        VerifyLogError("Error fetching odds for game");
    }

    /// <summary>
    /// Tests that the method constructs the correct API URL.
    /// Verifies endpoint format, game ID placement, and API key inclusion.
    /// </summary>
    [Fact]
    public async Task GetNBAOddsAsync_ConstructsCorrectUrl()
    {
        // Arrange
        var testGameId = "sr:match:12345678";
        var testOdds = CreateTestNBAOddsResponse(testGameId);
        HttpRequestMessage? capturedRequest = null;

        // Capture the actual HTTP request made
        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(CreateHttpResponse(HttpStatusCode.OK, testOdds));

        // Act
        await _service.GetNBAOddsAsync(testGameId);

        // Assert - Verify URL structure
        capturedRequest.Should().NotBeNull("HTTP request should have been made");
        capturedRequest!.RequestUri.Should().NotBeNull("request URI should be set");

        var url = capturedRequest.RequestUri!.ToString();
        url.Should().Contain("/sport_events/", "should use sport_events endpoint");
        url.Should().Contain(testGameId, "should include the game ID in the path");
        url.Should().Contain("/markets.json", "should request markets endpoint");
        url.Should().Contain("api_key=test-api-key", "should include API key parameter");
    }

    /// <summary>
    /// Tests that the method correctly parses all market types.
    /// Verifies moneyline, spread, and totals markets are all present in response.
    /// </summary>
    [Fact]
    public async Task GetNBAOddsAsync_ParsesAllMarketTypes_Correctly()
    {
        // Arrange - Create comprehensive odds data with all market types
        var testGameId = "sr:match:12345678";
        var testOdds = CreateTestNBAOddsResponse(testGameId);

        SetupHttpResponse(HttpStatusCode.OK, testOdds);

        // Act
        var result = await _service.GetNBAOddsAsync(testGameId);

        // Assert - Verify all three market types are present
        result.Should().NotBeNull();
        result.Markets.Should().HaveCount(3, "should have moneyline, spread, and totals");

        // Verify each market type exists
        result.Markets.Should().Contain(m => m.Name == "1x2", "moneyline market should exist");
        result.Markets.Should().Contain(m => m.Name == "pointspread", "spread market should exist");
        result.Markets.Should().Contain(m => m.Name == "totals", "totals market should exist");
    }

    /// <summary>
    /// Tests that the method correctly counts distinct bookmakers.
    /// Verifies logging includes accurate bookmaker count from all markets.
    /// </summary>
    [Fact]
    public async Task GetNBAOddsAsync_CountsDistinctBookmakers_Correctly()
    {
        // Arrange - Create odds with multiple bookmakers across markets
        var testGameId = "sr:match:12345678";
        var testOdds = CreateTestNBAOddsResponse(testGameId);

        SetupHttpResponse(HttpStatusCode.OK, testOdds);

        // Act
        var result = await _service.GetNBAOddsAsync(testGameId);

        // Assert - Verify distinct bookmakers are counted
        var distinctBookmakers = result!.Markets
            .SelectMany(m => m.Bookmakers)
            .Select(b => b.Name)
            .Distinct()
            .Count();

        distinctBookmakers.Should().Be(2, "test data includes DraftKings and FanDuel");

        // Verify log message includes correct counts
        VerifyLogInformation($"Retrieved odds for game {testGameId}");
    }

    /// <summary>
    /// Tests that the method handles various HTTP status codes appropriately.
    /// Verifies return null behavior for non-success status codes.
    /// </summary>
    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "400 Bad Request")]
    [InlineData(HttpStatusCode.Unauthorized, "401 Unauthorized")]
    [InlineData(HttpStatusCode.Forbidden, "403 Forbidden")]
    [InlineData(HttpStatusCode.NotFound, "404 Not Found")]
    [InlineData(HttpStatusCode.TooManyRequests, "429 Too Many Requests")]
    [InlineData(HttpStatusCode.InternalServerError, "500 Internal Server Error")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "503 Service Unavailable")]
    public async Task GetNBAOddsAsync_NonSuccessStatusCodes_ReturnsNull(
        HttpStatusCode statusCode,
        string description)
    {
        // Arrange
        SetupHttpResponse(statusCode, null);

        // Act
        var result = await _service.GetNBAOddsAsync("sr:match:12345678");

        // Assert
        result.Should().BeNull($"{description} should return null gracefully");

        // Verify warning was logged with status code
        VerifyLogWarning($"Status: {statusCode}");
    }

    /// <summary>
    /// Tests that the method properly deserializes all outcome fields.
    /// Verifies odds, lines, and probabilities are correctly parsed.
    /// </summary>
    [Fact]
    public async Task GetNBAOddsAsync_DeserializesOutcomeFields_Correctly()
    {
        // Arrange
        var testGameId = "sr:match:12345678";
        var testOdds = CreateTestNBAOddsResponse(testGameId);

        SetupHttpResponse(HttpStatusCode.OK, testOdds);

        // Act
        var result = await _service.GetNBAOddsAsync(testGameId);

        // Assert - Verify spread market outcomes have all fields
        var spreadMarket = result!.Markets.First(m => m.Name == "pointspread");
        var spreadBookmaker = spreadMarket.Bookmakers.First();
        var homeOutcome = spreadBookmaker.Outcomes.First(o => o.Type == "1");

        homeOutcome.Type.Should().Be("1", "home team should have type '1'");
        homeOutcome.Odds.Should().Be(-110, "odds should be deserialized correctly");
        homeOutcome.Line.Should().Be(-3.5m, "spread line should be deserialized correctly");
        homeOutcome.Probability.Should().NotBeNull("probability should be present if provided");
    }

    /// <summary>
    /// Tests that multiple bookmakers in the same market are all included.
    /// Verifies no data is lost when multiple bookmakers offer the same market.
    /// </summary>
    [Fact]
    public async Task GetNBAOddsAsync_MultipleBookmakersInMarket_AllIncluded()
    {
        // Arrange
        var testGameId = "sr:match:12345678";
        var testOdds = CreateTestNBAOddsResponse(testGameId);

        SetupHttpResponse(HttpStatusCode.OK, testOdds);

        // Act
        var result = await _service.GetNBAOddsAsync(testGameId);

        // Assert - Verify moneyline has multiple bookmakers
        var moneylineMarket = result!.Markets.First(m => m.Name == "1x2");
        moneylineMarket.Bookmakers.Should().HaveCount(2,
            "test data includes both DraftKings and FanDuel for moneyline");

        moneylineMarket.Bookmakers.Should().Contain(b => b.Name == "DraftKings");
        moneylineMarket.Bookmakers.Should().Contain(b => b.Name == "FanDuel");
    }

    // ==================== Helper Methods ====================

    /// <summary>
    /// Creates a complete test NBAOddsResponse with all market types and multiple bookmakers.
    /// Used across multiple tests to ensure consistent test data.
    /// </summary>
    private static NBAOddsResponse CreateTestNBAOddsResponse(string gameId)
    {
        return new NBAOddsResponse
        {
            Sport_Event_Id = gameId,
            Markets =
            [
                new NBAMarket
                {
                    Id = "market-1",
                    Name = "1x2",
                    Bookmakers =
                    [
                        new NBABookmaker
                        {
                            Id = "draftkings",
                            Name = "DraftKings",
                            Outcomes =
                            [
                                new NBAOutcome { Type = "1", Odds = -150, Probability = 0.60m }, // Home
                                new NBAOutcome { Type = "2", Odds = 130, Probability = 0.40m }
                            ]
                        },

                        new NBABookmaker
                        {
                            Id = "fanduel",
                            Name = "FanDuel",
                            Outcomes =
                            [
                                new NBAOutcome { Type = "1", Odds = -145, Probability = 0.59m },
                                new NBAOutcome { Type = "2", Odds = 125, Probability = 0.41m }
                            ]
                        }
                    ]
                },

                // Point spread market

                new NBAMarket
                {
                    Id = "market-2",
                    Name = "pointspread",
                    Bookmakers =
                    [
                        new NBABookmaker
                        {
                            Id = "draftkings",
                            Name = "DraftKings",
                            Outcomes =
                            [
                                new NBAOutcome { Type = "1", Odds = -110, Line = -3.5m, Probability = 0.52m },
                                new NBAOutcome { Type = "2", Odds = -110, Line = 3.5m, Probability = 0.48m }
                            ]
                        },

                        new NBABookmaker
                        {
                            Id = "fanduel",
                            Name = "FanDuel",
                            Outcomes =
                            [
                                new NBAOutcome { Type = "1", Odds = -108, Line = -3.5m, Probability = 0.52m },
                                new NBAOutcome { Type = "2", Odds = -112, Line = 3.5m, Probability = 0.48m }
                            ]
                        }
                    ]
                },

                // Totals market (over/under)

                new NBAMarket
                {
                    Id = "market-3",
                    Name = "totals",
                    Bookmakers =
                    [
                        new NBABookmaker
                        {
                            Id = "draftkings",
                            Name = "DraftKings",
                            Outcomes =
                            [
                                new NBAOutcome { Type = "over", Odds = -110, Line = 220.5m, Probability = 0.50m },
                                new NBAOutcome { Type = "under", Odds = -110, Line = 220.5m, Probability = 0.50m }
                            ]
                        },

                        new NBABookmaker
                        {
                            Id = "fanduel",
                            Name = "FanDuel",
                            Outcomes =
                            [
                                new NBAOutcome { Type = "over", Odds = -112, Line = 221.5m, Probability = 0.51m },
                                new NBAOutcome { Type = "under", Odds = -108, Line = 221.5m, Probability = 0.49m }
                            ]
                        }
                    ]
                }
            ]
        };
    }

    /// <summary>
    /// Sets up the mock HTTP handler to return a specific status code and optional data.
    /// Simplifies test arrangement by encapsulating HTTP mock setup.
    /// </summary>
    private void SetupHttpResponse(HttpStatusCode statusCode, NBAOddsResponse? data)
    {
        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(CreateHttpResponse(statusCode, data));
    }

    /// <summary>
    /// Creates an HttpResponseMessage with the specified status and optional JSON data.
    /// Handles JSON serialization of test data.
    /// </summary>
    private static HttpResponseMessage CreateHttpResponse(HttpStatusCode statusCode, NBAOddsResponse? data)
    {
        var response = new HttpResponseMessage(statusCode);

        if (data != null && statusCode == HttpStatusCode.OK)
        {
            var json = JsonSerializer.Serialize(data);
            response.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        }

        return response;
    }

    /// <summary>
    /// Verifies that a warning was logged containing the expected message.
    /// Uses Moq to verify logger interaction.
    /// </summary>
    private void VerifyLogWarning(string expectedMessage)
    {
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    /// <summary>
    /// Verifies that an information log was written containing the expected message.
    /// </summary>
    private void VerifyLogInformation(string expectedMessage)
    {
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    /// <summary>
    /// Verifies that an error was logged containing the expected message.
    /// </summary>
    private void VerifyLogError(string expectedMessage)
    {
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
}
