using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moneyball.API.Controllers;
using Moneyball.Core.DTOs;
using Moneyball.Core.Entities;
using Moneyball.Core.Enums;
using Moneyball.Core.Interfaces.Repositories;
using Moq;

namespace Moneyball.Tests.Controllers;

/// <summary>
/// Unit tests for GamesController.
/// Tests cover all four endpoints across success, not-found, and error paths.
/// Anonymous projection types returned by the controller are verified using
/// FluentAssertions' BeEquivalentTo, which compares by member name rather than type,
/// making it well-suited to asserting anonymous objects.
/// Uses Moq for mocking the repository and FluentAssertions for readable assertions.
/// </summary>
public class GamesControllerTests
{
    private readonly Mock<IGameRepository> _mockGameRepository;
    private readonly Mock<IOddsRepository> _mockOddsRepository;
    private readonly GamesController _controller;

    /// <summary>
    /// Initializes mock and wires the repository graph together.
    /// IMoneyballRepository exposes IGamesRepository and IOddsRepository as properties,
    /// so both sub-repositories are set up here and linked via the parent mock.
    /// </summary>
    public GamesControllerTests()
    {
        var mockRepository = new Mock<IMoneyballRepository>();
        _mockGameRepository = new Mock<IGameRepository>();
        _mockOddsRepository = new Mock<IOddsRepository>();
        var mockLogger = new Mock<ILogger<GamesController>>();

        mockRepository.Setup(r => r.Games).Returns(_mockGameRepository.Object);
        mockRepository.Setup(r => r.Odds).Returns(_mockOddsRepository.Object);

        _controller = new GamesController(mockRepository.Object, mockLogger.Object);
    }

    // ==================== Test data helpers ====================

    /// <summary>
    /// Builds a minimal but realistic Game entity for use across multiple tests.
    /// Navigation properties (Sport, HomeTeam, AwayTeam) are fully populated so
    /// the controller's projection lambdas do not throw NullReferenceExceptions.
    /// </summary>
    private static Game BuildGame(
        int gameId = 1,
        int? homeScore = null,
        int? awayScore = null) => new()
        {
            GameId = gameId,
            ExternalGameId = $"ext-{gameId}",
            Sport = new Sport { SportId = 1, Name = "NBA" },
            HomeTeam = new Team
            {
                TeamId = 10,
                Name = "Los Angeles Lakers",
                Abbreviation = "LAL",
                City = "Los Angeles"
            },
            AwayTeam = new Team
            {
                TeamId = 11,
                Name = "Boston Celtics",
                Abbreviation = "BOS",
                City = "Boston"
            },
            GameDate = new DateTime(2025, 3, 15, 19, 30, 0, DateTimeKind.Utc),
            Status = GameStatus.Scheduled,
            HomeScore = homeScore,
            AwayScore = awayScore,
            Odds = new List<Odds>(),
            TeamStatistics = new List<TeamStatistic>(),
            Predictions = new List<Prediction>()
        };

    /// <summary>
    /// Builds a minimal Odds entity attached to the given game.
    /// </summary>
    private static Odds BuildOdds(Game game) => new()
    {
        OddsId = 1,
        GameId = game.GameId,
        Game = game,
        BookmakerName = "DraftKings",
        HomeMoneyline = -150,
        AwayMoneyline = 130,
        HomeSpread = -5.5m,
        HomeSpreadOdds = -110,
        AwaySpread = 5.5m,
        AwaySpreadOdds = -110,
        OverUnder = 220.5m,
        OverOdds = -110,
        UnderOdds = -110,
        RecordedAt = new DateTime(2025, 3, 14, 12, 0, 0, DateTimeKind.Utc)
    };

    // ==================== GET api/games/upcoming ====================

