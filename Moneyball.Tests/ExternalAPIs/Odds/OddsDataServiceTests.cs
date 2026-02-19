using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moneyball.Infrastructure.ExternalAPIs.Odds;
using Moneyball.Service.ExternalAPIs.DTO;
using Shouldly;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Moneyball.Tests.ExternalAPIs.Odds;

// ──────────────────────────────────────────────────────────────────────────────
// Factory helper
// ──────────────────────────────────────────────────────────────────────────────

internal static class OddsDataServiceFactory
{
    public const string FakeApiKey = "TEST_ODDS_KEY";
    public const string FakeBaseUrl = "https://fake.odds-api.com/v4";

    /// <summary>
    /// Creates an OddsDataService whose HTTP calls are intercepted by a mock
    /// handler. Optionally supply custom response headers (e.g. x-requests-remaining).
    /// </summary>
    public static (OddsDataService Service, Mock<HttpMessageHandler> HandlerMock)
        Create(
            HttpStatusCode status,
            object? body,
            Dictionary<string, string>? responseHeaders = null,
            string? apiKey = null,
            string? baseUrl = null)
    {
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                var response = body is null
                    ? new HttpResponseMessage(status)
                    : new HttpResponseMessage(status)
                    {
                        Content = new StringContent(
                            JsonSerializer.Serialize(body),
                            Encoding.UTF8,
                            "application/json")
                    };

                if (responseHeaders != null)
                    foreach (var (key, value) in responseHeaders)
                        response.Headers.TryAddWithoutValidation(key, value);

                return response;
            });

        var config = BuildConfig(apiKey ?? FakeApiKey, baseUrl ?? FakeBaseUrl);
        var logger = Mock.Of<ILogger<OddsDataService>>();

        return (new OddsDataService(new HttpClient(handlerMock.Object), config, logger), handlerMock);
    }

    /// <summary>
    /// Variant that captures every outgoing request URI for URL-assertion tests.
    /// </summary>
    public static (OddsDataService Service, List<string?> CapturedUrls)
        CreateWithUrlCapture(object? body = null, string? apiKey = null, string? baseUrl = null)
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

                var payload = body ?? new List<OddsGame>();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(payload),
                        Encoding.UTF8,
                        "application/json")
                };
            });

        var config = BuildConfig(apiKey ?? FakeApiKey, baseUrl ?? FakeBaseUrl);
        var logger = Mock.Of<ILogger<OddsDataService>>();
        var service = new OddsDataService(new HttpClient(handlerMock.Object), config, logger);

        return (service, capturedUrls);
    }

    private static IConfiguration BuildConfig(string apiKey, string baseUrl) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OddsAPI:ApiKey"] = apiKey,
                ["OddsAPI:BaseUrl"] = baseUrl,
            })
            .Build();
}

// ──────────────────────────────────────────────────────────────────────────────
// Constructor tests
// ──────────────────────────────────────────────────────────────────────────────

public class OddsDataService_ConstructorTests
{
    [Fact]
    public void Constructor_MissingApiKey_ThrowsInvalidOperationException()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var ex = Should.Throw<InvalidOperationException>(() =>
            new OddsDataService(
                new HttpClient(),
                config,
                Mock.Of<ILogger<OddsDataService>>()));

        ex.Message.ShouldContain("OddsAPI:ApiKey");
    }

    [Fact]
    public void Constructor_MissingBaseUrl_UsesDefaultUrlWithoutThrowing()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OddsAPI:ApiKey"] = "any-key",
                // BaseUrl intentionally omitted — implementation has a hardcoded default
            })
            .Build();

        var ex = Record.Exception(() =>
            new OddsDataService(
                new HttpClient(),
                config,
                Mock.Of<ILogger<OddsDataService>>()));

        ex.ShouldBeNull();
    }

    [Fact]
    public void Constructor_AllConfigPresent_DoesNotThrow()
    {
        var ex = Record.Exception(() =>
        {
            var (service, _) = OddsDataServiceFactory.Create(HttpStatusCode.OK, null);
            _ = service; // suppress unused warning
        });

        ex.ShouldBeNull();
    }
}

// ──────────────────────────────────────────────────────────────────────────────
// GetOddsAsync — happy path
// ──────────────────────────────────────────────────────────────────────────────

