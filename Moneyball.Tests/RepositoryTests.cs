using Microsoft.EntityFrameworkCore;
using Moneyball.Data;
using Moneyball.Data.Entities;
using Moneyball.Data.Enums;
using Moneyball.Data.Repository;

namespace Moneyball.Tests;

public class GameRepositoryTests : IDisposable
{
    private readonly MoneyballDbContext _context;
    private readonly GameRepository _repository;

    public GameRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<MoneyballDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new MoneyballDbContext(options);
        _repository = new GameRepository(_context);

        SeedTestData();
    }

    [Fact]
    public async Task GetUpcomingGamesAsync_ReturnsOnlyFutureGames()
    {
        // Arrange - data seeded in constructor

        // Act
        var upcomingGames = await _repository.GetUpcomingGamesAsync(daysAhead: 7);

        // Assert
        Assert.NotEmpty(upcomingGames);
        Assert.All(upcomingGames, game => Assert.True(game.GameDate >= DateTime.UtcNow));
        Assert.All(upcomingGames, game => Assert.Equal(GameStatus.Scheduled, game.Status));
    }

    [Fact]
    public async Task GetUpcomingGamesAsync_FiltersBySport()
    {
        // Act
        var nbaGames = await _repository.GetUpcomingGamesAsync(sportId: 1, daysAhead: 7);

        // Assert
        Assert.NotEmpty(nbaGames);
        Assert.All(nbaGames, game => Assert.Equal(1, game.SportId));
    }

    [Fact]
    public async Task GetGameByExternalIdAsync_ReturnsCorrectGame()
    {
        // Arrange
        var externalId = "test-game-123";

        // Act
        var game = await _repository.GetGameByExternalIdAsync(externalId, 1);

        // Assert
        Assert.NotNull(game);
        Assert.Equal(externalId, game.ExternalGameId);
    }

    [Fact]
    public async Task GetGameWithDetailsAsync_IncludesRelatedData()
    {
        // Arrange
        var gameId = 1;

        // Act
        var game = await _repository.GetGameWithDetailsAsync(gameId);

        // Assert
        Assert.NotNull(game);
        Assert.NotNull(game.HomeTeam);
        Assert.NotNull(game.AwayTeam);
        Assert.NotNull(game.Sport);
    }

    private void SeedTestData()
    {
        // Add Sports
        var nbaSport = new Sport { SportId = 1, Name = "NBA", IsActive = true };
        var nflSport = new Sport { SportId = 2, Name = "NFL", IsActive = true };
        _context.Sports.AddRange(nbaSport, nflSport);

        // Add Teams
        var lakers = new Team
        {
            TeamId = 1,
            SportId = 1,
            ExternalId = "lakers-123",
            Name = "Los Angeles Lakers",
            Abbreviation = "LAL",
            City = "Los Angeles"
        };

        var celtics = new Team
        {
            TeamId = 2,
            SportId = 1,
            ExternalId = "celtics-456",
            Name = "Boston Celtics",
            Abbreviation = "BOS",
            City = "Boston"
        };

        _context.Teams.AddRange(lakers, celtics);

        // Add Games
        var futureGame = new Game
        {
            GameId = 1,
            SportId = 1,
            ExternalGameId = "test-game-123",
            HomeTeamId = 1,
            AwayTeamId = 2,
            GameDate = DateTime.UtcNow.AddDays(2),
            Status = GameStatus.Scheduled,
            IsComplete = false
        };

        var pastGame = new Game
        {
            GameId = 2,
            SportId = 1,
            ExternalGameId = "past-game-456",
            HomeTeamId = 2,
            AwayTeamId = 1,
            GameDate = DateTime.UtcNow.AddDays(-2),
            Status = GameStatus.Final,
            IsComplete = true,
            HomeScore = 108,
            AwayScore = 102
        };

        _context.Games.AddRange(futureGame, pastGame);

        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}

public class OddsRepositoryTests : IDisposable
{
    private readonly MoneyballDbContext _context;
    private readonly GameOddsRepository _repository;

    public OddsRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<MoneyballDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new MoneyballDbContext(options);
        _repository = new GameOddsRepository(_context);

        SeedTestData();
    }

    [Fact]
    public async Task GetLatestOddsAsync_ReturnsNewestOdds()
    {
        // Act
        var latestOdds = await _repository.GetLatestOddsAsync(1);

        // Assert
        Assert.NotNull(latestOdds);
        Assert.Equal("DraftKings", latestOdds.BookmakerName);
    }

    [Fact]
    public async Task GetLatestOddsAsync_FiltersBookmaker()
    {
        // Act
        var fanduelOdds = await _repository.GetLatestOddsAsync(1, "FanDuel");

        // Assert
        Assert.NotNull(fanduelOdds);
        Assert.Equal("FanDuel", fanduelOdds.BookmakerName);
    }

    [Fact]
    public async Task GetOddsHistoryAsync_ReturnsAllOddsForGame()
    {
        // Act
        var history = await _repository.GetOddsHistoryAsync(1);

        // Assert
        var historyList = history.ToList();
        Assert.Equal(2, historyList.Count);
        Assert.True(historyList[0].RecordedAt >= historyList[1].RecordedAt); // Ordered by newest first
    }

    private void SeedTestData()
    {
        // Add required Sport
        _context.Sports.Add(new Sport { SportId = 1, Name = "NBA", IsActive = true });

        // Add Teams
        _context.Teams.AddRange(
            new Team { TeamId = 1, SportId = 1, Name = "Team A", ExternalId = "a" },
            new Team { TeamId = 2, SportId = 1, Name = "Team B", ExternalId = "b" }
        );

        // Add Game
        _context.Games.Add(new Game
        {
            GameId = 1,
            SportId = 1,
            HomeTeamId = 1,
            AwayTeamId = 2,
            GameDate = DateTime.UtcNow.AddDays(1),
            Status = GameStatus.Scheduled
        });

        // Add Odds with different timestamps
        _context.GameOdds.AddRange(
            new GameOdds
            {
                GameId = 1,
                BookmakerName = "FanDuel",
                HomeMoneyline = -150,
                AwayMoneyline = 130,
                RecordedAt = DateTime.UtcNow.AddHours(-2)
            },
            new GameOdds
            {
                GameId = 1,
                BookmakerName = "DraftKings",
                HomeMoneyline = -145,
                AwayMoneyline = 125,
                RecordedAt = DateTime.UtcNow.AddHours(-1) // More recent
            }
        );

        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}