    /// <summary>
    /// Happy path: returns 200 OK with a projected list when games exist.
    /// Verifies that each projected item contains the fields the controller maps.
    /// </summary>
    [Fact]
    public async Task GetUpcomingGames_GamesExist_Returns200WithProjectedList()
    {
        // Arrange
        var games = new List<Game> { BuildGame(), BuildGame(2) };

        _mockGameRepository
            .Setup(r => r.GetUpcomingGamesAsync(It.IsAny<int?>(), It.IsAny<int>()))
            .ReturnsAsync(games);

        // Act
        var result = await _controller.GetUpcomingGames();

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(StatusCodes.Status200OK);

        // Cast to IEnumerable<object> and materialise so we can assert on each item
        var items = ok.Value.Should().BeAssignableTo<IEnumerable<object>>().Subject.ToList();
        items.Should().HaveCount(2);

        // Verify the shape of the first projection
        items[0].Should().BeEquivalentTo(new
        {
            GameId = 1,
            ExternalGameId = "ext-1",
            Sport = "NBA",
            HomeTeam = "Los Angeles Lakers",
            AwayTeam = "Boston Celtics",
            Status = GameStatus.Scheduled,
        }, options => options.ExcludingMissingMembers());
    }

    /// <summary>
    /// Returns 200 OK with an empty list when no upcoming games are found.
    /// The controller should not return 404 in this case.
    /// </summary>
    [Fact]
    public async Task GetUpcomingGames_NoGames_Returns200WithEmptyList()
    {
        // Arrange
        _mockGameRepository
            .Setup(r => r.GetUpcomingGamesAsync(It.IsAny<int?>(), It.IsAny<int>()))
            .ReturnsAsync(new List<Game>());

        // Act
        var result = await _controller.GetUpcomingGames();

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var items = ok.Value.Should().BeAssignableTo<IEnumerable<object>>().Subject;
        items.Should().BeEmpty("no games should produce an empty list, not a 404");
    }

    /// <summary>
    /// Verifies the repository is called with the exact sportId and daysAhead values
    /// supplied by the caller.
    /// </summary>
    [Fact]
    public async Task GetUpcomingGames_PassesQueryParametersToRepository()
    {
        // Arrange
        _mockGameRepository
            .Setup(r => r.GetUpcomingGamesAsync(It.IsAny<int?>(), It.IsAny<int>()))
            .ReturnsAsync(new List<Game>());

        // Act
        await _controller.GetUpcomingGames(sportId: 1, daysAhead: 14);

        // Assert
        _mockGameRepository.Verify(
            r => r.GetUpcomingGamesAsync(1, 14),
            Times.Once,
            "repository should receive the exact sportId and daysAhead provided by the caller");
    }

    /// <summary>
    /// When sportId is omitted the controller defaults it to null, meaning all sports.
    /// Verifies the null is forwarded to the repository rather than substituted.
    /// </summary>
    [Fact]
    public async Task GetUpcomingGames_NullSportId_ForwardsNullToRepository()
    {
        // Arrange
        _mockGameRepository
            .Setup(r => r.GetUpcomingGamesAsync(null, It.IsAny<int>()))
            .ReturnsAsync(new List<Game>());

        // Act
        await _controller.GetUpcomingGames(sportId: null);

        // Assert
        _mockGameRepository.Verify(
            r => r.GetUpcomingGamesAsync(null, It.IsAny<int>()),
            Times.Once,
            "null sportId should be forwarded to the repository to retrieve games for all sports");
    }

    /// <summary>
    /// When the repository throws, the endpoint returns 500 with the error message.
    /// The exception must not propagate to the caller.
    /// </summary>
    [Fact]
    public async Task GetUpcomingGames_RepositoryThrows_Returns500WithError()
    {
        // Arrange
        _mockGameRepository
            .Setup(r => r.GetUpcomingGamesAsync(It.IsAny<int?>(), It.IsAny<int>()))
            .ThrowsAsync(new InvalidOperationException("DB connection lost"));

        // Act
        var result = await _controller.GetUpcomingGames();

        // Assert
        var error = result.Should().BeOfType<ObjectResult>().Subject;
        error.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        error.Value.Should().BeEquivalentTo(new { error = "DB connection lost" });
    }

