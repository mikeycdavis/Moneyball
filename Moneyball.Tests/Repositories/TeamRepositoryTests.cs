using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moneyball.Core.Entities;
using Moneyball.Core.Enums;
using Moneyball.Infrastructure.Repositories;

namespace Moneyball.Tests.Repositories;

public class TeamRepositoryTests : IDisposable
{
    private readonly MoneyballDbContext _context;
    private readonly TeamRepository _sut;

    public TeamRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<MoneyballDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new MoneyballDbContext(options);
        _sut = new TeamRepository(_context);
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

    private static Team CreateTeam(
        int id,
        string name,
        int sportId = 1,
        string? externalId = null) =>
        new()
        {
            TeamId = id,
            Name = name,
            SportId = sportId,
            ExternalId = externalId ?? $"ext-{id}"
        };

    private async Task SeedSportsAsync()
    {
        _context.Sports.AddRange(
            CreateSport(1),
            CreateSport(2, SportType.NBA));

        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    #endregion

    #region GetByExternalIdAsync

    public class GetByExternalIdAsyncTests : TeamRepositoryTests
    {
        [Fact]
        public async Task ReturnsTeamMatchingExternalIdAndSportId()
        {
            await SeedSportsAsync();
            _context.Teams.Add(CreateTeam(1, "Arsenal", sportId: 1, externalId: "afc-001"));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetByExternalIdAsync("afc-001", sportId: 1);

            result.Should().NotBeNull();
            result.TeamId.Should().Be(1);
        }

        [Fact]
        public async Task ReturnsNullWhenExternalIdDoesNotExist()
        {
            await SeedSportsAsync();

            var result = await _sut.GetByExternalIdAsync("nonexistent", sportId: 1);

            result.Should().BeNull();
        }

        [Fact]
        public async Task ReturnsNullWhenExternalIdExistsButSportIdDoesNotMatch()
        {
            await SeedSportsAsync();
            _context.Teams.Add(CreateTeam(1, "Arsenal", sportId: 1, externalId: "afc-001"));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetByExternalIdAsync("afc-001", sportId: 2);

            result.Should().BeNull();
        }

        [Fact]
        public async Task DistinguishesBetweenTeamsWithSameExternalIdInDifferentSports()
        {
            await SeedSportsAsync();
            _context.Teams.AddRange(
                CreateTeam(1, "Team A", sportId: 1, externalId: "shared-ext"),
                CreateTeam(2, "Team B", sportId: 2, externalId: "shared-ext"));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var sport1Result = await _sut.GetByExternalIdAsync("shared-ext", sportId: 1);
            var sport2Result = await _sut.GetByExternalIdAsync("shared-ext", sportId: 2);

            sport1Result!.TeamId.Should().Be(1);
            sport2Result!.TeamId.Should().Be(2);
        }

        [Fact]
        public async Task ReturnsNullWhenDbIsEmpty()
        {
            var result = await _sut.GetByExternalIdAsync("any-id", sportId: 1);

            result.Should().BeNull();
        }
    }

    #endregion

    #region GetBySportAsync

    public class GetBySportAsyncTests : TeamRepositoryTests
    {
        [Fact]
        public async Task ReturnsOnlyTeamsForGivenSport()
        {
            await SeedSportsAsync();
            _context.Teams.AddRange(
                CreateTeam(1, "Team A", sportId: 1),
                CreateTeam(2, "Team B", sportId: 1),
                CreateTeam(3, "Team C", sportId: 2));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetBySportAsync(sportId: 1);

            result.Should().HaveCount(2)
                .And.OnlyContain(t => t.SportId == 1);
        }

        [Fact]
        public async Task ReturnsTeamsOrderedByNameAscending()
        {
            await SeedSportsAsync();
            _context.Teams.AddRange(
                CreateTeam(1, "Zebra FC", sportId: 1),
                CreateTeam(2, "Alpha FC", sportId: 1),
                CreateTeam(3, "Mango United", sportId: 1));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetBySportAsync(sportId: 1);

            result.Select(t => t.Name).Should().BeInAscendingOrder();
        }

        [Fact]
        public async Task ReturnsEmptyWhenNoTeamsExistForSport()
        {
            await SeedSportsAsync();

            var result = await _sut.GetBySportAsync(sportId: 1);

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task ReturnsEmptyForUnknownSportId()
        {
            await SeedSportsAsync();
            _context.Teams.Add(CreateTeam(1, "Team A", sportId: 1));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetBySportAsync(sportId: 99);

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task DoesNotReturnTeamsFromOtherSports()
        {
            await SeedSportsAsync();
            _context.Teams.AddRange(
                CreateTeam(1, "Football Team", sportId: 1),
                CreateTeam(2, "Basketball Team", sportId: 2));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetBySportAsync(sportId: 1);

            result.Should().NotContain(t => t.SportId == 2);
        }

        [Fact]
        public async Task ReturnsSingleTeamWhenOnlyOneExistsForSport()
        {
            await SeedSportsAsync();
            _context.Teams.Add(CreateTeam(1, "Lone Ranger FC", sportId: 1));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetBySportAsync(sportId: 1);

            result.Should().ContainSingle()
                .Which.Name.Should().Be("Lone Ranger FC");
        }
    }

    #endregion

    #region GetTeamWithStatsAsync

    public class GetTeamWithStatsAsyncTests : TeamRepositoryTests
    {
        [Fact]
        public async Task ReturnsTeamWithSportIncluded()
        {
            await SeedSportsAsync();
            _context.Teams.Add(CreateTeam(1, "Arsenal", sportId: 1));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetTeamWithStatsAsync(teamId: 1);

            result.Should().NotBeNull();
            result.Sport.Should().NotBeNull();
            result.Sport.SportId.Should().Be(1);
        }

        [Fact]
        public async Task ReturnsNullWhenTeamDoesNotExist()
        {
            var result = await _sut.GetTeamWithStatsAsync(teamId: 999);

            result.Should().BeNull();
        }

        [Fact]
        public async Task ReturnsCorrectTeamById()
        {
            await SeedSportsAsync();
            _context.Teams.AddRange(
                CreateTeam(1, "Team One", sportId: 1),
                CreateTeam(2, "Team Two", sportId: 2));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetTeamWithStatsAsync(teamId: 2);

            result.Should().NotBeNull();
            result.TeamId.Should().Be(2);
            result.Name.Should().Be("Team Two");
        }

        [Fact]
        public async Task SportNavigationPropertyReflectsCorrectSport()
        {
            await SeedSportsAsync();
            _context.Teams.Add(CreateTeam(1, "Hoops Squad", sportId: 2));
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetTeamWithStatsAsync(teamId: 1);

            result!.Sport.Name.Should().Be(SportType.NBA);
        }

        [Fact]
        public async Task ReturnsNullWhenDbIsEmpty()
        {
            var result = await _sut.GetTeamWithStatsAsync(teamId: 1);

            result.Should().BeNull();
        }
    }

    #endregion
}