public class OddsDataService_GetOddsAsync_HappyPathTests
{
    [Fact]
    public async Task SuccessResponse_WithGames_ReturnsPopulatedOddsResponse()
    {
        var games = new List<OddsGame>
        {
            new() { Id = "game-1", HomeTeam = "Lakers",  AwayTeam = "Celtics" },
            new() { Id = "game-2", HomeTeam = "Warriors", AwayTeam = "Nets"   },
        };

        var (service, _) = OddsDataServiceFactory.Create(HttpStatusCode.OK, games);

        var result = await service.GetOddsAsync("basketball_nba");

        result.ShouldNotBeNull();
        result.Data.Count.ShouldBe(2);
        result.Data[0].Id.ShouldBe("game-1");
        result.Data[1].Id.ShouldBe("game-2");
    }

    [Fact]
    public async Task SuccessResponse_EmptyGamesList_ReturnsEmptyData()
    {
        var (service, _) = OddsDataServiceFactory.Create(
            HttpStatusCode.OK, new List<OddsGame>());

        var result = await service.GetOddsAsync("basketball_nba");

        result.ShouldNotBeNull();
        result.Data.ShouldBeEmpty();
    }

    [Fact]
    public async Task SuccessResponse_NullBody_ReturnsEmptyData()
    {
        // ReadFromJsonAsync returns null → implementation falls back to []
        var (service, _) = OddsDataServiceFactory.Create(HttpStatusCode.OK, body: null);

        var result = await service.GetOddsAsync("basketball_nba");

        result.ShouldNotBeNull();
        result.Data.ShouldBeEmpty();
    }

    [Fact]
    public async Task DefaultParameters_AreUsedWhenNotProvided()
    {
        var (service, capturedUrls) = OddsDataServiceFactory.CreateWithUrlCapture();

        await service.GetOddsAsync("basketball_nba");

        var url = capturedUrls.Single()!;
        url.ShouldContain("regions=us");
        url.ShouldContain("markets=h2h");
    }

    [Fact]
    public async Task CustomRegionAndMarket_AreReflectedInUrl()
    {
        var (service, capturedUrls) = OddsDataServiceFactory.CreateWithUrlCapture();

        await service.GetOddsAsync("americanfootball_nfl", region: "uk", market: "spreads");

        var url = capturedUrls.Single()!;
        url.ShouldContain("regions=uk");
        url.ShouldContain("markets=spreads");
    }
}

// ──────────────────────────────────────────────────────────────────────────────
// GetOddsAsync — URL construction
// ──────────────────────────────────────────────────────────────────────────────

public class OddsDataService_GetOddsAsync_UrlTests
{
    [Fact]
    public async Task Url_ContainsSportSegment()
    {
        var (service, capturedUrls) = OddsDataServiceFactory.CreateWithUrlCapture();

        await service.GetOddsAsync("basketball_nba");

        capturedUrls.ShouldHaveSingleItem();
        var url = capturedUrls[0]!;
        url.ShouldContain("/sports/basketball_nba/odds");
    }

    [Fact]
    public async Task Url_ContainsApiKey()
    {
        var (service, capturedUrls) = OddsDataServiceFactory
            .CreateWithUrlCapture(apiKey: "MY_SECRET_KEY");

        await service.GetOddsAsync("basketball_nba");

        capturedUrls.ShouldHaveSingleItem();
        var url = capturedUrls[0]!;
        url.ShouldContain("apiKey=MY_SECRET_KEY");
    }

    [Fact]
    public async Task Url_ContainsSpreadsTotalsAlongWithRequestedMarket()
    {
        var (service, capturedUrls) = OddsDataServiceFactory.CreateWithUrlCapture();

        await service.GetOddsAsync("basketball_nba", market: "h2h");

        capturedUrls.ShouldHaveSingleItem();
        var url = capturedUrls[0]!;

        // Implementation always appends ",spreads,totals" to the supplied market
        url.ShouldContain("markets=h2h,spreads,totals");
    }

    [Fact]
    public async Task Url_ContainsAmericanOddsFormat()
    {
        var (service, capturedUrls) = OddsDataServiceFactory.CreateWithUrlCapture();

        await service.GetOddsAsync("basketball_nba");

        capturedUrls.ShouldHaveSingleItem();
        var url = capturedUrls[0]!;
        url.ShouldContain("oddsFormat=american");
    }

    [Fact]
    public async Task Url_UsesConfiguredBaseUrl()
    {
        var (service, capturedUrls) = OddsDataServiceFactory
            .CreateWithUrlCapture(baseUrl: "https://custom.base.url/v4");

        await service.GetOddsAsync("basketball_nba");

        capturedUrls.ShouldHaveSingleItem();
        var url = capturedUrls[0]!;
        url.ShouldStartWith("https://custom.base.url/v4");
    }