    // ==================== GET api/games/{gameId} ====================

    /// <summary>
    /// Happy path: returns 200 OK with the full detail projection when the game exists.
    /// Checks the top-level shape; nested sub-objects (Odds, Statistics, Predictions)
    /// are verified in their own dedicated tests below.
    /// </summary>
    [Fact]
    public async Task GetGame_GameExists_Returns200WithDetailProjection()
    {
        // Arrange
        var game = BuildGame(gameId: 42, homeScore: 110, awayScore: 105);

        _mockGameRepository
            .Setup(r => r.GetGameWithDetailsAsync(42))
            .ReturnsAsync(game);

        // Act
        var result = await _controller.GetGame(42);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(StatusCodes.Status200OK);

        ok.Value.Should().BeEquivalentTo(new
        {
            GameId = 42,
            ExternalGameId = "ext-42",
            Sport = "NBA",
            Status = GameStatus.Scheduled,
            Score = new { Home = (int?)110, Away = (int?)105 }
        }, options => options.ExcludingMissingMembers());
    }

    /// <summary>
    /// Verifies the HomeTeam and AwayTeam nested objects are projected correctly.
    /// </summary>
    [Fact]
    public async Task GetGame_GameExists_ProjectsTeamDetailsCorrectly()
    {
        // Arrange
        var game = BuildGame(gameId: 1);

        _mockGameRepository
            .Setup(r => r.GetGameWithDetailsAsync(1))
            .ReturnsAsync(game);

        // Act
        var result = await _controller.GetGame(1);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var value = ok.Value as GameResponse;
        value.Should().NotBeNull();

        value.HomeTeam.Should().BeEquivalentTo(new TeamResponse
        {
            TeamId = 10,
            Name = "Los Angeles Lakers",
            Abbreviation = "LAL",
            City = "Los Angeles"
        });

        value.AwayTeam.Should().BeEquivalentTo(new TeamResponse
        {
            TeamId = 11,
            Name = "Boston Celtics",
            Abbreviation = "BOS",
            City = "Boston"
        });
    }

    /// <summary>
    /// Verifies the Odds collection is projected correctly when odds are present.
    /// </summary>
    [Fact]
    public async Task GetGame_GameWithOdds_ProjectsOddsCorrectly()
    {
        // Arrange
        var game = BuildGame(gameId: 1);
        game.Odds = new List<Odds> { BuildOdds(game) };

        _mockGameRepository
            .Setup(r => r.GetGameWithDetailsAsync(1))
            .ReturnsAsync(game);

        // Act
        var result = await _controller.GetGame(1);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var value = ok.Value as GameResponse;
        value.Should().NotBeNull();

        var oddsItems = value.Odds.ToList();
        oddsItems.Should().HaveCount(1);

        oddsItems[0].Should().BeEquivalentTo(new OddsResponse
        {
            BookmakerName = "DraftKings",
            Moneyline = new MoneylineResponse
            {
                Home = -150,
                Away = 130
            },
            Spread = new SpreadResponse
            {
                Home = -5.5m,
                HomeOdds = -110,
                Away = 5.5m,
                AwayOdds = -110
            },
            Total = new TotalResponse
            {
                Line = 220.5m,
                Over = -110,
                Under = -110
            },
            RecordedAt = game.Odds.Single().RecordedAt,
        }, options => options.ExcludingMissingMembers());
    }

