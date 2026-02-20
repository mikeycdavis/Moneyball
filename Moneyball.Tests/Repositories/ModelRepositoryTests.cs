using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moneyball.Core.Entities;
using Moneyball.Core.Enums;
using Moneyball.Infrastructure.Repositories;

namespace Moneyball.Tests.Repositories;

public class ModelRepositoryTests : IDisposable
{
    private readonly MoneyballDbContext _context;
    private readonly ModelRepository _sut;

    public ModelRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<MoneyballDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new MoneyballDbContext(options);
        _sut = new ModelRepository(_context);
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

    private static Model CreateModel(
        int id,
        string name,
        string version = "1.0",
        int sportId = 1,
        bool isActive = true,
        ModelType modelType = ModelType.Python,
        DateTime? createdAt = null) =>
        new()
        {
            ModelId = id,
            Name = name,
            Version = version,
            SportId = sportId,
            IsActive = isActive,
            ModelType = modelType,
            CreatedAt = createdAt ?? DateTime.UtcNow
        };

    private async Task SeedSportsAsync()
    {
        _context.Sports.AddRange(
            CreateSport(1),
            CreateSport(2, "Basketball"));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    #endregion

    #region GetActiveModelsAsync

    public class GetActiveModelsAsyncTests : ModelRepositoryTests
    {
        [Fact]
        public async Task ReturnsOnlyActiveModelsForSport()
        {
            await SeedSportsAsync();
            _context.Models.AddRange(
                CreateModel(1, "Model A", sportId: 1, isActive: true),
                CreateModel(2, "Model B", sportId: 1, isActive: false),
                CreateModel(3, "Model C", sportId: 1, isActive: true));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetActiveModelsAsync(sportId: 1);

            result.Should().HaveCount(2)
                .And.OnlyContain(m => m.IsActive);
        }

        [Fact]
        public async Task ExcludesModelsFromOtherSports()
        {
            await SeedSportsAsync();
            _context.Models.AddRange(
                CreateModel(1, "Football Model", sportId: 1, isActive: true),
                CreateModel(2, "Basketball Model", sportId: 2, isActive: true));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetActiveModelsAsync(sportId: 1);

            result.Should().ContainSingle()
                .Which.SportId.Should().Be(1);
        }

        [Fact]
        public async Task ReturnsModelsOrderedByCreatedAtDescending()
        {
            await SeedSportsAsync();
            var now = DateTime.UtcNow;
            _context.Models.AddRange(
                CreateModel(1, "Oldest", sportId: 1, createdAt: now.AddDays(-10)),
                CreateModel(2, "Newest", sportId: 1, createdAt: now.AddDays(-1)),
                CreateModel(3, "Middle", sportId: 1, createdAt: now.AddDays(-5)));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetActiveModelsAsync(sportId: 1);

            result.Select(m => m.ModelId).Should().ContainInOrder(2, 3, 1);
        }

        [Fact]
        public async Task ReturnsEmptyWhenNoActiveModelsForSport()
        {
            await SeedSportsAsync();
            _context.Models.Add(CreateModel(1, "Inactive", sportId: 1, isActive: false));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetActiveModelsAsync(sportId: 1);

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task ReturnsEmptyForUnknownSportId()
        {
            await SeedSportsAsync();
            _context.Models.Add(CreateModel(1, "Model A", sportId: 1, isActive: true));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetActiveModelsAsync(sportId: 99);

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task IncludesSportNavigationProperty()
        {
            await SeedSportsAsync();
            _context.Models.Add(CreateModel(1, "Model A", sportId: 1));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetActiveModelsAsync(sportId: 1);

            result.Single().Sport.Should().NotBeNull();
        }
    }

    #endregion

    #region GetByNameAndVersionAsync

    public class GetByNameAndVersionAsyncTests : ModelRepositoryTests
    {
        [Fact]
        public async Task ReturnsModelMatchingNameAndVersion()
        {
            await SeedSportsAsync();
            _context.Models.Add(CreateModel(1, "Predictor", version: "2.1", sportId: 1));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetByNameAndVersionAsync("Predictor", "2.1");

            result.Should().NotBeNull();
            result.ModelId.Should().Be(1);
        }

        [Fact]
        public async Task ReturnsNullWhenNameDoesNotMatch()
        {
            await SeedSportsAsync();
            _context.Models.Add(CreateModel(1, "Predictor", version: "2.1"));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetByNameAndVersionAsync("NonExistent", "2.1");

            result.Should().BeNull();
        }

        [Fact]
        public async Task ReturnsNullWhenVersionDoesNotMatch()
        {
            await SeedSportsAsync();
            _context.Models.Add(CreateModel(1, "Predictor", version: "2.1"));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetByNameAndVersionAsync("Predictor", "9.9");

            result.Should().BeNull();
        }

        [Fact]
        public async Task DistinguishesBetweenDifferentVersionsOfSameModel()
        {
            await SeedSportsAsync();
            _context.Models.AddRange(
                CreateModel(1, "Predictor", version: "1.0", sportId: 1),
                CreateModel(2, "Predictor", version: "2.0", sportId: 1));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetByNameAndVersionAsync("Predictor", "2.0");

            result!.ModelId.Should().Be(2);
        }

        [Fact]
        public async Task ReturnsNullWhenDbIsEmpty()
        {
            var result = await _sut.GetByNameAndVersionAsync("Any", "1.0");

            result.Should().BeNull();
        }

        [Fact]
        public async Task IncludesSportNavigationProperty()
        {
            await SeedSportsAsync();
            _context.Models.Add(CreateModel(1, "Predictor", version: "1.0", sportId: 1));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetByNameAndVersionAsync("Predictor", "1.0");

            result!.Sport.Should().NotBeNull();
            result.Sport.Name.Should().Be("Football");
        }
    }

    #endregion

    #region GetModelsByTypeAsync

    public class GetModelsByTypeAsyncTests : ModelRepositoryTests
    {
        [Fact]
        public async Task ReturnsOnlyModelsOfSpecifiedType()
        {
            await SeedSportsAsync();
            _context.Models.AddRange(
                CreateModel(1, "ML Model", modelType: ModelType.Python, sportId: 1),
                CreateModel(2, "Stats Model", modelType: ModelType.MLNet, sportId: 1),
                CreateModel(3, "External Model", modelType: ModelType.External, sportId: 1),
                CreateModel(4, "Another ML", modelType: ModelType.Python, sportId: 1));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetModelsByTypeAsync(ModelType.Python);

            result.Should().HaveCount(2)
                .And.OnlyContain(m => m.ModelType == ModelType.Python);
        }

        [Fact]
        public async Task ReturnsOnlyActiveModels()
        {
            await SeedSportsAsync();
            _context.Models.AddRange(
                CreateModel(1, "Active ML", modelType: ModelType.Python, isActive: true, sportId: 1),
                CreateModel(2, "Inactive ML", modelType: ModelType.Python, isActive: false, sportId: 1));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetModelsByTypeAsync(ModelType.Python);

            result.Should().ContainSingle()
                .Which.ModelId.Should().Be(1);
        }

        [Fact]
        public async Task ReturnsEmptyWhenNoModelsOfTypeExist()
        {
            await SeedSportsAsync();
            _context.Models.Add(CreateModel(1, "ML Model", modelType: ModelType.Python, sportId: 1));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetModelsByTypeAsync(ModelType.MLNet);

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task ReturnsEmptyWhenDbIsEmpty()
        {
            var result = await _sut.GetModelsByTypeAsync(ModelType.Python);

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task ReturnsModelsAcrossAllSportsForType()
        {
            await SeedSportsAsync();
            _context.Models.AddRange(
                CreateModel(1, "Football ML", modelType: ModelType.Python, sportId: 1),
                CreateModel(2, "Basketball ML", modelType: ModelType.Python, sportId: 2));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetModelsByTypeAsync(ModelType.Python);

            result.Should().HaveCount(2);
        }

        [Fact]
        public async Task IncludesSportNavigationProperty()
        {
            await SeedSportsAsync();
            _context.Models.Add(CreateModel(1, "ML Model", modelType: ModelType.Python, sportId: 1));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetModelsByTypeAsync(ModelType.Python);

            result.Single().Sport.Should().NotBeNull();
        }
    }

    #endregion
}