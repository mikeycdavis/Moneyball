using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moneyball.Core.Entities;
using Moneyball.Core.Interfaces.ExternalAPIs;
using Moneyball.Core.Interfaces.Repositories;
using Moneyball.Infrastructure.ExternalServices;
using Moq;
using System.Linq.Expressions;
using Moneyball.Core.DTOs;

namespace Moneyball.Tests.ExternalServices;

public class DataIngestionService_IngestNBATeamsAsyncTests
{
    private readonly Mock<IMoneyballRepository> _moneyballRepository;
    private readonly Mock<ISportsDataService> _mockSportsDataService;
    private readonly Mock<IOddsDataService> _mockOddsDataService;
    private readonly Mock<ILogger<DataIngestionService>> _mockLogger;
    private readonly DataIngestionService _service;

    // Mock repositories
    private readonly Mock<IRepository<Sport>> _mockSportsRepo;
    private readonly Mock<ITeamRepository> _mockTeamsRepo;

    public DataIngestionService_IngestNBATeamsAsyncTests()
    {
        _moneyballRepository = new Mock<IMoneyballRepository>();
        _mockSportsDataService = new Mock<ISportsDataService>();
        _mockOddsDataService = new Mock<IOddsDataService>();
        _mockLogger = new Mock<ILogger<DataIngestionService>>();

        _mockSportsRepo = new Mock<IRepository<Sport>>();
        _mockTeamsRepo = new Mock<ITeamRepository>();

        // Wire up unit of work
        _moneyballRepository.Setup(u => u.Sports).Returns(_mockSportsRepo.Object);
        _moneyballRepository.Setup(u => u.Teams).Returns(_mockTeamsRepo.Object);

        _service = new DataIngestionService(
            _moneyballRepository.Object,
            _mockSportsDataService.Object,
            _mockOddsDataService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task IngestNBATeamsAsync_FirstRun_Creates30Teams()
    {
        // Arrange
        var nbaSport = new Sport { SportId = 1, Name = "NBA" };

        _mockSportsRepo
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Sport, bool>>>()))
            .ReturnsAsync(nbaSport);

        var apiTeams = GenerateTestNBATeams(30);
        _mockSportsDataService
            .Setup(s => s.GetNBATeamsAsync())
            .ReturnsAsync(apiTeams);

        // All teams are new (GetByExternalIdAsync returns null)
        _mockTeamsRepo
            .Setup(r => r.GetByExternalIdAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync((Team?)null);

        var addedTeams = new List<Team>();
        _mockTeamsRepo
            .Setup(r => r.AddAsync(It.IsAny<Team>()))
            .Callback<Team>(t => addedTeams.Add(t))
            .ReturnsAsync((Team t) => t);

        _moneyballRepository
            .Setup(u => u.SaveChangesAsync())
            .ReturnsAsync(30);

        _mockTeamsRepo
            .Setup(r => r.CountAsync(It.IsAny<Expression<Func<Team, bool>>>()))
            .ReturnsAsync(30);

        // Act
        await _service.IngestNBATeamsAsync();

        // Assert
        addedTeams.Should().HaveCount(30, "30 teams should be added on first run");
        addedTeams.Should().AllSatisfy(t =>
        {
            t.SportId.Should().Be(1);
            t.ExternalId.Should().NotBeNullOrEmpty();
            t.Name.Should().NotBeNullOrEmpty();
        });

        _moneyballRepository.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task IngestNBATeamsAsync_SecondRun_NoChanges_UpdatesNothing()
    {
        // Arrange
        var nbaSport = new Sport { SportId = 1, Name = "NBA" };

        _mockSportsRepo
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Sport, bool>>>()))
            .ReturnsAsync(nbaSport);

        var apiTeams = GenerateTestNBATeams(30);
        _mockSportsDataService
            .Setup(s => s.GetNBATeamsAsync())
            .ReturnsAsync(apiTeams);

        // All teams exist with matching data
        _mockTeamsRepo
            .Setup(r => r.GetByExternalIdAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync((string externalId, int sportId) =>
            {
                var apiTeam = apiTeams.First(t => t.Id == externalId);
                return new Team
                {
                    TeamId = 1,
                    SportId = sportId,
                    ExternalId = apiTeam.Id,
                    Name = apiTeam.Name,
                    Abbreviation = apiTeam.Alias,
                    City = apiTeam.Market
                };
            });

        _moneyballRepository
            .Setup(u => u.SaveChangesAsync())
            .ReturnsAsync(0); // No changes

        _mockTeamsRepo
            .Setup(r => r.CountAsync(It.IsAny<Expression<Func<Team, bool>>>()))
            .ReturnsAsync(30);

        // Act
        await _service.IngestNBATeamsAsync();

        // Assert
        _mockTeamsRepo.Verify(r => r.AddAsync(It.IsAny<Team>()), Times.Never,
            "no teams should be added when all exist");
        _mockTeamsRepo.Verify(r => r.UpdateAsync(It.IsAny<Team>()), Times.Never,
            "no teams should be updated when data matches");
        _moneyballRepository.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task IngestNBATeamsAsync_NameChanged_UpdatesTeam()
    {
        // Arrange
        var nbaSport = new Sport { SportId = 1, Name = "NBA" };

        _mockSportsRepo
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Sport, bool>>>()))
            .ReturnsAsync(nbaSport);

        var apiTeams = new List<NBATeamInfo>
        {
            new()
            {
                Id = "lakers-id",
                Name = "Los Angeles Lakers Updated", // Changed
                Alias = "LAL",
                Market = "Los Angeles"
            }
        };

        _mockSportsDataService
            .Setup(s => s.GetNBATeamsAsync())
            .ReturnsAsync(apiTeams);

        var existingTeam = new Team
        {
            TeamId = 1,
            SportId = 1,
            ExternalId = "lakers-id",
            Name = "Los Angeles Lakers", // Old name
            Abbreviation = "LAL",
            City = "Los Angeles"
        };

        _mockTeamsRepo
            .Setup(r => r.GetByExternalIdAsync("lakers-id", 1))
            .ReturnsAsync(existingTeam);

        var updatedTeams = new List<Team>();
        _mockTeamsRepo
            .Setup(r => r.UpdateAsync(It.IsAny<Team>()))
            .Callback<Team>(t => updatedTeams.Add(t))
            .Returns(Task.CompletedTask);

        _moneyballRepository
            .Setup(u => u.SaveChangesAsync())
            .ReturnsAsync(1);

        _mockTeamsRepo
            .Setup(r => r.CountAsync(It.IsAny<Expression<Func<Team, bool>>>()))
            .ReturnsAsync(1);

        // Act
        await _service.IngestNBATeamsAsync();

        // Assert
        updatedTeams.Should().ContainSingle("one team should be updated");
        updatedTeams[0].Name.Should().Be("Los Angeles Lakers Updated");
        _mockTeamsRepo.Verify(r => r.UpdateAsync(It.IsAny<Team>()), Times.Once);
    }