    /// <summary>
    /// Verifies the TeamStatistics collection is projected correctly, including
    /// the Home/Away label derived from IsHomeTeam.
    /// </summary>
    [Fact]
    public async Task GetGame_GameWithStatistics_ProjectsStatisticsCorrectly()
    {
        // Arrange
        var game = BuildGame(gameId: 1);
        game.TeamStatistics = new List<TeamStatistic>
        {
            new()
            {
                IsHomeTeam = true,
                Points = 110,
                FieldGoalsMade = 42,
                FieldGoalsAttempted = 88,
                FieldGoalPercentage = 47.7m,
                ThreePointsMade = 12,
                Assists = 25,
                Rebounds = 44,
                Turnovers = 12
            },
            new()
            {
                IsHomeTeam = false,
                Points = 105,
                FieldGoalsMade = 40,
                FieldGoalsAttempted = 90,
                FieldGoalPercentage = 44.4m,
                ThreePointsMade = 10,
                Assists = 22,
                Rebounds = 40,
                Turnovers = 15
            }
        };

        _mockGameRepository
            .Setup(r => r.GetGameWithDetailsAsync(1))
            .ReturnsAsync(game);

        // Act
        var result = await _controller.GetGame(1);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var value = ok.Value as GameResponse;
        value.Should().NotBeNull();

        var stats = value.Statistics.ToList();
        stats.Should().HaveCount(2);

        // IsHomeTeam = true should project as "Home"
        stats[0].Should().BeEquivalentTo(new StatisticResponse
        {
            HomeOrAway = "Home",
            Points = 110,
            FieldGoalsMade = 42,
            FieldGoalsAttempted = 88,
            FieldGoalPercentage = 47.7m,
            ThreePointsMade = 12,
            Assists = 25,
            Rebounds = 44,
            Turnovers = 12
        }, options => options.ExcludingMissingMembers());

        // IsHomeTeam = false should project as "Away"
        stats[1].Should().BeEquivalentTo(new StatisticResponse
        {
            HomeOrAway = "Away",
            Points = 105,
            FieldGoalsMade = 40,
            FieldGoalsAttempted = 90,
            FieldGoalPercentage = 44.4m,
            ThreePointsMade = 10,
            Assists = 22,
            Rebounds = 40,
            Turnovers = 15
        }, options => options.ExcludingMissingMembers());
    }

    /// <summary>
    /// Verifies the Predictions collection is projected correctly, including
    /// the nested Model name and version.
    /// </summary>
    [Fact]
    public async Task GetGame_GameWithPredictions_ProjectsPredictionsCorrectly()
    {
        // Arrange
        var game = BuildGame(gameId: 1);
        game.Predictions = new List<Prediction>
        {
            new()
            {
                Model = new Model { Name = "XGBoost", Version = "2.1" },
                PredictedHomeWinProbability = 0.62m,
                PredictedAwayWinProbability = 0.38m,
                Edge = 0.05m,
                Confidence = 0.75m,
                CreatedAt = new DateTime(2025, 3, 14, 10, 0, 0, DateTimeKind.Utc)
            }
        };

        _mockGameRepository
            .Setup(r => r.GetGameWithDetailsAsync(1))
            .ReturnsAsync(game);

        // Act
        var result = await _controller.GetGame(1);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var value = ok.Value as GameResponse;
        value.Should().NotBeNull();

        var predictions = value.Predictions.ToList();
        predictions.Should().HaveCount(1);

        predictions[0].Should().BeEquivalentTo(new PredictionResponse
        {
            Model = "XGBoost",
            Version = "2.1",
            PredictedHomeWinProbability = 0.62m,
            PredictedAwayWinProbability = 0.38m,
            Edge = 0.05m,
            Confidence = 0.75m,
            CreatedAt = game.Predictions.Single().CreatedAt
        }, options => options.ExcludingMissingMembers());
    }

    /// <summary>
    /// Returns 404 Not Found when the repository returns null for the requested game ID.
    /// </summary>
    [Fact]
    public async Task GetGame_GameNotFound_Returns404WithError()
    {
        // Arrange
        _mockGameRepository
            .Setup(r => r.GetGameWithDetailsAsync(It.IsAny<int>()))
            .ReturnsAsync((Game?)null);

        // Act
        var result = await _controller.GetGame(99);

        // Assert
        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFound.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        notFound.Value.Should().BeEquivalentTo(new { error = "Game not found" });
    }

