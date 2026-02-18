using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moneyball.Core.Entities;
using Moneyball.Core.Enums;
using Moneyball.Infrastructure.Repositories;

namespace Moneyball.Tests.Repositories;

public class GameRepositoryTests : IDisposable
{
    private readonly MoneyballDbContext _context;
    private readonly GameRepository _sut;

    public GameRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<MoneyballDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new MoneyballDbContext(options);
        _sut = new GameRepository(_context);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();

        GC.SuppressFinalize(this);
    }

    #region Helpers

    private static Sport CreateSport(int id, string name = "Football") =>
        new() { SportId = id, Name = name };

    private static Team CreateTeam(int id, string name) =>
        new() { TeamId = id, Name = name };

    private static Game CreateGame(
        int id,
        DateTime gameDate,
        GameStatus status = GameStatus.Scheduled,
        int sportId = 1,
        int homeTeamId = 1,
        int awayTeamId = 2,
        string? externalId = null) =>
        new()
        {
            GameId = id,
            GameDate = gameDate,
            Status = status,
            SportId = sportId,
            HomeTeamId = homeTeamId,
            AwayTeamId = awayTeamId,
            ExternalGameId = externalId ?? $"ext-{id}"
        };

    private async Task SeedBaseDataAsync()
    {
        _context.Sports.AddRange(
            CreateSport(1),
            CreateSport(2, "Basketball"));

        _context.Teams.AddRange(
            CreateTeam(1, "Home Team"),
            CreateTeam(2, "Away Team"));

        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    #endregion

    #region GetUpcomingGamesAsync

    public class GetUpcomingGamesAsyncTests : GameRepositoryTests
    {
        [Fact]
        public async Task ReturnsScheduledGamesWithinDefaultWindow()
        {
            await SeedBaseDataAsync();
            var now = DateTime.UtcNow;

            _context.Games.AddRange(
                CreateGame(1, now.AddDays(1)),
                CreateGame(2, now.AddDays(5)),
                CreateGame(3, now.AddDays(10)) // outside 7-day window
            );
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetUpcomingGamesAsync();

            result.Should().HaveCount(2)
                .And.OnlyContain(g => g.GameId == 1 || g.GameId == 2);
        }

        [Fact]
        public async Task ExcludesPastGames()
        {
            await SeedBaseDataAsync();

            _context.Games.AddRange(
                CreateGame(1, DateTime.UtcNow.AddDays(-1)), // past
                CreateGame(2, DateTime.UtcNow.AddDays(1))   // future
            );
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetUpcomingGamesAsync();

            result.Should().ContainSingle()
                .Which.GameId.Should().Be(2);
        }

        [Fact]
        public async Task ExcludesNonScheduledGames()
        {
            await SeedBaseDataAsync();
            var now = DateTime.UtcNow;

            _context.Games.AddRange(
                CreateGame(1, now.AddDays(1)),
                CreateGame(2, now.AddDays(2), GameStatus.Final),
                CreateGame(3, now.AddDays(3), GameStatus.Cancelled)
            );
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetUpcomingGamesAsync();

            result.Should().ContainSingle()
                .Which.GameId.Should().Be(1);
        }

        [Fact]
        public async Task FiltersBySportIdWhenProvided()
        {
            await SeedBaseDataAsync();
            var now = DateTime.UtcNow;

            _context.Games.AddRange(
                CreateGame(1, now.AddDays(1), sportId: 1),
                CreateGame(2, now.AddDays(2), sportId: 2),
                CreateGame(3, now.AddDays(3), sportId: 1)
            );
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetUpcomingGamesAsync(sportId: 1);

            result.Should().HaveCount(2)
                .And.OnlyContain(g => g.SportId == 1);
        }

        [Fact]
        public async Task ReturnsAllSportsWhenSportIdIsNull()
        {
            await SeedBaseDataAsync();
            var now = DateTime.UtcNow;

            _context.Games.AddRange(
                CreateGame(1, now.AddDays(1), sportId: 1),
                CreateGame(2, now.AddDays(2), sportId: 2)
            );
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetUpcomingGamesAsync(sportId: null);

            result.Should().HaveCount(2);
        }

        [Fact]
        public async Task RespectsCustomDaysAheadParameter()
        {
            await SeedBaseDataAsync();
            var now = DateTime.UtcNow;

            _context.Games.AddRange(
                CreateGame(1, now.AddDays(1)),
                CreateGame(2, now.AddDays(3)),
                CreateGame(3, now.AddDays(5))
            );
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetUpcomingGamesAsync(daysAhead: 2);

            result.Should().ContainSingle()
                .Which.GameId.Should().Be(1);
        }

        [Fact]
        public async Task ReturnsGamesOrderedByGameDate()
        {
            await SeedBaseDataAsync();
            var now = DateTime.UtcNow;

            _context.Games.AddRange(
                CreateGame(1, now.AddDays(3)),
                CreateGame(2, now.AddDays(1)),
                CreateGame(3, now.AddDays(2))
            );
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetUpcomingGamesAsync();

            result.Select(g => g.GameId).Should().ContainInOrder(2, 3, 1);
        }

        [Fact]
        public async Task ReturnsEmptyWhenNoUpcomingGames()
        {
            await SeedBaseDataAsync();

            var result = await _sut.GetUpcomingGamesAsync();

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task IncludesHomeTeamAwayTeamAndSport()
        {
            await SeedBaseDataAsync();

            _context.Games.Add(CreateGame(1, DateTime.UtcNow.AddDays(1)));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetUpcomingGamesAsync();

            var game = result.Single();
            game.HomeTeam.Should().NotBeNull();
            game.AwayTeam.Should().NotBeNull();
            game.Sport.Should().NotBeNull();
        }
    }

    #endregion

    #region GetGamesByDateRangeAsync

    public class GetGamesByDateRangeAsyncTests : GameRepositoryTests
    {
        [Fact]
        public async Task ReturnsGamesWithinDateRange()
        {
            await SeedBaseDataAsync();
            var start = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var end = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc);

            _context.Games.AddRange(
                CreateGame(1, new DateTime(2025, 1, 5, 0, 0, 0, DateTimeKind.Utc)),
                CreateGame(2, new DateTime(2025, 1, 8, 0, 0, 0, DateTimeKind.Utc)),
                CreateGame(3, new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc)) // outside range
            );
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetGamesByDateRangeAsync(start, end);

            result.Should().HaveCount(2)
                .And.OnlyContain(g => g.GameId == 1 || g.GameId == 2);
        }

        [Fact]
        public async Task IncludesBoundaryDates()
        {
            await SeedBaseDataAsync();
            var start = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var end = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc);

            _context.Games.AddRange(
                CreateGame(1, start),
                CreateGame(2, end)
            );
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetGamesByDateRangeAsync(start, end);

            result.Should().HaveCount(2);
        }

        [Fact]
        public async Task FiltersBySportIdWhenProvided()
        {
            await SeedBaseDataAsync();
            var start = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var end = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc);

            _context.Games.AddRange(
                CreateGame(1, new DateTime(2025, 1, 5, 0, 0, 0, DateTimeKind.Utc), sportId: 1),
                CreateGame(2, new DateTime(2025, 1, 5, 0, 0, 0, DateTimeKind.Utc), sportId: 2)
            );
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetGamesByDateRangeAsync(start, end, sportId: 2);

            result.Should().ContainSingle()
                .Which.SportId.Should().Be(2);
        }

        [Fact]
        public async Task ReturnsGamesOrderedByGameDate()
        {
            await SeedBaseDataAsync();
            var start = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var end = new DateTime(2025, 1, 31, 0, 0, 0, DateTimeKind.Utc);

            _context.Games.AddRange(
                CreateGame(1, new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc)),
                CreateGame(2, new DateTime(2025, 1, 5, 0, 0, 0, DateTimeKind.Utc)),
                CreateGame(3, new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc))
            );
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetGamesByDateRangeAsync(start, end);

            result.Select(g => g.GameId).Should().ContainInOrder(2, 3, 1);
        }

        [Fact]
        public async Task ReturnsEmptyWhenNoGamesInRange()
        {
            await SeedBaseDataAsync();
            var start = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var end = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc);

            var result = await _sut.GetGamesByDateRangeAsync(start, end);

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task IncludesRelatedEntities()
        {
            await SeedBaseDataAsync();
            var start = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var end = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc);

            _context.Games.Add(CreateGame(1, new DateTime(2025, 1, 5, 0, 0, 0, DateTimeKind.Utc)));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetGamesByDateRangeAsync(start, end);

            var game = result.Single();
            game.HomeTeam.Should().NotBeNull();
            game.AwayTeam.Should().NotBeNull();
            game.Sport.Should().NotBeNull();
        }
    }

    #endregion

    #region GetGameWithDetailsAsync

    public class GetGameWithDetailsAsyncTests : GameRepositoryTests
    {
        [Fact]
        public async Task ReturnsGameWithAllRelatedEntities()
        {
            await SeedBaseDataAsync();

            var game = CreateGame(1, DateTime.UtcNow.AddDays(1));
            _context.Games.Add(game);
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetGameWithDetailsAsync(1);

            result.Should().NotBeNull();
            result.GameId.Should().Be(1);
            result.HomeTeam.Should().NotBeNull();
            result.AwayTeam.Should().NotBeNull();
            result.Sport.Should().NotBeNull();
        }

        [Fact]
        public async Task ReturnsNullWhenGameNotFound()
        {
            var result = await _sut.GetGameWithDetailsAsync(999);

            result.Should().BeNull();
        }

        [Fact]
        public async Task ReturnsCorrectGameById()
        {
            await SeedBaseDataAsync();

            _context.Games.AddRange(
                CreateGame(1, DateTime.UtcNow.AddDays(1)),
                CreateGame(2, DateTime.UtcNow.AddDays(2))
            );
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetGameWithDetailsAsync(2);

            result.Should().NotBeNull();
            result.GameId.Should().Be(2);
        }

        [Fact]
        public async Task IncludesOddsCollection()
        {
            await SeedBaseDataAsync();

            var game = CreateGame(1, DateTime.UtcNow.AddDays(1));
            _context.Games.Add(game);
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            _context.Odds.AddRange(
                new Odds { OddsId = 1, GameId = 1, RecordedAt = DateTime.UtcNow },
                new Odds { OddsId = 2, GameId = 1, RecordedAt = DateTime.UtcNow }
            );
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetGameWithDetailsAsync(1);

            result!.Odds.Should().HaveCount(2);
        }

        [Fact]
        public async Task IncludesPredictionsWithModel()
        {
            await SeedBaseDataAsync();

            _context.Games.Add(CreateGame(1, DateTime.UtcNow.AddDays(1)));
            var model = new Model { ModelId = 1, Name = "Test Model" };
            _context.Models.Add(model);
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            _context.Predictions.Add(new Prediction { PredictionId = 1, GameId = 1, ModelId = 1 });
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetGameWithDetailsAsync(1);

            result!.Predictions.Should().ContainSingle()
                .Which.Model.Should().NotBeNull();
        }
    }

    #endregion

    #region GetGameByExternalIdAsync

    public class GetGameByExternalIdAsyncTests : GameRepositoryTests
    {
        [Fact]
        public async Task ReturnsGameMatchingExternalIdAndSportId()
        {
            await SeedBaseDataAsync();

            _context.Games.Add(CreateGame(1, DateTime.UtcNow.AddDays(1), externalId: "abc-123", sportId: 1));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetGameByExternalIdAsync("abc-123", 1);

            result.Should().NotBeNull();
            result.GameId.Should().Be(1);
        }

        [Fact]
        public async Task ReturnsNullWhenExternalIdNotFound()
        {
            await SeedBaseDataAsync();

            var result = await _sut.GetGameByExternalIdAsync("nonexistent", 1);

            result.Should().BeNull();
        }

        [Fact]
        public async Task ReturnsNullWhenSportIdDoesNotMatch()
        {
            await SeedBaseDataAsync();

            _context.Games.Add(CreateGame(1, DateTime.UtcNow.AddDays(1), externalId: "abc-123", sportId: 1));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetGameByExternalIdAsync("abc-123", sportId: 2);

            result.Should().BeNull();
        }

        [Fact]
        public async Task DistinguishesBetweenSameSportDifferentExternalIds()
        {
            await SeedBaseDataAsync();

            _context.Games.AddRange(
                CreateGame(1, DateTime.UtcNow.AddDays(1), externalId: "game-1", sportId: 1),
                CreateGame(2, DateTime.UtcNow.AddDays(2), externalId: "game-2", sportId: 1)
            );
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetGameByExternalIdAsync("game-2", 1);

            result.Should().NotBeNull();
            result.GameId.Should().Be(2);
        }

        [Fact]
        public async Task IncludesHomeTeamAndAwayTeam()
        {
            await SeedBaseDataAsync();

            _context.Games.Add(CreateGame(1, DateTime.UtcNow.AddDays(1), externalId: "abc-123"));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetGameByExternalIdAsync("abc-123", 1);

            result!.HomeTeam.Should().NotBeNull();
            result.AwayTeam.Should().NotBeNull();
        }
    }

    #endregion

    #region GetGamesNeedingOddsUpdateAsync

    public class GetGamesNeedingOddsUpdateAsyncTests : GameRepositoryTests
    {
        [Fact]
        public async Task ReturnsScheduledFutureGamesWithNoOdds()
        {
            await SeedBaseDataAsync();

            _context.Games.Add(CreateGame(1, DateTime.UtcNow.AddDays(1)));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetGamesNeedingOddsUpdateAsync();

            result.Should().ContainSingle()
                .Which.GameId.Should().Be(1);
        }

        [Fact]
        public async Task ReturnsGamesWhereOddsAreOlderThanCutoff()
        {
            await SeedBaseDataAsync();

            var game = CreateGame(1, DateTime.UtcNow.AddDays(1));
            _context.Games.Add(game);
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            _context.Odds.Add(new Odds
            {
                OddsId = 1,
                GameId = 1,
                RecordedAt = DateTime.UtcNow.AddHours(-3) // older than default 1-hour cutoff
            });
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetGamesNeedingOddsUpdateAsync(hoursOld: 1);

            result.Should().ContainSingle()
                .Which.GameId.Should().Be(1);
        }

        [Fact]
        public async Task ExcludesGamesWithRecentOdds()
        {
            await SeedBaseDataAsync();

            var game = CreateGame(1, DateTime.UtcNow.AddDays(1));
            _context.Games.Add(game);
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            _context.Odds.Add(new Odds
            {
                OddsId = 1,
                GameId = 1,
                RecordedAt = DateTime.UtcNow.AddMinutes(-10) // recent — within 1 hour
            });
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetGamesNeedingOddsUpdateAsync(hoursOld: 1);

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task ExcludesPastGames()
        {
            await SeedBaseDataAsync();

            _context.Games.Add(CreateGame(1, DateTime.UtcNow.AddDays(-1)));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetGamesNeedingOddsUpdateAsync();

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task ExcludesNonScheduledGames()
        {
            await SeedBaseDataAsync();

            _context.Games.AddRange(
                CreateGame(1, DateTime.UtcNow.AddDays(1), GameStatus.Final),
                CreateGame(2, DateTime.UtcNow.AddDays(1), GameStatus.Cancelled)
            );
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetGamesNeedingOddsUpdateAsync();

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task RespectsCustomHoursOldParameter()
        {
            await SeedBaseDataAsync();

            var game = CreateGame(1, DateTime.UtcNow.AddDays(1));
            _context.Games.Add(game);
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            _context.Odds.Add(new Odds
            {
                OddsId = 1,
                GameId = 1,
                RecordedAt = DateTime.UtcNow.AddHours(-2)
            });
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            // With 3-hour cutoff, odds from 2 hours ago are still fresh
            var resultFresh = await _sut.GetGamesNeedingOddsUpdateAsync(hoursOld: 3);
            resultFresh.Should().BeEmpty();

            // With 1-hour cutoff, odds from 2 hours ago are stale
            var resultStale = await _sut.GetGamesNeedingOddsUpdateAsync(hoursOld: 1);
            resultStale.Should().ContainSingle();
        }

        [Fact]
        public async Task UsesMaxOddsRecordedAtForStalenessCheck()
        {
            await SeedBaseDataAsync();

            var game = CreateGame(1, DateTime.UtcNow.AddDays(1));
            _context.Games.Add(game);
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            _context.Odds.AddRange(
                new Odds { OddsId = 1, GameId = 1, RecordedAt = DateTime.UtcNow.AddHours(-5) }, // old
                new Odds { OddsId = 2, GameId = 1, RecordedAt = DateTime.UtcNow.AddMinutes(-10) } // recent
            );
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Max is recent, so game should NOT need an update
            var result = await _sut.GetGamesNeedingOddsUpdateAsync(hoursOld: 1);

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task IncludesOddsCollection()
        {
            await SeedBaseDataAsync();

            _context.Games.Add(CreateGame(1, DateTime.UtcNow.AddDays(1)));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetGamesNeedingOddsUpdateAsync();

            result.Single().Odds.Should().NotBeNull();
        }
    }

    #endregion
}