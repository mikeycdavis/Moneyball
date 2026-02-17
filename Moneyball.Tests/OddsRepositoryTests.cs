using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moneyball.Core.Entities;
using Moneyball.Core.Enums;
using Moneyball.Infrastructure.Repositories;

namespace Moneyball.Tests;

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
        latestOdds.Should().NotBeNull();
        latestOdds.BookmakerName.Should().Be("DraftKings");
    }

    [Fact]
    public async Task GetLatestOddsAsync_FiltersBookmaker()
    {
        // Act
        var fanduelOdds = await _repository.GetLatestOddsAsync(1, "FanDuel");

        // Assert
        fanduelOdds.Should().NotBeNull();
        fanduelOdds.BookmakerName.Should().Be("FanDuel");
    }

    [Fact]
    public async Task GetOddsHistoryAsync_ReturnsAllOddsForGame()
    {
        // Act
        var history = await _repository.GetOddsHistoryAsync(1);

        // Assert
        var historyList = history.ToList();
        historyList.Count.Should().Be(2);
        historyList[0].RecordedAt.Should().BeOnOrAfter(historyList[1].RecordedAt); // Ordered by newest first
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

        GC.SuppressFinalize(this);
    }
}