    /// <summary>
    /// Verifies the repository is called with the exact gameId from the route.
    /// </summary>
    [Fact]
    public async Task GetGame_CallsRepositoryWithCorrectGameId()
    {
        // Arrange
        _mockGameRepository
            .Setup(r => r.GetGameWithDetailsAsync(7))
            .ReturnsAsync(BuildGame(7));

        // Act
        await _controller.GetGame(7);

        // Assert
        _mockGameRepository.Verify(
            r => r.GetGameWithDetailsAsync(7),
            Times.Once,
            "repository should be called with the exact gameId from the route");
    }

    /// <summary>
    /// When the repository throws, the endpoint returns 500 with the error message.
    /// </summary>
    [Fact]
    public async Task GetGame_RepositoryThrows_Returns500WithError()
    {
        // Arrange
        _mockGameRepository
            .Setup(r => r.GetGameWithDetailsAsync(It.IsAny<int>()))
            .ThrowsAsync(new InvalidOperationException("Timeout"));

        // Act
        var result = await _controller.GetGame(1);

        // Assert
        var error = result.Should().BeOfType<ObjectResult>().Subject;
        error.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        error.Value.Should().BeEquivalentTo(new { error = "Timeout" });
    }

    // ==================== GET api/games/range ====================

    /// <summary>
    /// Happy path: returns 200 OK with projected games for the given date range.
    /// </summary>
    [Fact]
    public async Task GetGamesByDateRange_GamesExist_Returns200WithProjectedList()
    {
        // Arrange
        var start = new DateTime(2025, 3, 1);
        var end = new DateTime(2025, 3, 31);
        var games = new List<Game> { BuildGame(homeScore: 100, awayScore: 98) };

        _mockGameRepository
            .Setup(r => r.GetGamesByDateRangeAsync(start, end, null))
            .ReturnsAsync(games);

        // Act
        var result = await _controller.GetGamesByDateRange(start, end);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(StatusCodes.Status200OK);

        var items = ok.Value.Should().BeAssignableTo<IEnumerable<object>>().Subject.ToList();
        items.Should().HaveCount(1);

        items[0].Should().BeEquivalentTo(new
        {
            GameId = 1,
            Sport = "NBA",
            HomeTeam = "Los Angeles Lakers",
            AwayTeam = "Boston Celtics",
            Status = GameStatus.Scheduled,
            Score = new { Home = (int?)100, Away = (int?)98 }
        }, options => options.ExcludingMissingMembers());
    }

    /// <summary>
    /// Returns 200 OK with an empty list when no games fall in the date range.
    /// </summary>
    [Fact]
    public async Task GetGamesByDateRange_NoGames_Returns200WithEmptyList()
    {
        // Arrange
        _mockGameRepository
            .Setup(r => r.GetGamesByDateRangeAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<Game>());

        // Act
        var result = await _controller.GetGamesByDateRange(DateTime.UtcNow, DateTime.UtcNow.AddDays(7));

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeAssignableTo<IEnumerable<object>>().Subject.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies the repository is called with the exact dates and sportId provided.
    /// </summary>
    [Fact]
    public async Task GetGamesByDateRange_PassesAllParametersToRepository()
    {
        // Arrange
        var start = new DateTime(2025, 4, 1);
        var end = new DateTime(2025, 4, 30);

        _mockGameRepository
            .Setup(r => r.GetGamesByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<Game>());

        // Act
        await _controller.GetGamesByDateRange(start, end, sportId: 2);

        // Assert
        _mockGameRepository.Verify(
            r => r.GetGamesByDateRangeAsync(start, end, 2),
            Times.Once,
            "repository should receive the exact dates and sportId passed by the caller");
    }

    /// <summary>
    /// When the repository throws, the endpoint returns 500 with the error message.
    /// </summary>
    [Fact]
    public async Task GetGamesByDateRange_RepositoryThrows_Returns500WithError()
    {
        // Arrange
        _mockGameRepository
            .Setup(r => r.GetGamesByDateRangeAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int?>()))
            .ThrowsAsync(new Exception("Query timeout"));

        // Act
        var result = await _controller.GetGamesByDateRange(DateTime.UtcNow, DateTime.UtcNow.AddDays(1));

        // Assert
        var error = result.Should().BeOfType<ObjectResult>().Subject;
        error.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        error.Value.Should().BeEquivalentTo(new { error = "Query timeout" });
    }