    [Fact]
    public async Task IngestNBATeamsAsync_AbbreviationChanged_UpdatesTeam()
    {
        // Arrange
        var nbaSport = new Sport { SportId = 1, Name = "NBA" };

        _mockSportsRepo
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Sport, bool>>>()))
            .ReturnsAsync(nbaSport);

        var apiTeams = new List<NBATeamInfo>
        {
            new()
            {
                Id = "lakers-id",
                Name = "Los Angeles Lakers",
                Alias = "LAK", // Changed from LAL
                Market = "Los Angeles"
            }
        };

        _mockSportsDataService
            .Setup(s => s.GetNBATeamsAsync())
            .ReturnsAsync(apiTeams);

        var existingTeam = new Team
        {
            TeamId = 1,
            SportId = 1,
            ExternalId = "lakers-id",
            Name = "Los Angeles Lakers",
            Abbreviation = "LAL", // Old abbreviation
            City = "Los Angeles"
        };

        _mockTeamsRepo
            .Setup(r => r.GetByExternalIdAsync("lakers-id", 1))
            .ReturnsAsync(existingTeam);

        var updatedTeams = new List<Team>();
        _mockTeamsRepo
            .Setup(r => r.UpdateAsync(It.IsAny<Team>()))
            .Callback<Team>(t => updatedTeams.Add(t))
            .Returns(Task.CompletedTask);

        _moneyballRepository
            .Setup(u => u.SaveChangesAsync())
            .ReturnsAsync(1);

        _mockTeamsRepo
            .Setup(r => r.CountAsync(It.IsAny<Expression<Func<Team, bool>>>()))
            .ReturnsAsync(1);

        // Act
        await _service.IngestNBATeamsAsync();

        // Assert
        updatedTeams.Should().ContainSingle();
        updatedTeams[0].Abbreviation.Should().Be("LAK");
    }

    [Fact]
    public async Task IngestNBATeamsAsync_CityChanged_UpdatesTeam()
    {
        // Arrange
        var nbaSport = new Sport { SportId = 1, Name = "NBA" };

        _mockSportsRepo
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Sport, bool>>>()))
            .ReturnsAsync(nbaSport);

        var apiTeams = new List<NBATeamInfo>
        {
            new()
            {
                Id = "team-id",
                Name = "Team Name",
                Alias = "TM",
                Market = "New City" // Changed
            }
        };

        _mockSportsDataService
            .Setup(s => s.GetNBATeamsAsync())
            .ReturnsAsync(apiTeams);

        var existingTeam = new Team
        {
            TeamId = 1,
            SportId = 1,
            ExternalId = "team-id",
            Name = "Team Name",
            Abbreviation = "TM",
            City = "Old City" // Old city
        };

        _mockTeamsRepo
            .Setup(r => r.GetByExternalIdAsync("team-id", 1))
            .ReturnsAsync(existingTeam);

        var updatedTeams = new List<Team>();
        _mockTeamsRepo
            .Setup(r => r.UpdateAsync(It.IsAny<Team>()))
            .Callback<Team>(t => updatedTeams.Add(t))
            .Returns(Task.CompletedTask);

        _moneyballRepository
            .Setup(u => u.SaveChangesAsync())
            .ReturnsAsync(1);

        _mockTeamsRepo
            .Setup(r => r.CountAsync(It.IsAny<Expression<Func<Team, bool>>>()))
            .ReturnsAsync(1);

        // Act
        await _service.IngestNBATeamsAsync();

        // Assert
        updatedTeams.Should().ContainSingle();
        updatedTeams[0].City.Should().Be("New City");
    }

    [Fact]
    public async Task IngestNBATeamsAsync_MultipleFieldsChanged_UpdatesSingleTime()
    {
        // Arrange
        var nbaSport = new Sport { SportId = 1, Name = "NBA" };

        _mockSportsRepo
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Sport, bool>>>()))
            .ReturnsAsync(nbaSport);

        var apiTeams = new List<NBATeamInfo>
        {
            new()
            {
                Id = "team-id",
                Name = "New Name",
                Alias = "NN",
                Market = "New Market"
            }
        };

        _mockSportsDataService
            .Setup(s => s.GetNBATeamsAsync())
            .ReturnsAsync(apiTeams);

        var existingTeam = new Team
        {
            TeamId = 1,
            SportId = 1,
            ExternalId = "team-id",
            Name = "Old Name",
            Abbreviation = "ON",
            City = "Old Market"
        };

        _mockTeamsRepo
            .Setup(r => r.GetByExternalIdAsync("team-id", 1))
            .ReturnsAsync(existingTeam);

        _mockTeamsRepo
            .Setup(r => r.UpdateAsync(It.IsAny<Team>()))
            .Returns(Task.CompletedTask);

        _moneyballRepository
            .Setup(u => u.SaveChangesAsync())
            .ReturnsAsync(1);

        _mockTeamsRepo
            .Setup(r => r.CountAsync(It.IsAny<Expression<Func<Team, bool>>>()))
            .ReturnsAsync(1);

        // Act
        await _service.IngestNBATeamsAsync();

        // Assert
        _mockTeamsRepo.Verify(r => r.UpdateAsync(It.IsAny<Team>()), Times.Once,
            "should update only once even when multiple fields change");
        existingTeam.Name.Should().Be("New Name");
        existingTeam.Abbreviation.Should().Be("NN");
        existingTeam.City.Should().Be("New Market");
    }

    [Fact]
    public async Task IngestNBATeamsAsync_NBASportNotFound_ThrowsException()
    {
        // Arrange
        _mockSportsRepo
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Sport, bool>>>()))
            .ReturnsAsync((Sport?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.IngestNBATeamsAsync());

        exception.Message.Should().Contain("NBA sport not found");
    }

    [Fact]
    public async Task IngestNBATeamsAsync_APIReturnsEmpty_LogsWarningAndReturns()
    {
        // Arrange
        var nbaSport = new Sport { SportId = 1, Name = "NBA" };

        _mockSportsRepo
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Sport, bool>>>()))
            .ReturnsAsync(nbaSport);

        _mockSportsDataService
            .Setup(s => s.GetNBATeamsAsync())
            .ReturnsAsync(new List<NBATeamInfo>()); // Empty list

        // Act
        await _service.IngestNBATeamsAsync();

        // Assert
        _mockTeamsRepo.Verify(r => r.AddAsync(It.IsAny<Team>()), Times.Never);
        _moneyballRepository.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task IngestNBATeamsAsync_TeamWithMissingId_SkipsTeam()
    {
        // Arrange
        var nbaSport = new Sport { SportId = 1, Name = "NBA" };

        _mockSportsRepo
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Sport, bool>>>()))
            .ReturnsAsync(nbaSport);

        var apiTeams = new List<NBATeamInfo>
        {
            new() { Id = "", Name = "Team 1", Alias = "T1", Market = "City" }, // Invalid - empty ID
            new() { Id = "valid-id", Name = "Team 2", Alias = "T2", Market = "City" } // Valid
        };

        _mockSportsDataService
            .Setup(s => s.GetNBATeamsAsync())
            .ReturnsAsync(apiTeams);

        _mockTeamsRepo
            .Setup(r => r.GetByExternalIdAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync((Team?)null);

        var addedTeams = new List<Team>();
        _mockTeamsRepo
            .Setup(r => r.AddAsync(It.IsAny<Team>()))
            .Callback<Team>(t => addedTeams.Add(t))
            .ReturnsAsync((Team t) => t);

        _moneyballRepository
            .Setup(u => u.SaveChangesAsync())
            .ReturnsAsync(1);

        _mockTeamsRepo
            .Setup(r => r.CountAsync(It.IsAny<Expression<Func<Team, bool>>>()))
            .ReturnsAsync(1);

        // Act
        await _service.IngestNBATeamsAsync();

        // Assert
        addedTeams.Should().ContainSingle("only the valid team should be added");
        addedTeams[0].ExternalId.Should().Be("valid-id");
    }

    [Fact]
    public async Task IngestNBATeamsAsync_TeamWithMissingName_SkipsTeam()
    {
        // Arrange
        var nbaSport = new Sport { SportId = 1, Name = "NBA" };

        _mockSportsRepo
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Sport, bool>>>()))
            .ReturnsAsync(nbaSport);

        var apiTeams = new List<NBATeamInfo>
        {
            new() { Id = "id1", Name = "", Alias = "T1", Market = "City" }, // Invalid - empty name
            new() { Id = "id2", Name = "Valid Team", Alias = "T2", Market = "City" } // Valid
        };

        _mockSportsDataService
            .Setup(s => s.GetNBATeamsAsync())
            .ReturnsAsync(apiTeams);

        _mockTeamsRepo
            .Setup(r => r.GetByExternalIdAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync((Team?)null);

        var addedTeams = new List<Team>();
        _mockTeamsRepo
            .Setup(r => r.AddAsync(It.IsAny<Team>()))
            .Callback<Team>(t => addedTeams.Add(t))
            .ReturnsAsync((Team t) => t);

        _moneyballRepository
            .Setup(u => u.SaveChangesAsync())
            .ReturnsAsync(1);

        _mockTeamsRepo
            .Setup(r => r.CountAsync(It.IsAny<Expression<Func<Team, bool>>>()))
            .ReturnsAsync(1);

        // Act
        await _service.IngestNBATeamsAsync();

        // Assert
        addedTeams.Should().ContainSingle();
        addedTeams[0].Name.Should().Be("Valid Team");
    }

    [Fact]
    public async Task IngestNBATeamsAsync_MixedNewAndExisting_HandlesCorrectly()
    {
        // Arrange
        var nbaSport = new Sport { SportId = 1, Name = "NBA" };

        _mockSportsRepo
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Sport, bool>>>()))
            .ReturnsAsync(nbaSport);

        var apiTeams = new List<NBATeamInfo>
        {
            new() { Id = "existing-id", Name = "Existing Team", Alias = "ET", Market = "City1" },
            new() { Id = "new-id", Name = "New Team", Alias = "NT", Market = "City2" }
        };

        _mockSportsDataService
            .Setup(s => s.GetNBATeamsAsync())
            .ReturnsAsync(apiTeams);

        _mockTeamsRepo
            .Setup(r => r.GetByExternalIdAsync("existing-id", 1))
            .ReturnsAsync(new Team
            {
                TeamId = 1,
                SportId = 1,
                ExternalId = "existing-id",
                Name = "Existing Team",
                Abbreviation = "ET",
                City = "City1"
            });

        _mockTeamsRepo
            .Setup(r => r.GetByExternalIdAsync("new-id", 1))
            .ReturnsAsync((Team?)null);

        var addedTeams = new List<Team>();
        _mockTeamsRepo
            .Setup(r => r.AddAsync(It.IsAny<Team>()))
            .Callback<Team>(t => addedTeams.Add(t))
            .ReturnsAsync((Team t) => t);

        _moneyballRepository
            .Setup(u => u.SaveChangesAsync())
            .ReturnsAsync(1);

        _mockTeamsRepo
            .Setup(r => r.CountAsync(It.IsAny<Expression<Func<Team, bool>>>()))
            .ReturnsAsync(2);

        // Act
        await _service.IngestNBATeamsAsync();

        // Assert
        addedTeams.Should().ContainSingle("only the new team should be added");
        addedTeams[0].ExternalId.Should().Be("new-id");
        _mockTeamsRepo.Verify(r => r.UpdateAsync(It.IsAny<Team>()), Times.Never,
            "existing team has no changes so should not be updated");
    }

    // Helper method to generate test data
    private List<NBATeamInfo> GenerateTestNBATeams(int count)
    {
        var teams = new List<NBATeamInfo>();
        for (int i = 1; i <= count; i++)
        {
            teams.Add(new NBATeamInfo
            {
                Id = $"team-{i:D3}",
                Name = $"Team {i}",
                Alias = $"T{i}",
                Market = $"City {i}"
            });
        }
        return teams;
    }
}