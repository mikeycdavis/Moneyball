using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moneyball.Core.Entities;
using Moneyball.Core.Enums;
using Moneyball.Infrastructure.Repositories;

namespace Moneyball.Tests.Repositories;

public class PredictionRepositoryTests : IDisposable
{
    private readonly MoneyballDbContext _context;
    private readonly PredictionRepository _sut;

    public PredictionRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<MoneyballDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new MoneyballDbContext(options);
        _sut = new PredictionRepository(_context);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();

        GC.SuppressFinalize(this);
    }

    #region Helpers

    private static Sport CreateSport(int id, SportType name = SportType.NFL) =>
        new() { SportId = id, Name = name };

    private static Team CreateTeam(int id, string name) =>
        new() { TeamId = id, Name = name };

    private static Game CreateGame(
        int id,
        int sportId = 1,
        GameStatus status = GameStatus.Scheduled,
        DateTime? gameDate = null) =>
        new()
        {
            GameId = id,
            SportId = sportId,
            Status = status,
            GameDate = gameDate ?? DateTime.UtcNow.AddDays(1),
            HomeTeamId = 1,
            AwayTeamId = 2
        };

    private static Model CreateModel(int id, string name = "TestModel") =>
        new() { ModelId = id, Name = name };

    private static Prediction CreatePrediction(
        int id,
        int gameId,
        int modelId,
        DateTime? createdAt = null,
        decimal edge = 0.05m) =>
        new()
        {
            PredictionId = id,
            GameId = gameId,
            ModelId = modelId,
            CreatedAt = createdAt ?? DateTime.UtcNow,
            Edge = edge
        };

    private async Task SeedBaseDataAsync()
    {
        _context.Sports.Add(CreateSport(1));
        _context.Teams.AddRange(CreateTeam(1, "Home Team"), CreateTeam(2, "Away Team"));
        _context.Models.AddRange(CreateModel(1, "Model A"), CreateModel(2, "Model B"));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    #endregion

    #region GetPredictionsByGameAsync

    public class GetPredictionsByGameAsyncTests : PredictionRepositoryTests
    {
        [Fact]
        public async Task ReturnsAllPredictionsForGame()
        {
            await SeedBaseDataAsync();
            _context.Games.Add(CreateGame(1));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            _context.Predictions.AddRange(
                CreatePrediction(1, gameId: 1, modelId: 1),
                CreatePrediction(2, gameId: 1, modelId: 2),
                CreatePrediction(3, gameId: 2, modelId: 1)); // different game
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetPredictionsByGameAsync(gameId: 1);

            result.Should().HaveCount(2)
                .And.OnlyContain(p => p.GameId == 1);
        }

        [Fact]
        public async Task ReturnsEmptyWhenNoPredictionsExistForGame()
        {
            var result = await _sut.GetPredictionsByGameAsync(gameId: 99);

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task ReturnsPredictionsOrderedByCreatedAtDescending()
        {
            await SeedBaseDataAsync();
            _context.Games.Add(CreateGame(1));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var now = DateTime.UtcNow;
            _context.Predictions.AddRange(
                CreatePrediction(1, gameId: 1, modelId: 1, createdAt: now.AddHours(-3)),
                CreatePrediction(2, gameId: 1, modelId: 1, createdAt: now.AddHours(-1)),
                CreatePrediction(3, gameId: 1, modelId: 2, createdAt: now.AddHours(-5)));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetPredictionsByGameAsync(gameId: 1);

            result.Select(p => p.PredictionId).Should().ContainInOrder(2, 1, 3);
        }

        [Fact]
        public async Task IncludesModelAndGameNavigationProperties()
        {
            await SeedBaseDataAsync();
            _context.Games.Add(CreateGame(1));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
            _context.Predictions.Add(CreatePrediction(1, gameId: 1, modelId: 1));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetPredictionsByGameAsync(gameId: 1);

            var prediction = result.Single();
            prediction.Model.Should().NotBeNull();
            prediction.Game.Should().NotBeNull();
        }
    }

    #endregion

    #region GetPredictionsByModelAsync

    public class GetPredictionsByModelAsyncTests : PredictionRepositoryTests
    {
        [Fact]
        public async Task ReturnsAllPredictionsForModel()
        {
            await SeedBaseDataAsync();
            _context.Games.AddRange(CreateGame(1), CreateGame(2));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            _context.Predictions.AddRange(
                CreatePrediction(1, gameId: 1, modelId: 1),
                CreatePrediction(2, gameId: 2, modelId: 1),
                CreatePrediction(3, gameId: 1, modelId: 2)); // different model
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetPredictionsByModelAsync(modelId: 1);

            result.Should().HaveCount(2)
                .And.OnlyContain(p => p.ModelId == 1);
        }

        [Fact]
        public async Task ReturnsEmptyWhenNoPredictionsExistForModel()
        {
            var result = await _sut.GetPredictionsByModelAsync(modelId: 99);

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task FiltersBySinceDateWhenProvided()
        {
            await SeedBaseDataAsync();
            _context.Games.AddRange(CreateGame(1), CreateGame(2), CreateGame(3));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var cutoff = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
            _context.Predictions.AddRange(
                CreatePrediction(1, gameId: 1, modelId: 1, createdAt: new DateTime(2025, 5, 1, 0, 0, 0, DateTimeKind.Utc)), // before cutoff
                CreatePrediction(2, gameId: 2, modelId: 1, createdAt: new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc)), // on boundary
                CreatePrediction(3, gameId: 3, modelId: 1, createdAt: new DateTime(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc))); // after cutoff
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetPredictionsByModelAsync(modelId: 1, since: cutoff);

            result.Should().HaveCount(2)
                .And.OnlyContain(p => p.PredictionId == 2 || p.PredictionId == 3);
        }

        [Fact]
        public async Task ReturnsAllPredictionsWhenSinceIsNull()
        {
            await SeedBaseDataAsync();
            _context.Games.AddRange(CreateGame(1), CreateGame(2));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            _context.Predictions.AddRange(
                CreatePrediction(1, gameId: 1, modelId: 1, createdAt: DateTime.UtcNow.AddMonths(-6)),
                CreatePrediction(2, gameId: 2, modelId: 1, createdAt: DateTime.UtcNow.AddDays(-1)));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetPredictionsByModelAsync(modelId: 1, since: null);

            result.Should().HaveCount(2);
        }

        [Fact]
        public async Task ReturnsPredictionsOrderedByCreatedAtDescending()
        {
            await SeedBaseDataAsync();
            _context.Games.AddRange(CreateGame(1), CreateGame(2), CreateGame(3));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var now = DateTime.UtcNow;
            _context.Predictions.AddRange(
                CreatePrediction(1, gameId: 1, modelId: 1, createdAt: now.AddHours(-5)),
                CreatePrediction(2, gameId: 2, modelId: 1, createdAt: now.AddHours(-1)),
                CreatePrediction(3, gameId: 3, modelId: 1, createdAt: now.AddHours(-3)));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetPredictionsByModelAsync(modelId: 1);

            result.Select(p => p.PredictionId).Should().ContainInOrder(2, 3, 1);
        }

        [Fact]
        public async Task IncludesGameWithHomeAndAwayTeams()
        {
            await SeedBaseDataAsync();
            _context.Games.Add(CreateGame(1));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
            _context.Predictions.Add(CreatePrediction(1, gameId: 1, modelId: 1));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetPredictionsByModelAsync(modelId: 1);

            var prediction = result.Single();
            prediction.Game.Should().NotBeNull();
            prediction.Game.HomeTeam.Should().NotBeNull();
            prediction.Game.AwayTeam.Should().NotBeNull();
        }
    }

    #endregion

    #region GetLatestPredictionAsync

    public class GetLatestPredictionAsyncTests : PredictionRepositoryTests
    {
        [Fact]
        public async Task ReturnsLatestPredictionForGameAndModel()
        {
            await SeedBaseDataAsync();
            _context.Games.Add(CreateGame(1));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var now = DateTime.UtcNow;
            _context.Predictions.AddRange(
                CreatePrediction(1, gameId: 1, modelId: 1, createdAt: now.AddHours(-3)),
                CreatePrediction(2, gameId: 1, modelId: 1, createdAt: now.AddHours(-1)),
                CreatePrediction(3, gameId: 1, modelId: 1, createdAt: now.AddHours(-5)));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetLatestPredictionAsync(gameId: 1, modelId: 1);

            result.Should().NotBeNull();
            result.PredictionId.Should().Be(2);
        }

        [Fact]
        public async Task ReturnsNullWhenNoPredictionExistsForGameAndModel()
        {
            var result = await _sut.GetLatestPredictionAsync(gameId: 99, modelId: 99);

            result.Should().BeNull();
        }

        [Fact]
        public async Task DoesNotReturnPredictionFromDifferentModel()
        {
            await SeedBaseDataAsync();
            _context.Games.Add(CreateGame(1));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            _context.Predictions.Add(CreatePrediction(1, gameId: 1, modelId: 2));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetLatestPredictionAsync(gameId: 1, modelId: 1);

            result.Should().BeNull();
        }

        [Fact]
        public async Task DoesNotReturnPredictionFromDifferentGame()
        {
            await SeedBaseDataAsync();
            _context.Games.AddRange(CreateGame(1), CreateGame(2));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            _context.Predictions.Add(CreatePrediction(1, gameId: 2, modelId: 1));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetLatestPredictionAsync(gameId: 1, modelId: 1);

            result.Should().BeNull();
        }

        [Fact]
        public async Task IncludesModelAndGameNavigationProperties()
        {
            await SeedBaseDataAsync();
            _context.Games.Add(CreateGame(1));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
            _context.Predictions.Add(CreatePrediction(1, gameId: 1, modelId: 1));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetLatestPredictionAsync(gameId: 1, modelId: 1);

            result!.Model.Should().NotBeNull();
            result.Game.Should().NotBeNull();
        }
    }

    #endregion

    #region GetPredictionsWithHighEdgeAsync

    public class GetPredictionsWithHighEdgeAsyncTests : PredictionRepositoryTests
    {
        [Fact]
        public async Task ReturnsOnlyPredictionsAtOrAboveMinEdge()
        {
            await SeedBaseDataAsync();
            _context.Games.AddRange(CreateGame(1), CreateGame(2), CreateGame(3));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            _context.Predictions.AddRange(
                CreatePrediction(1, gameId: 1, modelId: 1, edge: 0.10m),
                CreatePrediction(2, gameId: 2, modelId: 1, edge: 0.05m),
                CreatePrediction(3, gameId: 3, modelId: 1, edge: 0.02m));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetPredictionsWithHighEdgeAsync(minEdge: 0.05m);

            result.Should().HaveCount(2)
                .And.OnlyContain(p => p.Edge >= 0.05m);
        }

        [Fact]
        public async Task ExcludesPredictionsForNonScheduledGames()
        {
            await SeedBaseDataAsync();
            _context.Games.AddRange(
                CreateGame(1, status: GameStatus.Scheduled),
                CreateGame(2, status: GameStatus.Final),
                CreateGame(3, status: GameStatus.Cancelled));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            _context.Predictions.AddRange(
                CreatePrediction(1, gameId: 1, modelId: 1, edge: 0.10m),
                CreatePrediction(2, gameId: 2, modelId: 1, edge: 0.10m),
                CreatePrediction(3, gameId: 3, modelId: 1, edge: 0.10m));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetPredictionsWithHighEdgeAsync(minEdge: 0.05m);

            result.Should().ContainSingle()
                .Which.GameId.Should().Be(1);
        }

        [Fact]
        public async Task ExcludesPredictionsForPastGames()
        {
            await SeedBaseDataAsync();
            _context.Games.AddRange(
                CreateGame(1, gameDate: DateTime.UtcNow.AddDays(1)),
                CreateGame(2, gameDate: DateTime.UtcNow.AddDays(-1)));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            _context.Predictions.AddRange(
                CreatePrediction(1, gameId: 1, modelId: 1, edge: 0.10m),
                CreatePrediction(2, gameId: 2, modelId: 1, edge: 0.10m));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetPredictionsWithHighEdgeAsync(minEdge: 0.05m);

            result.Should().ContainSingle()
                .Which.GameId.Should().Be(1);
        }

        [Fact]
        public async Task FiltersBySportIdWhenProvided()
        {
            await SeedBaseDataAsync();
            _context.Sports.Add(CreateSport(2, SportType.NBA));
            _context.Games.AddRange(
                CreateGame(1, sportId: 1),
                CreateGame(2, sportId: 2));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            _context.Predictions.AddRange(
                CreatePrediction(1, gameId: 1, modelId: 1, edge: 0.10m),
                CreatePrediction(2, gameId: 2, modelId: 1, edge: 0.10m));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetPredictionsWithHighEdgeAsync(minEdge: 0.05m, sportId: 1);

            result.Should().ContainSingle()
                .Which.GameId.Should().Be(1);
        }

        [Fact]
        public async Task ReturnsAllSportsWhenSportIdIsNull()
        {
            await SeedBaseDataAsync();
            _context.Sports.Add(CreateSport(2, SportType.NBA));
            _context.Games.AddRange(CreateGame(1, sportId: 1), CreateGame(2, sportId: 2));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            _context.Predictions.AddRange(
                CreatePrediction(1, gameId: 1, modelId: 1, edge: 0.10m),
                CreatePrediction(2, gameId: 2, modelId: 1, edge: 0.10m));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetPredictionsWithHighEdgeAsync(minEdge: 0.05m, sportId: null);

            result.Should().HaveCount(2);
        }

        [Fact]
        public async Task ReturnsPredictionsOrderedByEdgeDescending()
        {
            await SeedBaseDataAsync();
            _context.Games.AddRange(CreateGame(1), CreateGame(2), CreateGame(3));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            _context.Predictions.AddRange(
                CreatePrediction(1, gameId: 1, modelId: 1, edge: 0.05m),
                CreatePrediction(2, gameId: 2, modelId: 1, edge: 0.15m),
                CreatePrediction(3, gameId: 3, modelId: 1, edge: 0.10m));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetPredictionsWithHighEdgeAsync(minEdge: 0.01m);

            result.Select(p => p.PredictionId).Should().ContainInOrder(2, 3, 1);
        }

        [Fact]
        public async Task ReturnsEmptyWhenNoPredictionsMeetMinEdge()
        {
            await SeedBaseDataAsync();
            _context.Games.Add(CreateGame(1));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            _context.Predictions.Add(CreatePrediction(1, gameId: 1, modelId: 1, edge: 0.01m));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetPredictionsWithHighEdgeAsync(minEdge: 0.50m);

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task IncludesGameWithTeamsAndModel()
        {
            await SeedBaseDataAsync();
            _context.Games.Add(CreateGame(1));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
            _context.Predictions.Add(CreatePrediction(1, gameId: 1, modelId: 1, edge: 0.10m));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetPredictionsWithHighEdgeAsync(minEdge: 0.05m);

            var prediction = result.Single();
            prediction.Game.Should().NotBeNull();
            prediction.Game.HomeTeam.Should().NotBeNull();
            prediction.Game.AwayTeam.Should().NotBeNull();
            prediction.Model.Should().NotBeNull();
        }
    }

    #endregion
}