    // ==================== GET api/games/odds/latest ====================

    /// <summary>
    /// Happy path: returns 200 OK with a projected odds list for upcoming games.
    /// Verifies the two-step repository call (games → game IDs → odds) and the
    /// shape of the projected result.
    /// </summary>
    [Fact]
    public async Task GetLatestOdds_OddsExist_Returns200WithProjectedList()
    {
        // Arrange
        var game = BuildGame(gameId: 1);
        var oddsEntity = BuildOdds(game);

        _mockGameRepository
            .Setup(r => r.GetUpcomingGamesAsync(null, 3))
            .ReturnsAsync(new List<Game> { game });

        _mockOddsRepository
            .Setup(r => r.GetLatestOddsForGamesAsync(new List<int> { 1 }))
            .ReturnsAsync(new List<Odds> { oddsEntity });

        // Act
        var result = await _controller.GetLatestOdds();

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(StatusCodes.Status200OK);

        var items = ok.Value.Should().BeAssignableTo<IEnumerable<object>>().Subject.ToList();
        items.Should().HaveCount(1);

        items[0].Should().BeEquivalentTo(new
        {
            GameId = 1,
            Game = "Boston Celtics @ Los Angeles Lakers",
            BookmakerName = "DraftKings",
            Moneyline = new { Home = (int?)-150, Away = (int?)130 },
            Spread = new
            {
                Home = (decimal?)-5.5m,
                HomeOdds = (int?)-110,
                Away = (decimal?)5.5m,
                AwayOdds = (int?)-110
            },
            Total = new
            {
                Line = (decimal?)220.5m,
                OverOdds = (int?)-110,
                UnderOdds = (int?)-110
            }
        }, options => options.ExcludingMissingMembers());
    }

    /// <summary>
    /// Verifies the "Away @ Home" game label is formatted correctly.
    /// </summary>
    [Fact]
    public async Task GetLatestOdds_FormatsGameLabelAsAwayAtHome()
    {
        // Arrange
        var game = BuildGame(gameId: 1);

        _mockGameRepository
            .Setup(r => r.GetUpcomingGamesAsync(It.IsAny<int?>(), It.IsAny<int>()))
            .ReturnsAsync(new List<Game> { game });

        _mockOddsRepository
            .Setup(r => r.GetLatestOddsForGamesAsync(It.IsAny<List<int>>()))
            .ReturnsAsync(new List<Odds> { BuildOdds(game) });

        // Act
        var result = await _controller.GetLatestOdds();

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var items = ok.Value.Should().BeAssignableTo<IEnumerable<object>>().Subject.ToList();

        // Game label must be "AwayTeam @ HomeTeam"
        items[0].Should().BeEquivalentTo(
            new { Game = "Boston Celtics @ Los Angeles Lakers" },
            options => options.ExcludingMissingMembers());
    }

    /// <summary>
    /// Verifies that the game IDs extracted from the upcoming games query are
    /// forwarded to the odds repository exactly.
    /// </summary>
    [Fact]
    public async Task GetLatestOdds_PassesCorrectGameIdsToOddsRepository()
    {
        // Arrange
        var games = new List<Game> { BuildGame(3), BuildGame(7), BuildGame(11) };
        var expectedIds = new List<int> { 3, 7, 11 };

        _mockGameRepository
            .Setup(r => r.GetUpcomingGamesAsync(It.IsAny<int?>(), 3))
            .ReturnsAsync(games);

        _mockOddsRepository
            .Setup(r => r.GetLatestOddsForGamesAsync(It.IsAny<List<int>>()))
            .ReturnsAsync(new List<Odds>());

        // Act
        await _controller.GetLatestOdds();

        // Assert
        _mockOddsRepository.Verify(
            r => r.GetLatestOddsForGamesAsync(
                It.Is<List<int>>(ids => ids.SequenceEqual(expectedIds))),
            Times.Once,
            "odds repository should be called with the IDs of the upcoming games");
    }

