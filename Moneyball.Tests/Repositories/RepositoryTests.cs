using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moneyball.Infrastructure.Repositories;

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
    public class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
    {
        public DbSet<TestEntity> TestEntities => Set<TestEntity>();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Concrete subclass that wires Repository<T> to the test DbContext
    // ──────────────────────────────────────────────────────────────────────────
    public class TestEntityRepository(TestDbContext context)
        : Repository<TestEntity>(context) // ← passes context up to Repository<T>
    {
        // Repository<T> uses a primary constructor — replicate the pattern
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

        public void Dispose()
        {
            _context.Dispose();

            GC.SuppressFinalize(this);
        }

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

            result.Should().NotBeNull();
            result.Id.Should().Be(seeded.Id);
            result.Name.Should().Be(seeded.Name);
        }

        [Fact]
        public async Task GetByIdAsync_NonExistingId_ReturnsNull()
        {
            var result = await _repository.GetByIdAsync(99999);

            result.Should().BeNull();
        }

        // ── GetAllAsync ───────────────────────────────────────────────────────

        [Fact]
        public async Task GetAllAsync_EmptyTable_ReturnsEmptyCollection()
        {
            var result = (await _repository.GetAllAsync()).ToList();

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetAllAsync_WithData_ReturnsAllEntities()
        {
            var seeded = await SeedManyAsync();

            var result = (await _repository.GetAllAsync()).ToList();

            result.Count.Should().Be(seeded.Count);
        }

        // ── FindAsync ─────────────────────────────────────────────────────────

        [Fact]
        public async Task FindAsync_MatchingPredicate_ReturnsFilteredResults()
        {
            await SeedManyAsync();

            var result = (await _repository.FindAsync(e => e.IsActive)).ToList();

            result.Count.Should().Be(2);
            result.All(e => e.IsActive).Should().BeTrue();
        }

        [Fact]
        public async Task FindAsync_NoMatchingPredicate_ReturnsEmptyCollection()
        {
            await SeedManyAsync();

            var result = (await _repository.FindAsync(e => e.Name == "DoesNotExist")).ToList();

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task FindAsync_WithComplexPredicate_ReturnsCorrectEntities()
        {
            await SeedManyAsync();

            var result = (await _repository.FindAsync(
                e => e.IsActive && e.Name.StartsWith("A"))).ToList();

            result.Should().HaveCount(1);
            result.First().Name.Should().Be("Alpha");
        }

        // ── FirstOrDefaultAsync ───────────────────────────────────────────────

        [Fact]
        public async Task FirstOrDefaultAsync_MatchingPredicate_ReturnsFirstMatch()
        {
            await SeedManyAsync();

            var result = await _repository.FirstOrDefaultAsync(e => e.IsActive);

            result.Should().NotBeNull();
            result.IsActive.Should().BeTrue();
        }

        [Fact]
        public async Task FirstOrDefaultAsync_NoMatchingPredicate_ReturnsNull()
        {
            await SeedManyAsync();

            var result = await _repository.FirstOrDefaultAsync(e => e.Name == "ZZZ");

            result.Should().BeNull();
        }

        // ── AddAsync ──────────────────────────────────────────────────────────

        [Fact]
        public async Task AddAsync_ValidEntity_ReturnsSameEntity()
        {
            var entity = new TestEntity { Name = "New" };

            var result = await _repository.AddAsync(entity);

            result.Should().BeEquivalentTo(entity);
        }

        [Fact]
        public async Task AddAsync_ValidEntity_PersistsAfterSaveChanges()
        {
            var entity = new TestEntity { Name = "Persisted" };

            await _repository.AddAsync(entity);

            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            entity.Id.Should().BeGreaterThan(0);

            (await _context.TestEntities.FindAsync([entity.Id], TestContext.Current.CancellationToken)).Should().NotBeNull();
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

            result.Should().HaveCount(2);
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
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            (await _context.TestEntities.CountAsync(TestContext.Current.CancellationToken)).Should().Be(2);
        }

        [Fact]
        public async Task AddRangeAsync_EmptyCollection_ReturnsEmptyCollection()
        {
            var result = (await _repository.AddRangeAsync(new List<TestEntity>())).ToList();

            result.Should().BeEmpty();
        }

        // ── UpdateAsync ───────────────────────────────────────────────────────

        [Fact]
        public async Task UpdateAsync_ExistingEntity_PersistsChanges()
        {
            var entity = await SeedOneAsync("OriginalName");
            entity.Name = "UpdatedName";

            await _repository.UpdateAsync(entity);
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var updated = await _context.TestEntities.FindAsync([entity.Id], TestContext.Current.CancellationToken);

            updated.Should().NotBeNull();
            updated.Name.Should().Be("UpdatedName");
        }

        [Fact]
        public async Task UpdateAsync_CompletesWithoutException()
        {
            var entity = await SeedOneAsync();
            entity.Name = "Changed";

            var ex = await Record.ExceptionAsync(() => _repository.UpdateAsync(entity));

            ex.Should().BeNull();
        }

        // ── UpdateRangeAsync ──────────────────────────────────────────────────

        [Fact]
        public async Task UpdateRangeAsync_MultipleEntities_PersistsAllChanges()
        {
            var seeded = await SeedManyAsync();
            seeded.ForEach(e => e.IsActive = false);

            await _repository.UpdateRangeAsync(seeded);
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var all = await _context.TestEntities.ToListAsync(TestContext.Current.CancellationToken);
            all.All(e => e.IsActive).Should().BeFalse();
        }

        // ── DeleteAsync ───────────────────────────────────────────────────────

        [Fact]
        public async Task DeleteAsync_ExistingEntity_RemovesFromDatabase()
        {
            var entity = await SeedOneAsync();

            await _repository.DeleteAsync(entity);
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            (await _context.TestEntities.FindAsync([entity.Id], TestContext.Current.CancellationToken)).Should().BeNull();
        }

        [Fact]
        public async Task DeleteAsync_CompletesWithoutException()
        {
            var entity = await SeedOneAsync();

            var ex = await Record.ExceptionAsync(() => _repository.DeleteAsync(entity));

            ex.Should().BeNull();
        }

        // ── CountAsync ────────────────────────────────────────────────────────

        [Fact]
        public async Task CountAsync_NoPredicate_ReturnsTotal()
        {
            await SeedManyAsync();   // 3 entities

            var count = await _repository.CountAsync();

            count.Should().Be(3);
        }

        [Fact]
        public async Task CountAsync_WithPredicate_ReturnsFilteredCount()
        {
            await SeedManyAsync();   // 2 active, 1 inactive

            var count = await _repository.CountAsync(e => e.IsActive);

            count.Should().Be(2);
        }

        [Fact]
        public async Task CountAsync_EmptyTable_ReturnsZero()
        {
            var count = await _repository.CountAsync();

            count.Should().Be(0);
        }

        [Fact]
        public async Task CountAsync_PredicateMatchesNone_ReturnsZero()
        {
            await SeedManyAsync();

            var count = await _repository.CountAsync(e => e.Name == "NoMatch");

            count.Should().Be(0);
        }

        // ── ExistsAsync ───────────────────────────────────────────────────────

        [Fact]
        public async Task ExistsAsync_MatchingPredicate_ReturnsTrue()
        {
            await SeedOneAsync();

            var exists = await _repository.ExistsAsync(e => e.Name == "Alpha");

            exists.Should().BeTrue();
        }

        [Fact]
        public async Task ExistsAsync_NoMatchingPredicate_ReturnsFalse()
        {
            await SeedOneAsync();

            var exists = await _repository.ExistsAsync(e => e.Name == "NonExistent");

            exists.Should().BeFalse();
        }

        [Fact]
        public async Task ExistsAsync_EmptyTable_ReturnsFalse()
        {
            var exists = await _repository.ExistsAsync(e => e.IsActive);

            exists.Should().BeFalse();
        }
    }
}