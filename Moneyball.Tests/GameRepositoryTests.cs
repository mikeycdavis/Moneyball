using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moneyball.Core.Entities;
using Moneyball.Core.Enums;
using Moneyball.Infrastructure.Repositories;

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
        var upcomingGameList = upcomingGames.ToList();
        upcomingGameList.Should().NotBeEmpty();
        upcomingGameList.All(game => game.GameDate >= DateTime.UtcNow).Should().BeTrue();
        upcomingGameList.All(game => game.Status == GameStatus.Scheduled).Should().BeTrue();
    }

    [Fact]
    public async Task GetUpcomingGamesAsync_FiltersBySport()
    {
        // Act
        var nbaGames = await _repository.GetUpcomingGamesAsync(sportId: 1, daysAhead: 7);

        // Assert
        var nbaGameList = nbaGames.ToList();
        nbaGameList.Should().NotBeEmpty();
        nbaGameList.All(game => game.SportId == 1).Should().BeTrue();
    }

    [Fact]
    public async Task GetGameByExternalIdAsync_ReturnsCorrectGame()
    {
        // Arrange
        var externalId = "test-game-123";

        // Act
        var game = await _repository.GetGameByExternalIdAsync(externalId, 1);

        // Assert
        game.Should().NotBeNull();
        game.ExternalGameId.Should().Be(externalId);
    }

    [Fact]
    public async Task GetGameWithDetailsAsync_IncludesRelatedData()
    {
        // Arrange
        var gameId = 1;

        // Act
        var game = await _repository.GetGameWithDetailsAsync(gameId);

        // Assert
        game.Should().NotBeNull();
        game.HomeTeam.Should().NotBeNull();
        game.AwayTeam.Should().NotBeNull();
        game.Sport.Should().NotBeNull();
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

        GC.SuppressFinalize(this);
    }
}