    [Theory]
    [InlineData("basketball_nba")]
    [InlineData("americanfootball_nfl")]
    [InlineData("baseball_mlb")]
    public async Task Url_CorrectlyInterpolatesDifferentSports(string sport)
    {
        var (service, capturedUrls) = OddsDataServiceFactory.CreateWithUrlCapture();

        await service.GetOddsAsync(sport);

        capturedUrls.ShouldHaveSingleItem();
        var url = capturedUrls[0]!;
        url.ShouldContain($"/sports/{sport}/odds");
    }
}

// ──────────────────────────────────────────────────────────────────────────────
// GetOddsAsync — non-success HTTP responses
// ──────────────────────────────────────────────────────────────────────────────

public class OddsDataService_GetOddsAsync_NonSuccessTests
{
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task NonSuccessStatusCode_ReturnsEmptyOddsResponse(HttpStatusCode statusCode)
    {
        var (service, _) = OddsDataServiceFactory.Create(statusCode, body: null);

        var result = await service.GetOddsAsync("basketball_nba");

        result.ShouldNotBeNull();
        result.Data.ShouldBeEmpty();
    }

    [Fact]
    public async Task NonSuccessResponse_DoesNotThrow()
    {
        var (service, _) = OddsDataServiceFactory.Create(
            HttpStatusCode.InternalServerError, body: null);

        var ex = await Record.ExceptionAsync(
            () => service.GetOddsAsync("basketball_nba"));

        ex.ShouldBeNull();
    }
}

// ──────────────────────────────────────────────────────────────────────────────
// GetOddsAsync — response headers
// ──────────────────────────────────────────────────────────────────────────────

public class OddsDataService_GetOddsAsync_HeaderTests
{
    [Fact]
    public async Task RemainingRequestsHeader_Present_IsHandledWithoutException()
    {
        var headers = new Dictionary<string, string>
        {
            ["x-requests-remaining"] = "42"
        };

        var (service, _) = OddsDataServiceFactory.Create(
            HttpStatusCode.OK,
            new List<OddsGame>(),
            responseHeaders: headers);

        // The header value is only logged; we verify no exception is thrown
        // and the response is still returned correctly.
        var ex = await Record.ExceptionAsync(() => service.GetOddsAsync("basketball_nba"));
        ex.ShouldBeNull();
    }

    [Fact]
    public async Task RemainingRequestsHeader_Present_DoesNotAffectReturnedData()
    {
        var games = new List<OddsGame> { new() { Id = "g1" } };
        var headers = new Dictionary<string, string>
        {
            ["x-requests-remaining"] = "10"
        };

        var (service, _) = OddsDataServiceFactory.Create(
            HttpStatusCode.OK, games, responseHeaders: headers);

        var result = await service.GetOddsAsync("basketball_nba");

        result.Data.ShouldHaveSingleItem();
        result.Data[0].Id.ShouldBe("g1");
    }

    [Fact]
    public async Task RemainingRequestsHeader_Absent_StillReturnsDataNormally()
    {
        // No x-requests-remaining header — the TryGetValues branch is skipped
        var games = new List<OddsGame> { new() { Id = "g2" } };

        var (service, _) = OddsDataServiceFactory.Create(HttpStatusCode.OK, games);

        var result = await service.GetOddsAsync("basketball_nba");

        result.Data.ShouldHaveSingleItem();
        result.Data[0].Id.ShouldBe("g2");
    }
}

// ──────────────────────────────────────────────────────────────────────────────
// GetOddsAsync — exception handling
// ──────────────────────────────────────────────────────────────────────────────

public class OddsDataService_GetOddsAsync_ExceptionTests
{
    private static OddsDataService BuildThrowingService<TException>()
        where TException : Exception, new()
    {
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TException());

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OddsAPI:ApiKey"] = "key"
            })
            .Build();

        return new OddsDataService(
            new HttpClient(handlerMock.Object),
            config,
            Mock.Of<ILogger<OddsDataService>>());
    }

    [Fact]
    public async Task HttpRequestException_Propagates()
    {
        var service = BuildThrowingService<HttpRequestException>();

        await Should.ThrowAsync<HttpRequestException>(
            () => service.GetOddsAsync("basketball_nba"));
    }

    [Fact]
    public async Task TaskCanceledException_Propagates()
    {
        var service = BuildThrowingService<TaskCanceledException>();

        await Should.ThrowAsync<TaskCanceledException>(
            () => service.GetOddsAsync("basketball_nba"));
    }
}