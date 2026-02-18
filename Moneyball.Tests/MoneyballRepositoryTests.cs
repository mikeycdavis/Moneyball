using Microsoft.EntityFrameworkCore;
using Moneyball.Core.Entities;
using Moneyball.Core.Interfaces;
using Moneyball.Infrastructure.Repositories;
using Moq;

// ──────────────────────────────────────────────────────────────────────────────
// Minimal stub types — replace with your real models / DbContext
// ──────────────────────────────────────────────────────────────────────────────
namespace Moneyball.Tests.Repositories
{
    // Lightweight stand-in entity used across all generic tests
    public class TestEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }

    // Minimal DbContext backed by the EF Core In-Memory provider
    public class TestDbContext : DbContext
    {
        public DbSet<TestEntity> TestEntities => Set<TestEntity>();

        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Concrete subclass that wires Repository<T> to the test DbContext
    // ──────────────────────────────────────────────────────────────────────────
    public class TestEntityRepository : Repository<TestEntity>
    {
        // Repository<T> uses a primary constructor — replicate the pattern
        public TestEntityRepository(TestDbContext context)
            : base(context) { }               // ← passes context up to Repository<T>
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helper — builds a fresh in-memory context with an isolated database name
    // so tests never share state.
    // ──────────────────────────────────────────────────────────────────────────
    internal static class DbContextFactory
    {
        public static TestDbContext Create(string dbName)
        {
            var options = new DbContextOptionsBuilder<TestDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;

            var ctx = new TestDbContext(options);
            ctx.Database.EnsureCreated();
            return ctx;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Repository<T> — unit tests
    // ══════════════════════════════════════════════════════════════════════════
    public class RepositoryTests : IDisposable
    {
        private readonly TestDbContext _context;
        private readonly TestEntityRepository _repository;

        public RepositoryTests()
        {
            // Each test class instance gets its own isolated in-memory database
            _context = DbContextFactory.Create($"Repo_{Guid.NewGuid()}");
            _repository = new TestEntityRepository(_context);
        }

        public void Dispose() => _context.Dispose();

        // ── seed helpers ──────────────────────────────────────────────────────

        private async Task<TestEntity> SeedOneAsync(string name = "Alpha", bool active = true)
        {
            var entity = new TestEntity { Name = name, IsActive = active };
            _context.TestEntities.Add(entity);
            await _context.SaveChangesAsync();
            return entity;
        }

        private async Task<List<TestEntity>> SeedManyAsync()
        {
            var entities = new List<TestEntity>
            {
                new() { Name = "Alpha",   IsActive = true  },
                new() { Name = "Beta",    IsActive = true  },
                new() { Name = "Gamma",   IsActive = false },
            };
            _context.TestEntities.AddRange(entities);
            await _context.SaveChangesAsync();
            return entities;
        }

        // ── GetByIdAsync ──────────────────────────────────────────────────────

        [Fact]
        public async Task GetByIdAsync_ExistingId_ReturnsEntity()
        {
            var seeded = await SeedOneAsync();

            var result = await _repository.GetByIdAsync(seeded.Id);

            Assert.NotNull(result);
            Assert.Equal(seeded.Id, result!.Id);
            Assert.Equal(seeded.Name, result.Name);
        }

        [Fact]
        public async Task GetByIdAsync_NonExistingId_ReturnsNull()
        {
            var result = await _repository.GetByIdAsync(99999);

            Assert.Null(result);
        }

        // ── GetAllAsync ───────────────────────────────────────────────────────

        [Fact]
        public async Task GetAllAsync_EmptyTable_ReturnsEmptyCollection()
        {
            var result = await _repository.GetAllAsync();

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetAllAsync_WithData_ReturnsAllEntities()
        {
            var seeded = await SeedManyAsync();

            var result = (await _repository.GetAllAsync()).ToList();

            Assert.Equal(seeded.Count, result.Count);
        }

        // ── FindAsync ─────────────────────────────────────────────────────────

        [Fact]
        public async Task FindAsync_MatchingPredicate_ReturnsFilteredResults()
        {
            await SeedManyAsync();

            var result = (await _repository.FindAsync(e => e.IsActive)).ToList();

            Assert.Equal(2, result.Count);
            Assert.All(result, e => Assert.True(e.IsActive));
        }

        [Fact]
        public async Task FindAsync_NoMatchingPredicate_ReturnsEmptyCollection()
        {
            await SeedManyAsync();

            var result = await _repository.FindAsync(e => e.Name == "DoesNotExist");

            Assert.Empty(result);
        }

        [Fact]
        public async Task FindAsync_WithComplexPredicate_ReturnsCorrectEntities()
        {
            await SeedManyAsync();

            var result = (await _repository.FindAsync(
                e => e.IsActive && e.Name.StartsWith("A"))).ToList();

            Assert.Single(result);
            Assert.Equal("Alpha", result[0].Name);
        }

        // ── FirstOrDefaultAsync ───────────────────────────────────────────────

        [Fact]
        public async Task FirstOrDefaultAsync_MatchingPredicate_ReturnsFirstMatch()
        {
            await SeedManyAsync();

            var result = await _repository.FirstOrDefaultAsync(e => e.IsActive);

            Assert.NotNull(result);
            Assert.True(result!.IsActive);
        }

        [Fact]
        public async Task FirstOrDefaultAsync_NoMatchingPredicate_ReturnsNull()
        {
            await SeedManyAsync();

            var result = await _repository.FirstOrDefaultAsync(e => e.Name == "ZZZ");

            Assert.Null(result);
        }

        // ── AddAsync ──────────────────────────────────────────────────────────

        [Fact]
        public async Task AddAsync_ValidEntity_ReturnsSameEntity()
        {
            var entity = new TestEntity { Name = "New" };

            var result = await _repository.AddAsync(entity);

            Assert.Same(entity, result);
        }

        [Fact]
        public async Task AddAsync_ValidEntity_PersistsAfterSaveChanges()
        {
            var entity = new TestEntity { Name = "Persisted" };

            await _repository.AddAsync(entity);
            await _context.SaveChangesAsync();

            Assert.True(entity.Id > 0);
            Assert.NotNull(await _context.TestEntities.FindAsync(entity.Id));
        }

        // ── AddRangeAsync ─────────────────────────────────────────────────────

        [Fact]
        public async Task AddRangeAsync_ValidEntities_ReturnsAllEntities()
        {
            var entities = new List<TestEntity>
            {
                new() { Name = "X" },
                new() { Name = "Y" },
            };

            var result = (await _repository.AddRangeAsync(entities)).ToList();

            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task AddRangeAsync_ValidEntities_PersistsAfterSaveChanges()
        {
            var entities = new List<TestEntity>
            {
                new() { Name = "X" },
                new() { Name = "Y" },
            };

            await _repository.AddRangeAsync(entities);
            await _context.SaveChangesAsync();

            Assert.Equal(2, await _context.TestEntities.CountAsync());
        }

        [Fact]
        public async Task AddRangeAsync_EmptyCollection_ReturnsEmptyCollection()
        {
            var result = await _repository.AddRangeAsync(new List<TestEntity>());

            Assert.Empty(result);
        }

        // ── UpdateAsync ───────────────────────────────────────────────────────

        [Fact]
        public async Task UpdateAsync_ExistingEntity_PersistsChanges()
        {
            var entity = await SeedOneAsync("OriginalName");
            entity.Name = "UpdatedName";

            await _repository.UpdateAsync(entity);
            await _context.SaveChangesAsync();

            var updated = await _context.TestEntities.FindAsync(entity.Id);
            Assert.Equal("UpdatedName", updated!.Name);
        }

        [Fact]
        public async Task UpdateAsync_CompletesWithoutException()
        {
            var entity = await SeedOneAsync();
            entity.Name = "Changed";

            var ex = await Record.ExceptionAsync(() => _repository.UpdateAsync(entity));

            Assert.Null(ex);
        }

        // ── UpdateRangeAsync ──────────────────────────────────────────────────

        [Fact]
        public async Task UpdateRangeAsync_MultipleEntities_PersistsAllChanges()
        {
            var seeded = await SeedManyAsync();
            seeded.ForEach(e => e.IsActive = false);

            await _repository.UpdateRangeAsync(seeded);
            await _context.SaveChangesAsync();

            var all = await _context.TestEntities.ToListAsync();
            Assert.All(all, e => Assert.False(e.IsActive));
        }

        // ── DeleteAsync ───────────────────────────────────────────────────────

        [Fact]
        public async Task DeleteAsync_ExistingEntity_RemovesFromDatabase()
        {
            var entity = await SeedOneAsync();

            await _repository.DeleteAsync(entity);
            await _context.SaveChangesAsync();

            Assert.Null(await _context.TestEntities.FindAsync(entity.Id));
        }

        [Fact]
        public async Task DeleteAsync_CompletesWithoutException()
        {
            var entity = await SeedOneAsync();

            var ex = await Record.ExceptionAsync(() => _repository.DeleteAsync(entity));

            Assert.Null(ex);
        }

        // ── CountAsync ────────────────────────────────────────────────────────

        [Fact]
        public async Task CountAsync_NoPredicate_ReturnsTotal()
        {
            await SeedManyAsync();   // 3 entities

            var count = await _repository.CountAsync();

            Assert.Equal(3, count);
        }

        [Fact]
        public async Task CountAsync_WithPredicate_ReturnsFilteredCount()
        {
            await SeedManyAsync();   // 2 active, 1 inactive

            var count = await _repository.CountAsync(e => e.IsActive);

            Assert.Equal(2, count);
        }

        [Fact]
        public async Task CountAsync_EmptyTable_ReturnsZero()
        {
            var count = await _repository.CountAsync();

            Assert.Equal(0, count);
        }

        [Fact]
        public async Task CountAsync_PredicateMatchesNone_ReturnsZero()
        {
            await SeedManyAsync();

            var count = await _repository.CountAsync(e => e.Name == "NoMatch");

            Assert.Equal(0, count);
        }

        // ── ExistsAsync ───────────────────────────────────────────────────────

        [Fact]
        public async Task ExistsAsync_MatchingPredicate_ReturnsTrue()
        {
            await SeedOneAsync("Alpha");

            var exists = await _repository.ExistsAsync(e => e.Name == "Alpha");

            Assert.True(exists);
        }

        [Fact]
        public async Task ExistsAsync_NoMatchingPredicate_ReturnsFalse()
        {
            await SeedOneAsync("Alpha");

            var exists = await _repository.ExistsAsync(e => e.Name == "NonExistent");

            Assert.False(exists);
        }

        [Fact]
        public async Task ExistsAsync_EmptyTable_ReturnsFalse()
        {
            var exists = await _repository.ExistsAsync(e => e.IsActive);

            Assert.False(exists);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  IMoneyballRepository — unit tests (using Moq)
    //
    //  These tests verify that consumers of IMoneyballRepository interact with
    //  the interface correctly, and that the contract behaves as expected.
    //  A concrete implementation would be tested via integration tests.
    // ══════════════════════════════════════════════════════════════════════════
    public class MoneyballRepositoryTests
    {
        private readonly Mock<IMoneyballRepository> _mockRepo;

        public MoneyballRepositoryTests()
        {
            _mockRepo = new Mock<IMoneyballRepository>();
        }

        // ── SaveChangesAsync ──────────────────────────────────────────────────

        [Fact]
        public async Task SaveChangesAsync_ReturnsNumberOfAffectedRows()
        {
            _mockRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(3);

            var result = await _mockRepo.Object.SaveChangesAsync();

            Assert.Equal(3, result);
            _mockRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task SaveChangesAsync_NoChanges_ReturnsZero()
        {
            _mockRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(0);

            var result = await _mockRepo.Object.SaveChangesAsync();

            Assert.Equal(0, result);
        }

        // ── BeginTransactionAsync ─────────────────────────────────────────────

        [Fact]
        public async Task BeginTransactionAsync_CompletesWithoutException()
        {
            _mockRepo.Setup(r => r.BeginTransactionAsync()).Returns(Task.CompletedTask);

            var ex = await Record.ExceptionAsync(
                () => _mockRepo.Object.BeginTransactionAsync());

            Assert.Null(ex);
            _mockRepo.Verify(r => r.BeginTransactionAsync(), Times.Once);
        }

        // ── CommitTransactionAsync ────────────────────────────────────────────

        [Fact]
        public async Task CommitTransactionAsync_CompletesWithoutException()
        {
            _mockRepo.Setup(r => r.CommitTransactionAsync()).Returns(Task.CompletedTask);

            var ex = await Record.ExceptionAsync(
                () => _mockRepo.Object.CommitTransactionAsync());

            Assert.Null(ex);
            _mockRepo.Verify(r => r.CommitTransactionAsync(), Times.Once);
        }

        // ── RollbackTransactionAsync ──────────────────────────────────────────

        [Fact]
        public async Task RollbackTransactionAsync_CompletesWithoutException()
        {
            _mockRepo.Setup(r => r.RollbackTransactionAsync()).Returns(Task.CompletedTask);

            var ex = await Record.ExceptionAsync(
                () => _mockRepo.Object.RollbackTransactionAsync());

            Assert.Null(ex);
            _mockRepo.Verify(r => r.RollbackTransactionAsync(), Times.Once);
        }

        // ── Transaction lifecycle (begin → commit) ────────────────────────────

        [Fact]
        public async Task Transaction_BeginThenCommit_CallsInOrder()
        {
            var callOrder = new List<string>();

            _mockRepo.Setup(r => r.BeginTransactionAsync())
                     .Callback(() => callOrder.Add("Begin"))
                     .Returns(Task.CompletedTask);

            _mockRepo.Setup(r => r.CommitTransactionAsync())
                     .Callback(() => callOrder.Add("Commit"))
                     .Returns(Task.CompletedTask);

            await _mockRepo.Object.BeginTransactionAsync();
            await _mockRepo.Object.CommitTransactionAsync();

            Assert.Equal(new[] { "Begin", "Commit" }, callOrder);
        }

        // ── Transaction lifecycle (begin → rollback) ──────────────────────────

        [Fact]
        public async Task Transaction_BeginThenRollback_CallsInOrder()
        {
            var callOrder = new List<string>();

            _mockRepo.Setup(r => r.BeginTransactionAsync())
                     .Callback(() => callOrder.Add("Begin"))
                     .Returns(Task.CompletedTask);

            _mockRepo.Setup(r => r.RollbackTransactionAsync())
                     .Callback(() => callOrder.Add("Rollback"))
                     .Returns(Task.CompletedTask);

            await _mockRepo.Object.BeginTransactionAsync();
            await _mockRepo.Object.RollbackTransactionAsync();

            Assert.Equal(new[] { "Begin", "Rollback" }, callOrder);
        }

        // ── IDisposable ───────────────────────────────────────────────────────

        [Fact]
        public void Dispose_IsCalled_DoesNotThrow()
        {
            var ex = Record.Exception(() => _mockRepo.Object.Dispose());

            Assert.Null(ex);
            _mockRepo.Verify(r => r.Dispose(), Times.Once);
        }

        // ── Sub-repository property exposure ──────────────────────────────────

        [Fact]
        public void Sports_Property_IsAccessible()
        {
            var mockSports = new Mock<IRepository<Sport>>();
            _mockRepo.Setup(r => r.Sports).Returns(mockSports.Object);

            var sports = _mockRepo.Object.Sports;

            Assert.NotNull(sports);
        }

        [Fact]
        public void TeamStatistics_Property_IsAccessible()
        {
            var mockStats = new Mock<IRepository<TeamStatistic>>();
            _mockRepo.Setup(r => r.TeamStatistics).Returns(mockStats.Object);

            var stats = _mockRepo.Object.TeamStatistics;

            Assert.NotNull(stats);
        }

        [Fact]
        public void ModelPerformances_Property_IsAccessible()
        {
            var mockPerf = new Mock<IRepository<ModelPerformance>>();
            _mockRepo.Setup(r => r.ModelPerformances).Returns(mockPerf.Object);

            var perfs = _mockRepo.Object.ModelPerformances;

            Assert.NotNull(perfs);
        }

        [Fact]
        public void BettingRecommendations_Property_IsAccessible()
        {
            var mockRecs = new Mock<IRepository<BettingRecommendation>>();
            _mockRepo.Setup(r => r.BettingRecommendations).Returns(mockRecs.Object);

            var recs = _mockRepo.Object.BettingRecommendations;

            Assert.NotNull(recs);
        }

        // ── Sub-repo AddAsync delegated through unit-of-work ──────────────────

        [Fact]
        public async Task Sports_AddAsync_ThenSaveChanges_PersistsEntity()
        {
            var sport = new Sport { /* populate required fields */ };
            var mockSports = new Mock<IRepository<Sport>>();

            mockSports.Setup(r => r.AddAsync(sport)).ReturnsAsync(sport);
            _mockRepo.Setup(r => r.Sports).Returns(mockSports.Object);
            _mockRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);

            await _mockRepo.Object.Sports.AddAsync(sport);
            var saved = await _mockRepo.Object.SaveChangesAsync();

            Assert.Equal(1, saved);
            mockSports.Verify(r => r.AddAsync(sport), Times.Once);
            _mockRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
        }
    }
}