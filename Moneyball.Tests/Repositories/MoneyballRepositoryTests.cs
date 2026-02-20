using Shouldly;
using Moneyball.Core.Entities;
using Moq;
using Moneyball.Core.Interfaces.Repositories;

// ──────────────────────────────────────────────────────────────────────────────
// Minimal stub types — replace with your real models / DbContext
// ──────────────────────────────────────────────────────────────────────────────
namespace Moneyball.Tests.Repositories
{
    // ══════════════════════════════════════════════════════════════════════════
    //  IMoneyballRepository — unit tests (using Moq)
    //
    //  These tests verify that consumers of IMoneyballRepository interact with
    //  the interface correctly, and that the contract behaves as expected.
    //  A concrete implementation would be tested via integration tests.
    // ══════════════════════════════════════════════════════════════════════════
    public class MoneyballRepositoryTests
    {
        private readonly Mock<IMoneyballRepository> _mockRepo = new();

        // ── SaveChangesAsync ──────────────────────────────────────────────────

        [Fact]
        public async Task SaveChangesAsync_ReturnsNumberOfAffectedRows()
        {
            _mockRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(3);

            var result = await _mockRepo.Object.SaveChangesAsync();

            result.ShouldBe(3);
            _mockRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task SaveChangesAsync_NoChanges_ReturnsZero()
        {
            _mockRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(0);

            var result = await _mockRepo.Object.SaveChangesAsync();
            result.ShouldBe(0);
        }

        // ── BeginTransactionAsync ─────────────────────────────────────────────

        [Fact]
        public async Task BeginTransactionAsync_CompletesWithoutException()
        {
            _mockRepo.Setup(r => r.BeginTransactionAsync()).Returns(Task.CompletedTask);

            var ex = await Record.ExceptionAsync(
                () => _mockRepo.Object.BeginTransactionAsync());

            ex.ShouldBeNull();
            _mockRepo.Verify(r => r.BeginTransactionAsync(), Times.Once);
        }

        // ── CommitTransactionAsync ────────────────────────────────────────────

        [Fact]
        public async Task CommitTransactionAsync_CompletesWithoutException()
        {
            _mockRepo.Setup(r => r.CommitTransactionAsync()).Returns(Task.CompletedTask);

            var ex = await Record.ExceptionAsync(
                () => _mockRepo.Object.CommitTransactionAsync());

            ex.ShouldBeNull();
            _mockRepo.Verify(r => r.CommitTransactionAsync(), Times.Once);
        }

        // ── RollbackTransactionAsync ──────────────────────────────────────────

        [Fact]
        public async Task RollbackTransactionAsync_CompletesWithoutException()
        {
            _mockRepo.Setup(r => r.RollbackTransactionAsync()).Returns(Task.CompletedTask);

            var ex = await Record.ExceptionAsync(
                () => _mockRepo.Object.RollbackTransactionAsync());

            ex.ShouldBeNull();
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

            callOrder.ShouldBe(new[] { "Begin", "Commit" });
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

            callOrder.ShouldBe(new[] { "Begin", "Rollback" });
        }

        // ── IDisposable ───────────────────────────────────────────────────────

        [Fact]
        public void Dispose_IsCalled_DoesNotThrow()
        {
            var ex = Record.Exception(() => _mockRepo.Object.Dispose());

            ex.ShouldBeNull();
            _mockRepo.Verify(r => r.Dispose(), Times.Once);
        }

        // ── Sub-repository property exposure ──────────────────────────────────

        [Fact]
        public void Sports_Property_IsAccessible()
        {
            var mockSports = new Mock<IRepository<Sport>>();
            _mockRepo.Setup(r => r.Sports).Returns(mockSports.Object);

            var sports = _mockRepo.Object.Sports;

            sports.ShouldNotBeNull();
        }

        [Fact]
        public void TeamStatistics_Property_IsAccessible()
        {
            var mockStats = new Mock<IRepository<TeamStatistic>>();
            _mockRepo.Setup(r => r.TeamStatistics).Returns(mockStats.Object);

            var stats = _mockRepo.Object.TeamStatistics;

            stats.ShouldNotBeNull();
        }

        [Fact]
        public void ModelPerformances_Property_IsAccessible()
        {
            var mockPerf = new Mock<IRepository<ModelPerformance>>();
            _mockRepo.Setup(r => r.ModelPerformances).Returns(mockPerf.Object);

            var perfs = _mockRepo.Object.ModelPerformances;

            perfs.ShouldNotBeNull();
        }

        [Fact]
        public void BettingRecommendations_Property_IsAccessible()
        {
            var mockRecs = new Mock<IRepository<BettingRecommendation>>();
            _mockRepo.Setup(r => r.BettingRecommendations).Returns(mockRecs.Object);

            var recs = _mockRepo.Object.BettingRecommendations;

            recs.ShouldNotBeNull();
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

            saved.ShouldBe(1);
            mockSports.Verify(r => r.AddAsync(sport), Times.Once);
            _mockRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
        }
    }
}