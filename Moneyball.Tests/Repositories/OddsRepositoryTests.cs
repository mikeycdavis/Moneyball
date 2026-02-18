using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moneyball.Core.Entities;
using Moneyball.Infrastructure.Repositories;

namespace Moneyball.Tests.Repositories;

public class OddsRepositoryTests : IDisposable
{
    private readonly MoneyballDbContext _context;
    private readonly OddsRepository _sut;

    public OddsRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<MoneyballDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new MoneyballDbContext(options);
        _sut = new OddsRepository(_context);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();

        GC.SuppressFinalize(this);
    }

    #region Helpers

    private static Odds CreateOdds(
        int id,
        int gameId,
        DateTime recordedAt,
        string bookmaker = "BetFair") =>
        new()
        {
            OddsId = id,
            GameId = gameId,
            BookmakerName = bookmaker,
            RecordedAt = recordedAt
        };

    #endregion

    #region GetLatestOddsAsync

    public class GetLatestOddsAsyncTests : OddsRepositoryTests
    {
        [Fact]
        public async Task ReturnsLatestOddsByRecordedAt()
        {
            var now = DateTime.UtcNow;
            _context.Odds.AddRange(
                CreateOdds(1, gameId: 1, recordedAt: now.AddHours(-3)),
                CreateOdds(2, gameId: 1, recordedAt: now.AddHours(-1)),
                CreateOdds(3, gameId: 1, recordedAt: now.AddHours(-5)));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetLatestOddsAsync(gameId: 1);

            result.Should().NotBeNull();
            result.OddsId.Should().Be(2);
        }

        [Fact]
        public async Task ReturnsNullWhenNoOddsExistForGame()
        {
            var result = await _sut.GetLatestOddsAsync(gameId: 99);

            result.Should().BeNull();
        }

        [Fact]
        public async Task FiltersToSpecificBookmakerWhenProvided()
        {
            var now = DateTime.UtcNow;
            _context.Odds.AddRange(
                CreateOdds(1, gameId: 1, recordedAt: now.AddHours(-1), bookmaker: "BetFair"),
                CreateOdds(2, gameId: 1, recordedAt: now.AddHours(-2), bookmaker: "DraftKings"),
                CreateOdds(3, gameId: 1, recordedAt: now.AddMinutes(-30), bookmaker: "DraftKings"));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetLatestOddsAsync(gameId: 1, bookmaker: "DraftKings");

            result.Should().NotBeNull();
            result.OddsId.Should().Be(3);
            result.BookmakerName.Should().Be("DraftKings");
        }

        [Fact]
        public async Task ReturnsNullWhenBookmakerHasNoOddsForGame()
        {
            _context.Odds.Add(CreateOdds(1, gameId: 1, recordedAt: DateTime.UtcNow, bookmaker: "BetFair"));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetLatestOddsAsync(gameId: 1, bookmaker: "NonExistentBook");

            result.Should().BeNull();
        }

        [Fact]
        public async Task ReturnsAcrossAllBookmakersWhenBookmakerIsNull()
        {
            var now = DateTime.UtcNow;
            _context.Odds.AddRange(
                CreateOdds(1, gameId: 1, recordedAt: now.AddHours(-2), bookmaker: "BetFair"),
                CreateOdds(2, gameId: 1, recordedAt: now.AddMinutes(-10), bookmaker: "DraftKings"));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetLatestOddsAsync(gameId: 1, bookmaker: null);

            result!.OddsId.Should().Be(2);
        }

        [Fact]
        public async Task ReturnsAcrossAllBookmakersWhenBookmakerIsEmptyString()
        {
            var now = DateTime.UtcNow;
            _context.Odds.AddRange(
                CreateOdds(1, gameId: 1, recordedAt: now.AddHours(-2), bookmaker: "BetFair"),
                CreateOdds(2, gameId: 1, recordedAt: now.AddMinutes(-10), bookmaker: "DraftKings"));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetLatestOddsAsync(gameId: 1, bookmaker: "");

            result!.OddsId.Should().Be(2);
        }

        [Fact]
        public async Task DoesNotReturnOddsFromDifferentGame()
        {
            _context.Odds.Add(CreateOdds(1, gameId: 2, recordedAt: DateTime.UtcNow));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetLatestOddsAsync(gameId: 1);

            result.Should().BeNull();
        }
    }

    #endregion

    #region GetOddsHistoryAsync

    public class GetOddsHistoryAsyncTests : OddsRepositoryTests
    {
        [Fact]
        public async Task ReturnsAllOddsForGame()
        {
            var now = DateTime.UtcNow;
            _context.Odds.AddRange(
                CreateOdds(1, gameId: 1, recordedAt: now.AddHours(-1)),
                CreateOdds(2, gameId: 1, recordedAt: now.AddHours(-2)),
                CreateOdds(3, gameId: 2, recordedAt: now.AddHours(-1))); // different game
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetOddsHistoryAsync(gameId: 1);

            result.Should().HaveCount(2)
                .And.OnlyContain(o => o.GameId == 1);
        }

        [Fact]
        public async Task ReturnsOddsOrderedByRecordedAtDescending()
        {
            var now = DateTime.UtcNow;
            _context.Odds.AddRange(
                CreateOdds(1, gameId: 1, recordedAt: now.AddHours(-3)),
                CreateOdds(2, gameId: 1, recordedAt: now.AddHours(-1)),
                CreateOdds(3, gameId: 1, recordedAt: now.AddHours(-5)));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetOddsHistoryAsync(gameId: 1);

            result.Select(o => o.OddsId).Should().ContainInOrder(2, 1, 3);
        }

        [Fact]
        public async Task ReturnsEmptyWhenNoOddsExistForGame()
        {
            var result = await _sut.GetOddsHistoryAsync(gameId: 99);

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task ReturnsSingleOddsEntryCorrectly()
        {
            _context.Odds.Add(CreateOdds(1, gameId: 1, recordedAt: DateTime.UtcNow));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetOddsHistoryAsync(gameId: 1);

            result.Should().ContainSingle()
                .Which.OddsId.Should().Be(1);
        }

        [Fact]
        public async Task DoesNotIncludeOddsFromOtherGames()
        {
            var now = DateTime.UtcNow;
            _context.Odds.AddRange(
                CreateOdds(1, gameId: 1, recordedAt: now),
                CreateOdds(2, gameId: 3, recordedAt: now),
                CreateOdds(3, gameId: 5, recordedAt: now));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetOddsHistoryAsync(gameId: 1);

            result.Should().NotContain(o => o.GameId != 1);
        }
    }

    #endregion

    #region GetLatestOddsForGamesAsync

    public class GetLatestOddsForGamesAsyncTests : OddsRepositoryTests
    {
        [Fact]
        public async Task ReturnsOneOddsEntryPerGame()
        {
            var now = DateTime.UtcNow;
            _context.Odds.AddRange(
                CreateOdds(1, gameId: 1, recordedAt: now.AddHours(-3)),
                CreateOdds(2, gameId: 1, recordedAt: now.AddHours(-1)), // latest for game 1
                CreateOdds(3, gameId: 2, recordedAt: now.AddHours(-2)),
                CreateOdds(4, gameId: 2, recordedAt: now.AddHours(-4))); // latest for game 2 is id 3
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetLatestOddsForGamesAsync([1, 2]);

            result.Should().HaveCount(2);
        }

        [Fact]
        public async Task ReturnsLatestOddsPerGame()
        {
            var now = DateTime.UtcNow;
            _context.Odds.AddRange(
                CreateOdds(1, gameId: 1, recordedAt: now.AddHours(-3)),
                CreateOdds(2, gameId: 1, recordedAt: now.AddHours(-1)),
                CreateOdds(3, gameId: 2, recordedAt: now.AddHours(-2)),
                CreateOdds(4, gameId: 2, recordedAt: now.AddHours(-4)));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = (await _sut.GetLatestOddsForGamesAsync([1, 2])).ToList();

            result.Should().Contain(o => o.GameId == 1 && o.OddsId == 2);
            result.Should().Contain(o => o.GameId == 2 && o.OddsId == 3);
        }

        [Fact]
        public async Task ReturnsEmptyWhenNoGameIdsProvided()
        {
            _context.Odds.Add(CreateOdds(1, gameId: 1, recordedAt: DateTime.UtcNow));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetLatestOddsForGamesAsync([]);

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task ReturnsEmptyWhenNoOddsExistForGivenIds()
        {
            var result = await _sut.GetLatestOddsForGamesAsync([10, 20, 30]);

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task IgnoresGameIdsNotInTheList()
        {
            var now = DateTime.UtcNow;
            _context.Odds.AddRange(
                CreateOdds(1, gameId: 1, recordedAt: now),
                CreateOdds(2, gameId: 5, recordedAt: now)); // not in the list
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetLatestOddsForGamesAsync([1]);

            result.Should().ContainSingle()
                .Which.GameId.Should().Be(1);
        }

        [Fact]
        public async Task HandlesSingleGameIdCorrectly()
        {
            var now = DateTime.UtcNow;
            _context.Odds.AddRange(
                CreateOdds(1, gameId: 1, recordedAt: now.AddHours(-2)),
                CreateOdds(2, gameId: 1, recordedAt: now.AddHours(-1)));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetLatestOddsForGamesAsync([1]);

            result.Should().ContainSingle()
                .Which.OddsId.Should().Be(2);
        }
    }

    #endregion
}