    /// <summary>
    /// Verifies the games repository is always called with daysAhead = 3 for the odds endpoint,
    /// regardless of the sportId filter.
    /// </summary>
    [Fact]
    public async Task GetLatestOdds_AlwaysRequestsThreeDaysAheadOfGames()
    {
        // Arrange
        _mockGameRepository
            .Setup(r => r.GetUpcomingGamesAsync(It.IsAny<int?>(), It.IsAny<int>()))
            .ReturnsAsync(new List<Game>());

        _mockOddsRepository
            .Setup(r => r.GetLatestOddsForGamesAsync(It.IsAny<List<int>>()))
            .ReturnsAsync(new List<Odds>());

        // Act
        await _controller.GetLatestOdds(sportId: 1);

        // Assert
        _mockGameRepository.Verify(
            r => r.GetUpcomingGamesAsync(1, 3),
            Times.Once,
            "the odds endpoint should always look ahead exactly 3 days for upcoming games");
    }

    /// <summary>
    /// Returns 200 OK with an empty list when there are no upcoming games
    /// (and therefore no game IDs to pass to the odds repository).
    /// </summary>
    [Fact]
    public async Task GetLatestOdds_NoUpcomingGames_Returns200WithEmptyList()
    {
        // Arrange
        _mockGameRepository
            .Setup(r => r.GetUpcomingGamesAsync(It.IsAny<int?>(), It.IsAny<int>()))
            .ReturnsAsync(new List<Game>());

        _mockOddsRepository
            .Setup(r => r.GetLatestOddsForGamesAsync(It.IsAny<List<int>>()))
            .ReturnsAsync(new List<Odds>());

        // Act
        var result = await _controller.GetLatestOdds();

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeAssignableTo<IEnumerable<object>>().Subject.Should().BeEmpty();
    }

    /// <summary>
    /// When the games repository throws, the endpoint returns 500.
    /// </summary>
    [Fact]
    public async Task GetLatestOdds_GamesRepositoryThrows_Returns500WithError()
    {
        // Arrange
        _mockGameRepository
            .Setup(r => r.GetUpcomingGamesAsync(It.IsAny<int?>(), It.IsAny<int>()))
            .ThrowsAsync(new Exception("Games query failed"));

        // Act
        var result = await _controller.GetLatestOdds();

        // Assert
        var error = result.Should().BeOfType<ObjectResult>().Subject;
        error.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        error.Value.Should().BeEquivalentTo(new { error = "Games query failed" });
    }

    /// <summary>
    /// When the odds repository throws, the endpoint returns 500.
    /// Verifies the try/catch covers the entire two-step operation.
    /// </summary>
    [Fact]
    public async Task GetLatestOdds_OddsRepositoryThrows_Returns500WithError()
    {
        // Arrange
        _mockGameRepository
            .Setup(r => r.GetUpcomingGamesAsync(It.IsAny<int?>(), It.IsAny<int>()))
            .ReturnsAsync(new List<Game> { BuildGame() });

        _mockOddsRepository
            .Setup(r => r.GetLatestOddsForGamesAsync(It.IsAny<List<int>>()))
            .ThrowsAsync(new Exception("Odds query failed"));

        // Act
        var result = await _controller.GetLatestOdds();

        // Assert
        var error = result.Should().BeOfType<ObjectResult>().Subject;
        error.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        error.Value.Should().BeEquivalentTo(new { error = "Odds query failed" });
    }
}