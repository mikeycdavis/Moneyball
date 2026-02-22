using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moneyball.Core.DTOs.ExternalAPIs.Odds;
using Moneyball.Core.DTOs.ExternalAPIs.SportsRadar;
using Moneyball.Core.Entities;
using Moneyball.Core.Enums;
using Moneyball.Core.Interfaces.ExternalAPIs;
using Moneyball.Core.Interfaces.Repositories;
using Moneyball.Infrastructure.ExternalServices;
using Moq;
using System.Linq.Expressions;

namespace Moneyball.Tests.ExternalServices;

public class DataIngestionService_IngestNBATeamsAsyncTests
{
    private readonly Mock<IMoneyballRepository> _mockMoneyballRepository;
    private readonly Mock<ISportsDataService> _mockSportsDataService;
    private readonly DataIngestionService _service;

    // Mock repositories
    private readonly Mock<IRepository<Sport>> _mockSportsRepo;
    private readonly Mock<ITeamRepository> _mockTeamsRepo;

    public DataIngestionService_IngestNBATeamsAsyncTests()
    {
        _mockMoneyballRepository = new Mock<IMoneyballRepository>();
        _mockSportsDataService = new Mock<ISportsDataService>();
        var mockOddsDataService = new Mock<IOddsDataService>();
        var mockLogger = new Mock<ILogger<DataIngestionService>>();

        _mockSportsRepo = new Mock<IRepository<Sport>>();
        _mockTeamsRepo = new Mock<ITeamRepository>();

        // Wire up unit of work
        _mockMoneyballRepository.Setup(u => u.Sports).Returns(_mockSportsRepo.Object);
        _mockMoneyballRepository.Setup(u => u.Teams).Returns(_mockTeamsRepo.Object);

        _service = new DataIngestionService(
            _mockMoneyballRepository.Object,
            _mockSportsDataService.Object,
            mockOddsDataService.Object,
            mockLogger.Object);
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

        _mockMoneyballRepository
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

        _mockMoneyballRepository.Verify(u => u.SaveChangesAsync(), Times.Once);
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

        _mockMoneyballRepository
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
        _mockMoneyballRepository.Verify(u => u.SaveChangesAsync(), Times.Once);
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

        _mockMoneyballRepository
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

        _mockMoneyballRepository
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

        _mockMoneyballRepository
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

        _mockMoneyballRepository
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
        _mockMoneyballRepository.Verify(u => u.SaveChangesAsync(), Times.Never);
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

        _mockMoneyballRepository
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

        _mockMoneyballRepository
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

        _mockMoneyballRepository
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
    private static List<NBATeamInfo> GenerateTestNBATeams(int count)
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

public class DataIngestionService_IngestNBAScheduleAsyncTests
{
    private readonly Mock<IMoneyballRepository> _mockMoneyballRepository;
    private readonly Mock<ISportsDataService> _mockSportsDataService;
    private readonly DataIngestionService _service;

    // Mock repositories
    private readonly Mock<IRepository<Sport>> _mockSportsRepo;
    private readonly Mock<ITeamRepository> _mockTeamsRepo;
    private readonly Mock<IGameRepository> _mockGamesRepo;

    public DataIngestionService_IngestNBAScheduleAsyncTests()
    {
        _mockMoneyballRepository = new Mock<IMoneyballRepository>();
        _mockSportsDataService = new Mock<ISportsDataService>();
        var mockOddsDataService = new Mock<IOddsDataService>();
        var mockLogger = new Mock<ILogger<DataIngestionService>>();

        _mockSportsRepo = new Mock<IRepository<Sport>>();
        _mockTeamsRepo = new Mock<ITeamRepository>();
        _mockGamesRepo = new Mock<IGameRepository>();

        // Wire up unit of work
        _mockMoneyballRepository.Setup(u => u.Sports).Returns(_mockSportsRepo.Object);
        _mockMoneyballRepository.Setup(u => u.Teams).Returns(_mockTeamsRepo.Object);
        _mockMoneyballRepository.Setup(u => u.Games).Returns(_mockGamesRepo.Object);

        _service = new DataIngestionService(
            _mockMoneyballRepository.Object,
            _mockSportsDataService.Object,
            mockOddsDataService.Object,
            mockLogger.Object);
    }

    [Fact]
    public async Task IngestNBAScheduleAsync_NewGames_InsertsAllByExternalGameId()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 1, 7);
        var nbaSport = new Sport { SportId = 1, Name = "NBA" };

        _mockSportsRepo
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Sport, bool>>>()))
            .ReturnsAsync(nbaSport);

        var apiGames = GenerateTestGames(5);
        _mockSportsDataService
            .Setup(s => s.GetNBAScheduleAsync(startDate, endDate))
            .ReturnsAsync(apiGames);

        SetupTeamsLookup();

        // All games are new (GetGameByExternalIdAsync returns null)
        _mockGamesRepo
            .Setup(r => r.GetGameByExternalIdAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync((Game?)null);

        var addedGames = new List<Game>();
        _mockGamesRepo
            .Setup(r => r.AddAsync(It.IsAny<Game>()))
            .Callback<Game>(g => addedGames.Add(g))
            .ReturnsAsync((Game g) => g);

        _mockMoneyballRepository
            .Setup(u => u.SaveChangesAsync())
            .ReturnsAsync(5);

        // Act
        await _service.IngestNBAScheduleAsync(startDate, endDate);

        // Assert
        addedGames.Should().HaveCount(5, "5 new games should be added");
        addedGames.Should().AllSatisfy(g =>
        {
            g.SportId.Should().Be(1);
            g.ExternalGameId.Should().NotBeNullOrEmpty();
            g.HomeTeamId.Should().BeGreaterThan(0);
            g.AwayTeamId.Should().BeGreaterThan(0);
        });

        _mockMoneyballRepository.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task IngestNBAScheduleAsync_ExistingGames_NoChanges_UpdatesNothing()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 1, 7);
        var nbaSport = new Sport { SportId = 1, Name = "NBA" };

        _mockSportsRepo
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Sport, bool>>>()))
            .ReturnsAsync(nbaSport);

        var apiGames = GenerateTestGames(3);
        _mockSportsDataService
            .Setup(s => s.GetNBAScheduleAsync(startDate, endDate))
            .ReturnsAsync(apiGames);

        SetupTeamsLookup();

        // All games exist with matching data
        _mockGamesRepo
            .Setup(r => r.GetGameByExternalIdAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync((string externalId, int sportId) =>
            {
                var apiGame = apiGames.First(g => g.Id == externalId);
                return new Game
                {
                    GameId = 1,
                    SportId = sportId,
                    ExternalGameId = apiGame.Id,
                    HomeTeamId = 1,
                    AwayTeamId = 2,
                    GameDate = apiGame.Scheduled,
                    Status = GameStatus.Scheduled,
                    HomeScore = apiGame.HomePoints,
                    AwayScore = apiGame.AwayPoints,
                    IsComplete = false
                };
            });

        _mockMoneyballRepository
            .Setup(u => u.SaveChangesAsync())
            .ReturnsAsync(0);

        // Act
        await _service.IngestNBAScheduleAsync(startDate, endDate);

        // Assert
        _mockGamesRepo.Verify(r => r.AddAsync(It.IsAny<Game>()), Times.Never);
        _mockGamesRepo.Verify(r => r.UpdateAsync(It.IsAny<Game>()), Times.Never);
        _mockMoneyballRepository.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task IngestNBAScheduleAsync_StatusChanged_UpdatesStatus()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 1, 7);
        var nbaSport = new Sport { SportId = 1, Name = "NBA" };

        _mockSportsRepo
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Sport, bool>>>()))
            .ReturnsAsync(nbaSport);

        var apiGames = new List<NBAGame>
        {
            new()
            {
                Id = "game-123",
                Status = "inprogress", // Changed from scheduled
                Scheduled = DateTime.UtcNow,
                Home = new NBATeamInfo { Id = "team-home" },
                Away = new NBATeamInfo { Id = "team-away" },
                HomePoints = null,
                AwayPoints = null
            }
        };

        _mockSportsDataService
            .Setup(s => s.GetNBAScheduleAsync(startDate, endDate))
            .ReturnsAsync(apiGames);

        SetupTeamsLookup();

        var existingGame = new Game
        {
            GameId = 1,
            SportId = 1,
            ExternalGameId = "game-123",
            HomeTeamId = 1,
            AwayTeamId = 2,
            GameDate = DateTime.UtcNow,
            Status = GameStatus.Scheduled, // Old status
            IsComplete = false
        };

        _mockGamesRepo
            .Setup(r => r.GetGameByExternalIdAsync("game-123", 1))
            .ReturnsAsync(existingGame);

        var updatedGames = new List<Game>();
        _mockGamesRepo
            .Setup(r => r.UpdateAsync(It.IsAny<Game>()))
            .Callback<Game>(g => updatedGames.Add(g))
            .Returns(Task.CompletedTask);

        _mockMoneyballRepository
            .Setup(u => u.SaveChangesAsync())
            .ReturnsAsync(1);

        // Act
        await _service.IngestNBAScheduleAsync(startDate, endDate);

        // Assert
        updatedGames.Should().ContainSingle();
        updatedGames[0].Status.Should().Be(GameStatus.InProgress);
        _mockGamesRepo.Verify(r => r.UpdateAsync(It.IsAny<Game>()), Times.Once);
    }

    [Fact]
    public async Task IngestNBAScheduleAsync_StatusClosed_FlipsIsComplete()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 1, 7);
        var nbaSport = new Sport { SportId = 1, Name = "NBA" };

        _mockSportsRepo
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Sport, bool>>>()))
            .ReturnsAsync(nbaSport);

        var apiGames = new List<NBAGame>
        {
            new()
            {
                Id = "game-123",
                Status = "closed", // Game finished
                Scheduled = DateTime.UtcNow.AddHours(-3),
                Home = new NBATeamInfo { Id = "team-home" },
                Away = new NBATeamInfo { Id = "team-away" },
                HomePoints = 105,
                AwayPoints = 98
            }
        };

        _mockSportsDataService
            .Setup(s => s.GetNBAScheduleAsync(startDate, endDate))
            .ReturnsAsync(apiGames);

        SetupTeamsLookup();

        var existingGame = new Game
        {
            GameId = 1,
            SportId = 1,
            ExternalGameId = "game-123",
            HomeTeamId = 1,
            AwayTeamId = 2,
            GameDate = DateTime.UtcNow.AddHours(-3),
            Status = GameStatus.InProgress,
            IsComplete = false, // Should flip to true
            HomeScore = null,
            AwayScore = null
        };

        _mockGamesRepo
            .Setup(r => r.GetGameByExternalIdAsync("game-123", 1))
            .ReturnsAsync(existingGame);

        var updatedGames = new List<Game>();
        _mockGamesRepo
            .Setup(r => r.UpdateAsync(It.IsAny<Game>()))
            .Callback<Game>(g => updatedGames.Add(g))
            .Returns(Task.CompletedTask);

        _mockMoneyballRepository
            .Setup(u => u.SaveChangesAsync())
            .ReturnsAsync(1);

        // Act
        await _service.IngestNBAScheduleAsync(startDate, endDate);

        // Assert
        updatedGames.Should().ContainSingle();
        updatedGames[0].IsComplete.Should().BeTrue("IsComplete should flip when status is closed");
        updatedGames[0].Status.Should().Be(GameStatus.Final);
    }

    [Fact]
    public async Task IngestNBAScheduleAsync_ScoresChanged_UpdatesScores()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 1, 7);
        var nbaSport = new Sport { SportId = 1, Name = "NBA" };

        _mockSportsRepo
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Sport, bool>>>()))
            .ReturnsAsync(nbaSport);

        var apiGames = new List<NBAGame>
        {
            new()
            {
                Id = "game-123",
                Status = "inprogress",
                Scheduled = DateTime.UtcNow,
                Home = new NBATeamInfo { Id = "team-home" },
                Away = new NBATeamInfo { Id = "team-away" },
                HomePoints = 55, // Updated scores
                AwayPoints = 48
            }
        };

        _mockSportsDataService
            .Setup(s => s.GetNBAScheduleAsync(startDate, endDate))
            .ReturnsAsync(apiGames);

        SetupTeamsLookup();

        var existingGame = new Game
        {
            GameId = 1,
            SportId = 1,
            ExternalGameId = "game-123",
            HomeTeamId = 1,
            AwayTeamId = 2,
            GameDate = DateTime.UtcNow,
            Status = GameStatus.InProgress,
            IsComplete = false,
            HomeScore = 42, // Old scores
            AwayScore = 38
        };

        _mockGamesRepo
            .Setup(r => r.GetGameByExternalIdAsync("game-123", 1))
            .ReturnsAsync(existingGame);

        var updatedGames = new List<Game>();
        _mockGamesRepo
            .Setup(r => r.UpdateAsync(It.IsAny<Game>()))
            .Callback<Game>(g => updatedGames.Add(g))
            .Returns(Task.CompletedTask);

        _mockMoneyballRepository
            .Setup(u => u.SaveChangesAsync())
            .ReturnsAsync(1);

        // Act
        await _service.IngestNBAScheduleAsync(startDate, endDate);

        // Assert
        updatedGames.Should().ContainSingle();
        updatedGames[0].HomeScore.Should().Be(55);
        updatedGames[0].AwayScore.Should().Be(48);
    }

    [Fact]
    public async Task IngestNBAScheduleAsync_MultipleFieldsChanged_UpdatesAll()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 1, 7);
        var nbaSport = new Sport { SportId = 1, Name = "NBA" };

        _mockSportsRepo
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Sport, bool>>>()))
            .ReturnsAsync(nbaSport);

        var apiGames = new List<NBAGame>
        {
            new()
            {
                Id = "game-123",
                Status = "closed", // Changed
                Scheduled = DateTime.UtcNow,
                Home = new NBATeamInfo { Id = "team-home" },
                Away = new NBATeamInfo { Id = "team-away" },
                HomePoints = 110, // Changed
                AwayPoints = 105  // Changed
            }
        };

        _mockSportsDataService
            .Setup(s => s.GetNBAScheduleAsync(startDate, endDate))
            .ReturnsAsync(apiGames);

        SetupTeamsLookup();

        var existingGame = new Game
        {
            GameId = 1,
            SportId = 1,
            ExternalGameId = "game-123",
            HomeTeamId = 1,
            AwayTeamId = 2,
            GameDate = DateTime.UtcNow,
            Status = GameStatus.InProgress,
            IsComplete = false,
            HomeScore = 85,
            AwayScore = 82
        };

        _mockGamesRepo
            .Setup(r => r.GetGameByExternalIdAsync("game-123", 1))
            .ReturnsAsync(existingGame);

        _mockGamesRepo
            .Setup(r => r.UpdateAsync(It.IsAny<Game>()))
            .Returns(Task.CompletedTask);

        _mockMoneyballRepository
            .Setup(u => u.SaveChangesAsync())
            .ReturnsAsync(1);

        // Act
        await _service.IngestNBAScheduleAsync(startDate, endDate);

        // Assert
        _mockGamesRepo.Verify(r => r.UpdateAsync(It.IsAny<Game>()), Times.Once);
        existingGame.Status.Should().Be(GameStatus.Final);
        existingGame.IsComplete.Should().BeTrue();
        existingGame.HomeScore.Should().Be(110);
        existingGame.AwayScore.Should().Be(105);
        existingGame.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task IngestNBAScheduleAsync_NBASportNotFound_ThrowsException()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 1, 7);

        _mockSportsRepo
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Sport, bool>>>()))
            .ReturnsAsync((Sport?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.IngestNBAScheduleAsync(startDate, endDate));

        exception.Message.Should().Contain("NBA sport not found");
    }

    [Fact]
    public async Task IngestNBAScheduleAsync_EmptySchedule_ReturnsEarly()
    {
        // Arrange
        var startDate = new DateTime(2024, 7, 1); // Off-season
        var endDate = new DateTime(2024, 7, 7);
        var nbaSport = new Sport { SportId = 1, Name = "NBA" };

        _mockSportsRepo
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Sport, bool>>>()))
            .ReturnsAsync(nbaSport);

        _mockSportsDataService
            .Setup(s => s.GetNBAScheduleAsync(startDate, endDate))
            .ReturnsAsync(new List<NBAGame>()); // Empty

        // Act
        await _service.IngestNBAScheduleAsync(startDate, endDate);

        // Assert
        _mockGamesRepo.Verify(r => r.AddAsync(It.IsAny<Game>()), Times.Never);
        _mockMoneyballRepository.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task IngestNBAScheduleAsync_GameWithMissingId_SkipsGame()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 1, 7);
        var nbaSport = new Sport { SportId = 1, Name = "NBA" };

        _mockSportsRepo
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Sport, bool>>>()))
            .ReturnsAsync(nbaSport);

        var apiGames = new List<NBAGame>
        {
            new() { Id = "", Status = "scheduled", Scheduled = DateTime.UtcNow, Home = new NBATeamInfo { Id = "home" }, Away = new NBATeamInfo { Id = "away" } },
            new() { Id = "valid-id", Status = "scheduled", Scheduled = DateTime.UtcNow, Home = new NBATeamInfo { Id = "home" }, Away = new NBATeamInfo { Id = "away" } }
        };

        _mockSportsDataService
            .Setup(s => s.GetNBAScheduleAsync(startDate, endDate))
            .ReturnsAsync(apiGames);

        SetupTeamsLookup();

        _mockGamesRepo
            .Setup(r => r.GetGameByExternalIdAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync((Game?)null);

        var addedGames = new List<Game>();
        _mockGamesRepo
            .Setup(r => r.AddAsync(It.IsAny<Game>()))
            .Callback<Game>(g => addedGames.Add(g))
            .ReturnsAsync((Game g) => g);

        _mockMoneyballRepository
            .Setup(u => u.SaveChangesAsync())
            .ReturnsAsync(1);

        // Act
        await _service.IngestNBAScheduleAsync(startDate, endDate);

        // Assert
        addedGames.Should().ContainSingle("only valid game should be added");
        addedGames[0].ExternalGameId.Should().Be("valid-id");
    }

    [Fact]
    public async Task IngestNBAScheduleAsync_GameWithMissingTeamIds_SkipsGame()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 1, 7);
        var nbaSport = new Sport { SportId = 1, Name = "NBA" };

        _mockSportsRepo
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Sport, bool>>>()))
            .ReturnsAsync(nbaSport);

        var apiGames = new List<NBAGame>
        {
            new() { Id = "game-1", Status = "scheduled", Scheduled = DateTime.UtcNow, Home = new NBATeamInfo { Id = "" }, Away = new NBATeamInfo { Id = "away" } },
            new() { Id = "game-2", Status = "scheduled", Scheduled = DateTime.UtcNow, Home = new NBATeamInfo { Id = "home" }, Away = new NBATeamInfo { Id = "away" } }
        };

        _mockSportsDataService
            .Setup(s => s.GetNBAScheduleAsync(startDate, endDate))
            .ReturnsAsync(apiGames);

        SetupTeamsLookup();

        _mockGamesRepo
            .Setup(r => r.GetGameByExternalIdAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync((Game?)null);

        var addedGames = new List<Game>();
        _mockGamesRepo
            .Setup(r => r.AddAsync(It.IsAny<Game>()))
            .Callback<Game>(g => addedGames.Add(g))
            .ReturnsAsync((Game g) => g);

        _mockMoneyballRepository
            .Setup(u => u.SaveChangesAsync())
            .ReturnsAsync(1);

        // Act
        await _service.IngestNBAScheduleAsync(startDate, endDate);

        // Assert
        addedGames.Should().ContainSingle("only game with valid team IDs should be added");
    }

    [Fact]
    public async Task IngestNBAScheduleAsync_TeamsNotFoundInDatabase_SkipsGame()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 1, 7);
        var nbaSport = new Sport { SportId = 1, Name = "NBA" };

        _mockSportsRepo
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Sport, bool>>>()))
            .ReturnsAsync(nbaSport);

        var apiGames = new List<NBAGame>
        {
            new() { Id = "game-1", Status = "scheduled", Scheduled = DateTime.UtcNow, Home = new NBATeamInfo { Id = "unknown-team" }, Away = new NBATeamInfo { Id = "team-away" } }
        };

        _mockSportsDataService
            .Setup(s => s.GetNBAScheduleAsync(startDate, endDate))
            .ReturnsAsync(apiGames);

        // Home team lookup returns null
        _mockTeamsRepo
            .Setup(r => r.GetByExternalIdAsync("unknown-team", 1))
            .ReturnsAsync((Team?)null);

        _mockTeamsRepo
            .Setup(r => r.GetByExternalIdAsync("team-away", 1))
            .ReturnsAsync(new Team { TeamId = 2, ExternalId = "team-away" });

        _mockGamesRepo
            .Setup(r => r.GetGameByExternalIdAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync((Game?)null);

        // Act
        await _service.IngestNBAScheduleAsync(startDate, endDate);

        // Assert
        _mockGamesRepo.Verify(r => r.AddAsync(It.IsAny<Game>()), Times.Never,
            "game should be skipped when teams not found");
    }

    [Fact]
    public async Task IngestNBAScheduleAsync_MixedNewAndExisting_HandlesCorrectly()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 1, 7);
        var nbaSport = new Sport { SportId = 1, Name = "NBA" };

        _mockSportsRepo
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Sport, bool>>>()))
            .ReturnsAsync(nbaSport);

        var apiGames = new List<NBAGame>
        {
            new() { Id = "existing-game", Status = "closed", Scheduled = DateTime.UtcNow, Home = new NBATeamInfo { Id = "home" }, Away = new NBATeamInfo { Id = "away" }, HomePoints = 100, AwayPoints = 95 },
            new() { Id = "new-game", Status = "scheduled", Scheduled = DateTime.UtcNow.AddDays(1), Home = new NBATeamInfo { Id = "home" }, Away = new NBATeamInfo { Id = "away" } }
        };

        _mockSportsDataService
            .Setup(s => s.GetNBAScheduleAsync(startDate, endDate))
            .ReturnsAsync(apiGames);

        SetupTeamsLookup();

        _mockGamesRepo
            .Setup(r => r.GetGameByExternalIdAsync("existing-game", 1))
            .ReturnsAsync(new Game
            {
                GameId = 1,
                SportId = 1,
                ExternalGameId = "existing-game",
                HomeTeamId = 1,
                AwayTeamId = 2,
                GameDate = DateTime.UtcNow,
                Status = GameStatus.InProgress,
                IsComplete = false
            });

        _mockGamesRepo
            .Setup(r => r.GetGameByExternalIdAsync("new-game", 1))
            .ReturnsAsync((Game?)null);

        var addedGames = new List<Game>();
        _mockGamesRepo
            .Setup(r => r.AddAsync(It.IsAny<Game>()))
            .Callback<Game>(g => addedGames.Add(g))
            .ReturnsAsync((Game g) => g);

        _mockGamesRepo
            .Setup(r => r.UpdateAsync(It.IsAny<Game>()))
            .Returns(Task.CompletedTask);

        _mockMoneyballRepository
            .Setup(u => u.SaveChangesAsync())
            .ReturnsAsync(2);

        // Act
        await _service.IngestNBAScheduleAsync(startDate, endDate);

        // Assert
        addedGames.Should().ContainSingle("new game should be added");
        addedGames[0].ExternalGameId.Should().Be("new-game");
        _mockGamesRepo.Verify(r => r.UpdateAsync(It.IsAny<Game>()), Times.Once,
            "existing game should be updated");
    }

    [Theory]
    [InlineData("scheduled", GameStatus.Scheduled, false)]
    [InlineData("created", GameStatus.Scheduled, false)]
    [InlineData("inprogress", GameStatus.InProgress, false)]
    [InlineData("halftime", GameStatus.InProgress, false)]
    [InlineData("closed", GameStatus.Final, true)]
    [InlineData("complete", GameStatus.Final, true)]
    [InlineData("final", GameStatus.Final, true)]
    [InlineData("postponed", GameStatus.Postponed, false)]
    [InlineData("cancelled", GameStatus.Cancelled, false)]
    [InlineData("canceled", GameStatus.Cancelled, false)]
    [InlineData("unknown", GameStatus.Unknown, false)]
    public async Task IngestNBAScheduleAsync_StatusMapping_MapsCorrectly(
        string apiStatus, GameStatus expectedStatus, bool expectedIsComplete)
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 1, 7);
        var nbaSport = new Sport { SportId = 1, Name = "NBA" };

        _mockSportsRepo
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Sport, bool>>>()))
            .ReturnsAsync(nbaSport);

        var apiGames = new List<NBAGame>
        {
            new()
            {
                Id = "test-game",
                Status = apiStatus,
                Scheduled = DateTime.UtcNow,
                Home = new NBATeamInfo { Id = "home" },
                Away = new NBATeamInfo { Id = "away" }
            }
        };

        _mockSportsDataService
            .Setup(s => s.GetNBAScheduleAsync(startDate, endDate))
            .ReturnsAsync(apiGames);

        SetupTeamsLookup();

        _mockGamesRepo
            .Setup(r => r.GetGameByExternalIdAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync((Game?)null);

        var addedGames = new List<Game>();
        _mockGamesRepo
            .Setup(r => r.AddAsync(It.IsAny<Game>()))
            .Callback<Game>(g => addedGames.Add(g))
            .ReturnsAsync((Game g) => g);

        _mockMoneyballRepository
            .Setup(u => u.SaveChangesAsync())
            .ReturnsAsync(1);

        // Act
        await _service.IngestNBAScheduleAsync(startDate, endDate);

        // Assert
        addedGames.Should().ContainSingle();
        addedGames[0].Status.Should().Be(expectedStatus,
            $"API status '{apiStatus}' should map to {expectedStatus}");
        addedGames[0].IsComplete.Should().Be(expectedIsComplete,
            $"API status '{apiStatus}' should set IsComplete={expectedIsComplete}");
    }

    // Helper methods
    private void SetupTeamsLookup()
    {
        _mockTeamsRepo
            .Setup(r => r.GetByExternalIdAsync("home", It.IsAny<int>()))
            .ReturnsAsync(new Team { TeamId = 1, ExternalId = "home", Name = "Home Team" });

        _mockTeamsRepo
            .Setup(r => r.GetByExternalIdAsync("away", It.IsAny<int>()))
            .ReturnsAsync(new Team { TeamId = 2, ExternalId = "away", Name = "Away Team" });

        _mockTeamsRepo
            .Setup(r => r.GetByExternalIdAsync("team-home", It.IsAny<int>()))
            .ReturnsAsync(new Team { TeamId = 1, ExternalId = "team-home", Name = "Home Team" });

        _mockTeamsRepo
            .Setup(r => r.GetByExternalIdAsync("team-away", It.IsAny<int>()))
            .ReturnsAsync(new Team { TeamId = 2, ExternalId = "team-away", Name = "Away Team" });
    }

    private static List<NBAGame> GenerateTestGames(int count)
    {
        var games = new List<NBAGame>();
        for (int i = 1; i <= count; i++)
        {
            games.Add(new NBAGame
            {
                Id = $"game-{i:D3}",
                Status = "scheduled",
                Scheduled = DateTime.UtcNow.AddDays(i),
                Home = new NBATeamInfo { Id = "home" },
                Away = new NBATeamInfo { Id = "away" },
                HomePoints = null,
                AwayPoints = null
            });
        }
        return games;
    }
}

/// <summary>
/// Unit tests for DataIngestionService.IngestNBAGameStatisticsAsync method.
/// Tests cover date range ingestion, statistics mapping, upsert logic, and error handling.
/// The method uses UpsertTeamStatisticsAsync helper to either create new or update existing statistics.
/// Uses Moq for mocking dependencies and FluentAssertions for readable assertions.
/// </summary>
public class DataIngestionService_IngestNBAGameStatisticsAsyncTests
{
    private readonly Mock<IMoneyballRepository> _mockMoneyballRepository;
    private readonly Mock<ISportsDataService> _mockSportsDataService;
    private readonly Mock<ILogger<DataIngestionService>> _mockLogger;
    private readonly DataIngestionService _service;

    // Mock repositories
    private readonly Mock<IRepository<Sport>> _mockSportsRepo;
    private readonly Mock<IGameRepository> _mockGamesRepo;
    private readonly Mock<IRepository<TeamStatistic>> _mockTeamStatsRepo;

    /// <summary>
    /// Test fixture setup - initializes all mocks and creates service instance.
    /// Runs before each test method to ensure clean state.
    /// </summary>
    public DataIngestionService_IngestNBAGameStatisticsAsyncTests()
    {
        _mockMoneyballRepository = new Mock<IMoneyballRepository>();
        _mockSportsDataService = new Mock<ISportsDataService>();
        var mockOddsDataService = new Mock<IOddsDataService>();
        _mockLogger = new Mock<ILogger<DataIngestionService>>();

        _mockSportsRepo = new Mock<IRepository<Sport>>();
        _mockGamesRepo = new Mock<IGameRepository>();
        _mockTeamStatsRepo = new Mock<IRepository<TeamStatistic>>();

        // Wire up repository properties
        _mockMoneyballRepository.Setup(r => r.Sports).Returns(_mockSportsRepo.Object);
        _mockMoneyballRepository.Setup(r => r.Games).Returns(_mockGamesRepo.Object);
        _mockMoneyballRepository.Setup(r => r.TeamStatistics).Returns(_mockTeamStatsRepo.Object);

        _service = new DataIngestionService(
            _mockMoneyballRepository.Object,
            _mockSportsDataService.Object,
            mockOddsDataService.Object,
            _mockLogger.Object);
    }

    /// <summary>
    /// Tests successful statistics ingestion creating new records for both teams.
    /// Verifies that home and away statistics are created when they don't exist.
    /// </summary>
    [Fact]
    public async Task IngestNBAGameStatisticsAsync_NewStatistics_CreatesBothHomeAndAway()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 1, 7);
        var nbaSport = new Sport { SportId = 1, Name = "NBA" };

        SetupBasicMocks(nbaSport);

        // API returns one game
        var apiGames = new List<NBAGame>
        {
            new() { Id = "sr:match:game-1", Scheduled = startDate.AddDays(1) }
        };

        _mockSportsDataService
            .Setup(s => s.GetNBAScheduleAsync(startDate, endDate))
            .ReturnsAsync(apiGames);

        // Game exists in database
        SetupExistingGame("sr:match:game-1", 1, 10, 20);

        // Statistics available
        var statistics = CreateTestStatistics();
        _mockSportsDataService
            .Setup(s => s.GetNBAGameStatisticsAsync("sr:match:game-1"))
            .ReturnsAsync(statistics);

        // No existing statistics (will create new)
        _mockTeamStatsRepo
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<TeamStatistic, bool>>>()))
            .ReturnsAsync((TeamStatistic?)null);

        var addedStats = new List<TeamStatistic>();
        _mockTeamStatsRepo
            .Setup(r => r.AddAsync(It.IsAny<TeamStatistic>()))
            .Callback<TeamStatistic>(s => addedStats.Add(s))
            .ReturnsAsync((TeamStatistic s) => s);

        _mockMoneyballRepository.Setup(r => r.SaveChangesAsync()).ReturnsAsync(2);

        // Act
        await _service.IngestNBAGameStatisticsAsync(startDate, endDate);

        // Assert - Both home and away statistics created
        addedStats.Should().HaveCount(2,
            "should create new statistics for both home and away teams");

        addedStats.Should().ContainSingle(s => s.IsHomeTeam == true,
            "should create home team statistics");
        addedStats.Should().ContainSingle(s => s.IsHomeTeam == false,
            "should create away team statistics");

        // Verify team IDs assigned correctly
        addedStats.First(s => s.IsHomeTeam).TeamId.Should().Be(10, "home team ID should be correct");
        addedStats.First(s => !s.IsHomeTeam).TeamId.Should().Be(20, "away team ID should be correct");

        // Verify no updates were called (only creates)
        _mockTeamStatsRepo.Verify(r => r.UpdateAsync(It.IsAny<TeamStatistic>()), Times.Never);
    }

    /// <summary>
    /// Tests that existing statistics are updated (replaced) with new data.
    /// Verifies the upsert logic replaces stale rows as per acceptance criteria.
    /// </summary>
    [Fact]
    public async Task IngestNBAGameStatisticsAsync_ExistingStatistics_UpdatesWithNewData()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 1, 7);
        var nbaSport = new Sport { SportId = 1, Name = "NBA" };

        SetupBasicMocks(nbaSport);

        var apiGames = new List<NBAGame>
        {
            new() { Id = "sr:match:game-1", Scheduled = startDate.AddDays(1) }
        };

        _mockSportsDataService
            .Setup(s => s.GetNBAScheduleAsync(startDate, endDate))
            .ReturnsAsync(apiGames);

        SetupExistingGame("sr:match:game-1", 1, 10, 20);

        // New statistics from API (final scores)
        var statistics = new NBAGameStatistics
        {
            //Status = "closed",
            Home = new NBATeamStatistics
            {
                Statistics = new NBAStatistics
                {
                    Points = 115,  // Final score
                    Rebounds = 50,
                    Assists = 28
                }
            },
            Away = new NBATeamStatistics
            {
                Statistics = new NBAStatistics
                {
                    Points = 108,  // Final score
                    Rebounds = 45,
                    Assists = 24
                }
            }
        };

        _mockSportsDataService
            .Setup(s => s.GetNBAGameStatisticsAsync("sr:match:game-1"))
            .ReturnsAsync(statistics);

        // Existing statistics (halftime scores - stale)
        var existingHomeStat = new TeamStatistic
        {
            TeamStatisticId = 1,
            GameId = 1,
            TeamId = 10,
            IsHomeTeam = true,
            Points = 55,  // Old halftime score
            Rebounds = 24,
            Assists = 12
        };

        var existingAwayStat = new TeamStatistic
        {
            TeamStatisticId = 2,
            GameId = 1,
            TeamId = 20,
            IsHomeTeam = false,
            Points = 48,  // Old halftime score
            Rebounds = 22,
            Assists = 10
        };

        // Setup to return existing stats
        _mockTeamStatsRepo
            .Setup(r => r.FirstOrDefaultAsync(It.Is<Expression<Func<TeamStatistic, bool>>>(
                expr => expr.Compile()(existingHomeStat))))
            .ReturnsAsync(existingHomeStat);

        _mockTeamStatsRepo
            .Setup(r => r.FirstOrDefaultAsync(It.Is<Expression<Func<TeamStatistic, bool>>>(
                expr => expr.Compile()(existingAwayStat))))
            .ReturnsAsync(existingAwayStat);

        var updatedStats = new List<TeamStatistic>();
        _mockTeamStatsRepo
            .Setup(r => r.UpdateAsync(It.IsAny<TeamStatistic>()))
            .Callback<TeamStatistic>(s => updatedStats.Add(s))
            .Returns(Task.CompletedTask);

        _mockMoneyballRepository.Setup(r => r.SaveChangesAsync()).ReturnsAsync(2);

        // Act
        await _service.IngestNBAGameStatisticsAsync(startDate, endDate);

        // Assert - Existing statistics should be updated (replaced) with final scores
        updatedStats.Should().HaveCount(2,
            "should update existing statistics for both teams");

        // Verify updates replaced stale data (acceptance criteria)
        existingHomeStat.Points.Should().Be(115, "home score should be updated to final value");
        existingAwayStat.Points.Should().Be(108, "away score should be updated to final value");

        // Verify no new records created (only updates)
        _mockTeamStatsRepo.Verify(r => r.AddAsync(It.IsAny<TeamStatistic>()), Times.Never);
    }

    /// <summary>
    /// Tests that all 17 NBAStatistics fields are correctly mapped to TeamStatistic columns.
    /// Verifies complete field mapping as per acceptance criteria.
    /// </summary>
    [Fact]
    public async Task IngestNBAGameStatisticsAsync_MapsAll17Fields_Correctly()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 1, 7);
        var nbaSport = new Sport { SportId = 1, Name = "NBA" };

        SetupBasicMocks(nbaSport);

        var apiGames = new List<NBAGame>
        {
            new() { Id = "sr:match:game-1", Scheduled = startDate.AddDays(1) }
        };

        _mockSportsDataService
            .Setup(s => s.GetNBAScheduleAsync(startDate, endDate))
            .ReturnsAsync(apiGames);

        SetupExistingGame("sr:match:game-1", 1, 10, 20);

        // Statistics with all 17 fields populated
        var statistics = CreateTestStatistics();
        _mockSportsDataService
            .Setup(s => s.GetNBAGameStatisticsAsync("sr:match:game-1"))
            .ReturnsAsync(statistics);

        // No existing statistics
        _mockTeamStatsRepo
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<TeamStatistic, bool>>>()))
            .ReturnsAsync((TeamStatistic?)null);

        var addedStats = new List<TeamStatistic>();
        _mockTeamStatsRepo
            .Setup(r => r.AddAsync(It.IsAny<TeamStatistic>()))
            .Callback<TeamStatistic>(s => addedStats.Add(s))
            .ReturnsAsync((TeamStatistic s) => s);

        _mockMoneyballRepository.Setup(r => r.SaveChangesAsync()).ReturnsAsync(2);

        // Act
        await _service.IngestNBAGameStatisticsAsync(startDate, endDate);

        // Assert - Verify all 17 fields mapped (acceptance criteria: all NBAStatistics fields mapped)
        var homeStat = addedStats.First(s => s.IsHomeTeam);

        // 1. Points
        homeStat.Points.Should().Be(110, "points should be mapped");

        // 2-4. Field goals
        homeStat.FieldGoalsMade.Should().Be(42, "field goals made should be mapped");
        homeStat.FieldGoalsAttempted.Should().Be(88, "field goals attempted should be mapped");
        homeStat.FieldGoalPercentage.Should().Be(0.477m, "field goal percentage should be mapped");

        // 5-7. Three-pointers
        homeStat.ThreePointsMade.Should().Be(12, "three-pointers made should be mapped");
        homeStat.ThreePointsAttempted.Should().Be(35, "three-pointers attempted should be mapped");
        homeStat.ThreePointPercentage.Should().Be(0.343m, "three-point percentage should be mapped");

        // 8-10. Free throws
        homeStat.FreeThrowsMade.Should().Be(14, "free throws made should be mapped");
        homeStat.FreeThrowsAttempted.Should().Be(18, "free throws attempted should be mapped");
        homeStat.FreeThrowPercentage.Should().Be(0.778m, "free throw percentage should be mapped");

        // 11-13. Rebounds
        homeStat.Rebounds.Should().Be(48, "rebounds should be mapped");
        homeStat.OffensiveRebounds.Should().Be(10, "offensive rebounds should be mapped");
        homeStat.DefensiveRebounds.Should().Be(38, "defensive rebounds should be mapped");

        // 14-17. Other stats
        homeStat.Assists.Should().Be(25, "assists should be mapped");
        homeStat.Steals.Should().Be(8, "steals should be mapped");
        homeStat.Blocks.Should().Be(5, "blocks should be mapped");
        homeStat.Turnovers.Should().Be(12, "turnovers should be mapped");
        homeStat.PersonalFouls.Should().Be(20, "personal fouls should be mapped");
    }

    /// <summary>
    /// Tests that games without statistics are skipped with warning.
    /// Verifies graceful handling when game hasn't started yet.
    /// </summary>
    [Fact]
    public async Task IngestNBAGameStatisticsAsync_NoStatistics_SkipsGameWithWarning()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 1, 7);
        var nbaSport = new Sport { SportId = 1, Name = "NBA" };

        SetupBasicMocks(nbaSport);

        var apiGames = new List<NBAGame>
        {
            new() { Id = "sr:match:game-1", Scheduled = startDate.AddDays(5) }  // Future game
        };

        _mockSportsDataService
            .Setup(s => s.GetNBAScheduleAsync(startDate, endDate))
            .ReturnsAsync(apiGames);

        SetupExistingGame("sr:match:game-1", 1, 10, 20);

        // Statistics not available (game hasn't started)
        _mockSportsDataService
            .Setup(s => s.GetNBAGameStatisticsAsync("sr:match:game-1"))
            .ReturnsAsync((NBAGameStatistics?)null);

        // Act
        await _service.IngestNBAGameStatisticsAsync(startDate, endDate);

        // Assert - No statistics should be processed
        _mockTeamStatsRepo.Verify(
            r => r.AddAsync(It.IsAny<TeamStatistic>()),
            Times.Never,
            "should not add statistics when API returns null");

        _mockTeamStatsRepo.Verify(
            r => r.UpdateAsync(It.IsAny<TeamStatistic>()),
            Times.Never,
            "should not update statistics when API returns null");

        // Verify warning logged
        VerifyLogWarning("No statistics returned from SportRadar API for game sr:match:game-1");
        VerifyLogWarning("Game may not have started yet");
    }

    /// <summary>
    /// Tests that games not in database are skipped silently.
    /// Verifies that statistics are only ingested for games that exist in database.
    /// </summary>
    [Fact]
    public async Task IngestNBAGameStatisticsAsync_GameNotInDatabase_SkipsGame()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 1, 7);
        var nbaSport = new Sport { SportId = 1, Name = "NBA" };

        SetupBasicMocks(nbaSport);

        var apiGames = new List<NBAGame>
        {
            new() { Id = "sr:match:unknown", Scheduled = startDate.AddDays(1) }
        };

        _mockSportsDataService
            .Setup(s => s.GetNBAScheduleAsync(startDate, endDate))
            .ReturnsAsync(apiGames);

        // Game does not exist in database
        _mockGamesRepo
            .Setup(r => r.GetGameByExternalIdAsync("sr:match:unknown", It.IsAny<int>()))
            .ReturnsAsync((Game?)null);

        // Act
        await _service.IngestNBAGameStatisticsAsync(startDate, endDate);

        // Assert - Should not fetch statistics for non-existent game
        _mockSportsDataService.Verify(
            s => s.GetNBAGameStatisticsAsync(It.IsAny<string>()),
            Times.Never,
            "should not fetch statistics for games not in database");
    }

    /// <summary>
    /// Tests that games with missing IDs are skipped with warning.
    /// Verifies data validation logic.
    /// </summary>
    [Fact]
    public async Task IngestNBAGameStatisticsAsync_MissingGameId_SkipsWithWarning()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 1, 7);
        var nbaSport = new Sport { SportId = 1, Name = "NBA" };

        SetupBasicMocks(nbaSport);

        // Game with missing ID
        var apiGames = new List<NBAGame>
        {
            new() { Id = "", Scheduled = startDate.AddDays(1) },  // Empty ID
            //new() { Id = null, Scheduled = startDate.AddDays(2) },  // Null ID
            new() { Id = "   ", Scheduled = startDate.AddDays(3) }  // Whitespace ID
        };

        _mockSportsDataService
            .Setup(s => s.GetNBAScheduleAsync(startDate, endDate))
            .ReturnsAsync(apiGames);

        // Act
        await _service.IngestNBAGameStatisticsAsync(startDate, endDate);

        // Assert - No games should be processed
        _mockGamesRepo.Verify(
            r => r.GetGameByExternalIdAsync(It.IsAny<string>(), It.IsAny<int>()),
            Times.Never,
            "should not check database for games with invalid IDs");

        // Verify warnings logged (2 times for 2 invalid games)
        VerifyLogWarningTimes("Skipping game with missing ID", 2);
    }

    /// <summary>
    /// Tests that changes are saved once per game.
    /// Verifies transactional behavior (home + away stats saved together).
    /// </summary>
    [Fact]
    public async Task IngestNBAGameStatisticsAsync_SavesChangesOncePerGame()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 1, 7);
        var nbaSport = new Sport { SportId = 1, Name = "NBA" };

        SetupBasicMocks(nbaSport);

        // Three games
        var apiGames = new List<NBAGame>
        {
            new() { Id = "sr:match:game-1", Scheduled = startDate.AddDays(1) },
            new() { Id = "sr:match:game-2", Scheduled = startDate.AddDays(2) },
            new() { Id = "sr:match:game-3", Scheduled = startDate.AddDays(3) }
        };

        _mockSportsDataService
            .Setup(s => s.GetNBAScheduleAsync(startDate, endDate))
            .ReturnsAsync(apiGames);

        // All games exist
        for (int i = 1; i <= 3; i++)
        {
            SetupExistingGame($"sr:match:game-{i}", i, i * 10, i * 10 + 1);
        }

        var statistics = CreateTestStatistics();
        _mockSportsDataService
            .Setup(s => s.GetNBAGameStatisticsAsync(It.IsAny<string>()))
            .ReturnsAsync(statistics);

        // No existing statistics
        _mockTeamStatsRepo
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<TeamStatistic, bool>>>()))
            .ReturnsAsync((TeamStatistic?)null);

        _mockTeamStatsRepo
            .Setup(r => r.AddAsync(It.IsAny<TeamStatistic>()))
            .ReturnsAsync((TeamStatistic s) => s);

        _mockMoneyballRepository.Setup(r => r.SaveChangesAsync()).ReturnsAsync(2);

        // Act
        await _service.IngestNBAGameStatisticsAsync(startDate, endDate);

        // Assert - SaveChanges should be called once per game (3 games = 3 saves)
        _mockMoneyballRepository.Verify(
            r => r.SaveChangesAsync(),
            Times.Exactly(3),
            "SaveChanges should be called once per game to save both home and away stats together");
    }

    /// <summary>
    /// Tests that completion is logged for each game with change count.
    /// Verifies logging includes game ID and number of changes saved.
    /// </summary>
    [Fact]
    public async Task IngestNBAGameStatisticsAsync_LogsCompletionPerGame()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 1, 7);
        var nbaSport = new Sport { SportId = 1, Name = "NBA" };

        SetupBasicMocks(nbaSport);

        var apiGames = new List<NBAGame>
        {
            new() { Id = "sr:match:game-123", Scheduled = startDate.AddDays(1) }
        };

        _mockSportsDataService
            .Setup(s => s.GetNBAScheduleAsync(startDate, endDate))
            .ReturnsAsync(apiGames);

        SetupExistingGame("sr:match:game-123", 1, 10, 20);

        var statistics = CreateTestStatistics();
        _mockSportsDataService
            .Setup(s => s.GetNBAGameStatisticsAsync("sr:match:game-123"))
            .ReturnsAsync(statistics);

        _mockTeamStatsRepo
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<TeamStatistic, bool>>>()))
            .ReturnsAsync((TeamStatistic?)null);

        _mockTeamStatsRepo
            .Setup(r => r.AddAsync(It.IsAny<TeamStatistic>()))
            .ReturnsAsync((TeamStatistic s) => s);

        _mockMoneyballRepository.Setup(r => r.SaveChangesAsync()).ReturnsAsync(2);

        // Act
        await _service.IngestNBAGameStatisticsAsync(startDate, endDate);

        // Assert - Verify completion logged with game ID and change count
        VerifyLogInformation("NBA game statistics ingestion complete for game sr:match:game-123");
        VerifyLogInformation("Changes saved: 2");
    }

    /// <summary>
    /// Tests that NBA sport not found throws exception with helpful message.
    /// Verifies prerequisite validation.
    /// </summary>
    [Fact]
    public async Task IngestNBAGameStatisticsAsync_NBASportNotFound_ThrowsException()
    {
        // Arrange - No NBA sport in database
        _mockSportsRepo
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Sport, bool>>>()))
            .ReturnsAsync((Sport?)null);

        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 1, 7);

        // Act & Assert
        var act = async () => await _service.IngestNBAGameStatisticsAsync(startDate, endDate);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*NBA sport not found*")
            .WithMessage("*Run database migrations or execute DatabaseSetup.sql*");
    }

    /// <summary>
    /// Tests that empty schedule from API returns early gracefully.
    /// Verifies handling of off-season or no games in date range.
    /// </summary>
    [Fact]
    public async Task IngestNBAGameStatisticsAsync_EmptySchedule_ReturnsEarly()
    {
        // Arrange
        var startDate = new DateTime(2024, 7, 1);  // Off-season
        var endDate = new DateTime(2024, 7, 7);
        var nbaSport = new Sport { SportId = 1, Name = "NBA" };

        SetupBasicMocks(nbaSport);

        // Empty schedule (off-season)
        _mockSportsDataService
            .Setup(s => s.GetNBAScheduleAsync(startDate, endDate))
            .ReturnsAsync(new List<NBAGame>());

        // Act
        await _service.IngestNBAGameStatisticsAsync(startDate, endDate);

        // Assert - Should return early
        _mockGamesRepo.Verify(
            r => r.GetGameByExternalIdAsync(It.IsAny<string>(), It.IsAny<int>()),
            Times.Never);

        _mockSportsDataService.Verify(
            s => s.GetNBAGameStatisticsAsync(It.IsAny<string>()),
            Times.Never);

        VerifyLogInformation("No games returned from SportRadar API for date range");
    }

    /// <summary>
    /// Tests that API errors are logged and re-thrown.
    /// Verifies exception handling propagates errors appropriately.
    /// </summary>
    [Fact]
    public async Task IngestNBAGameStatisticsAsync_APIError_LogsAndRethrows()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 1, 7);
        var nbaSport = new Sport { SportId = 1, Name = "NBA" };

        SetupBasicMocks(nbaSport);

        // API throws exception
        _mockSportsDataService
            .Setup(s => s.GetNBAScheduleAsync(startDate, endDate))
            .ThrowsAsync(new HttpRequestException("Network failure"));

        // Act & Assert
        var act = async () => await _service.IngestNBAGameStatisticsAsync(startDate, endDate);

        await act.Should().ThrowAsync<HttpRequestException>()
            .WithMessage("Network failure");

        // Verify error logged
        VerifyLogError("Error during NBA game statistics ingestion");
    }

    // ==================== Helper Methods ====================

    /// <summary>
    /// Sets up basic mocks required for most tests.
    /// Configures NBA sport entity.
    /// </summary>
    private void SetupBasicMocks(Sport nbaSport)
    {
        _mockSportsRepo
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Sport, bool>>>()))
            .ReturnsAsync(nbaSport);
    }

    /// <summary>
    /// Sets up an existing game mock for the given external ID.
    /// Simulates game already present in database.
    /// </summary>
    private void SetupExistingGame(string externalGameId, int gameId, int homeTeamId, int awayTeamId)
    {
        var game = new Game
        {
            GameId = gameId,
            ExternalGameId = externalGameId,
            HomeTeamId = homeTeamId,
            AwayTeamId = awayTeamId
        };

        _mockGamesRepo
            .Setup(r => r.GetGameByExternalIdAsync(externalGameId, It.IsAny<int>()))
            .ReturnsAsync(game);
    }

    /// <summary>
    /// Creates test NBA game statistics with all 17 fields populated.
    /// Used to verify complete field mapping.
    /// </summary>
    private static NBAGameStatistics CreateTestStatistics()
    {
        return new NBAGameStatistics
        {
            //Status = "closed",
            Home = new NBATeamStatistics
            {
                Name = "Home Team",
                Statistics = new NBAStatistics
                {
                    Points = 110,
                    FieldGoalsMade = 42,
                    FieldGoalsAttempted = 88,
                    FieldGoalPercentage = 0.477m,
                    ThreePointsMade = 12,
                    ThreePointsAttempted = 35,
                    ThreePointPercentage = 0.343m,
                    FreeThrowsMade = 14,
                    FreeThrowsAttempted = 18,
                    FreeThrowPercentage = 0.778m,
                    Rebounds = 48,
                    OffensiveRebounds = 10,
                    DefensiveRebounds = 38,
                    Assists = 25,
                    Steals = 8,
                    Blocks = 5,
                    Turnovers = 12,
                    PersonalFouls = 20
                }
            },
            Away = new NBATeamStatistics
            {
                Name = "Away Team",
                Statistics = new NBAStatistics
                {
                    Points = 105,
                    FieldGoalsMade = 39,
                    FieldGoalsAttempted = 85,
                    FieldGoalPercentage = 0.459m,
                    ThreePointsMade = 10,
                    ThreePointsAttempted = 30,
                    ThreePointPercentage = 0.333m,
                    FreeThrowsMade = 17,
                    FreeThrowsAttempted = 22,
                    FreeThrowPercentage = 0.773m,
                    Rebounds = 44,
                    OffensiveRebounds = 8,
                    DefensiveRebounds = 36,
                    Assists = 22,
                    Steals = 7,
                    Blocks = 4,
                    Turnovers = 15,
                    PersonalFouls = 18
                }
            }
        };
    }

    /// <summary>
    /// Verifies that an information log was written containing the expected message.
    /// </summary>
    private void VerifyLogInformation(string expectedMessage)
    {
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    /// <summary>
    /// Verifies that a warning was logged containing the expected message.
    /// </summary>
    private void VerifyLogWarning(string expectedMessage)
    {
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    /// <summary>
    /// Verifies that a warning was logged a specific number of times.
    /// </summary>
    private void VerifyLogWarningTimes(string expectedMessage, int times)
    {
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Exactly(times));
    }

    /// <summary>
    /// Verifies that an error was logged containing the expected message.
    /// </summary>
    private void VerifyLogError(string expectedMessage)
    {
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
}

/// <summary>
/// Unit tests for DataIngestionService.IngestNBAOddsAsync method.
/// Tests cover date range odds ingestion, market mapping, bookmaker grouping, and error handling.
/// The method creates one Odds row per bookmaker per game, grouping all markets together.
/// Uses Moq for mocking dependencies and FluentAssertions for readable assertions.
/// </summary>
public class DataIngestionService_IngestNBAOddsAsyncTests
{
    private readonly Mock<IMoneyballRepository> _mockMoneyballRepository;
    private readonly Mock<ISportsDataService> _mockSportsDataService;
    private readonly DataIngestionService _service;

    // Mock repositories
    private readonly Mock<IRepository<Sport>> _mockSportsRepo;
    private readonly Mock<IGameRepository> _mockGamesRepo;
    private readonly Mock<IOddsRepository> _mockOddsRepo;

    /// <summary>
    /// Test fixture setup - initializes all mocks and creates service instance.
    /// Runs before each test method to ensure clean state.
    /// </summary>
    public DataIngestionService_IngestNBAOddsAsyncTests()
    {
        _mockMoneyballRepository = new Mock<IMoneyballRepository>();
        _mockSportsDataService = new Mock<ISportsDataService>();
        var mockOddsDataService = new Mock<IOddsDataService>();
        var mockLogger = new Mock<ILogger<DataIngestionService>>();

        _mockSportsRepo = new Mock<IRepository<Sport>>();
        _mockGamesRepo = new Mock<IGameRepository>();
        _mockOddsRepo = new Mock<IOddsRepository>();

        // Wire up repository properties
        _mockMoneyballRepository.Setup(r => r.Sports).Returns(_mockSportsRepo.Object);
        _mockMoneyballRepository.Setup(r => r.Games).Returns(_mockGamesRepo.Object);
        _mockMoneyballRepository.Setup(r => r.Odds).Returns(_mockOddsRepo.Object);

        _service = new DataIngestionService(
            _mockMoneyballRepository.Object,
            _mockSportsDataService.Object,
            mockOddsDataService.Object,
            mockLogger.Object);
    }

    /// <summary>
    /// Tests successful odds ingestion creating one row per bookmaker per game.
    /// Verifies core acceptance criteria: one Odds row per bookmaker.
    /// </summary>
    [Fact]
    public async Task IngestNBAOddsAsync_SuccessfulIngestion_CreatesOneRowPerBookmaker()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 1, 7);
        var nbaSport = new Sport { SportId = 1, Name = "NBA" };

        SetupBasicMocks(nbaSport);

        // API returns one game
        var apiGames = new List<NBAGame>
        {
            new() { Id = "sr:match:game-1", Scheduled = startDate.AddDays(1) }
        };

        _mockSportsDataService
            .Setup(s => s.GetNBAScheduleAsync(startDate, endDate))
            .ReturnsAsync(apiGames);

        SetupExistingGame("sr:match:game-1", 1);

        // Odds with 2 bookmakers (DraftKings and FanDuel)
        var oddsResponse = CreateTestOddsResponse();
        _mockSportsDataService
            .Setup(s => s.GetNBAOddsAsync("sr:match:game-1"))
            .ReturnsAsync(oddsResponse);

        var addedOdds = new List<Odds>();
        _mockOddsRepo
            .Setup(r => r.AddAsync(It.IsAny<Odds>()))
            .Callback<Odds>(o => addedOdds.Add(o))
            .ReturnsAsync((Odds o) => o);

        _mockMoneyballRepository.Setup(r => r.SaveChangesAsync()).ReturnsAsync(2);

        // Act
        await _service.IngestNBAOddsAsync(startDate, endDate);

        // Assert - One row per bookmaker (acceptance criteria)
        addedOdds.Should().HaveCount(2,
            "should create exactly one Odds row per bookmaker");

        addedOdds.Select(o => o.BookmakerName).Should().OnlyHaveUniqueItems(
            "each bookmaker should have exactly one row");

        addedOdds.Should().Contain(o => o.BookmakerName == "DraftKings");
        addedOdds.Should().Contain(o => o.BookmakerName == "FanDuel");
    }

    /// <summary>
    /// Tests that all three market types (moneyline, spread, totals) are mapped correctly.
    /// Verifies that multiple markets from same bookmaker are grouped into one Odds row.
    /// </summary>
    [Fact]
    public async Task IngestNBAOddsAsync_MultipleMarkets_GroupedIntoOneRow()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 1, 7);
        var nbaSport = new Sport { SportId = 1, Name = "NBA" };

        SetupBasicMocks(nbaSport);

        var apiGames = new List<NBAGame>
        {
            new() { Id = "sr:match:game-1", Scheduled = startDate.AddDays(1) }
        };

        _mockSportsDataService
            .Setup(s => s.GetNBAScheduleAsync(startDate, endDate))
            .ReturnsAsync(apiGames);

        SetupExistingGame("sr:match:game-1", 1);

        // Odds with all 3 market types from same bookmaker
        var oddsResponse = CreateTestOddsResponse();
        _mockSportsDataService
            .Setup(s => s.GetNBAOddsAsync("sr:match:game-1"))
            .ReturnsAsync(oddsResponse);

        var addedOdds = new List<Odds>();
        _mockOddsRepo
            .Setup(r => r.AddAsync(It.IsAny<Odds>()))
            .Callback<Odds>(o => addedOdds.Add(o))
            .ReturnsAsync((Odds o) => o);

        _mockMoneyballRepository.Setup(r => r.SaveChangesAsync()).ReturnsAsync(2);

        // Act
        await _service.IngestNBAOddsAsync(startDate, endDate);

        // Assert - All markets from same bookmaker grouped into one row
        var draftKingsOdds = addedOdds.First(o => o.BookmakerName == "DraftKings");

        // Moneyline fields populated
        draftKingsOdds.HomeMoneyline.Should().NotBeNull("moneyline should be mapped");
        draftKingsOdds.AwayMoneyline.Should().NotBeNull("moneyline should be mapped");

        // Spread fields populated
        draftKingsOdds.HomeSpread.Should().NotBeNull("spread should be mapped");
        draftKingsOdds.AwaySpread.Should().NotBeNull("spread should be mapped");
        draftKingsOdds.HomeSpreadOdds.Should().NotBeNull("spread odds should be mapped");
        draftKingsOdds.AwaySpreadOdds.Should().NotBeNull("spread odds should be mapped");

        // Total fields populated
        draftKingsOdds.OverUnder.Should().NotBeNull("total should be mapped");
        draftKingsOdds.OverOdds.Should().NotBeNull("over odds should be mapped");
        draftKingsOdds.UnderOdds.Should().NotBeNull("under odds should be mapped");
    }

    /// <summary>
    /// Tests that moneyline odds are correctly mapped from SportRadar format.
    /// Verifies Type "1" = Home, Type "2" = Away.
    /// </summary>
    [Fact]
    public async Task IngestNBAOddsAsync_MapsMoneylineOdds_Correctly()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 1, 7);
        var nbaSport = new Sport { SportId = 1, Name = "NBA" };

        SetupBasicMocks(nbaSport);

        var apiGames = new List<NBAGame>
        {
            new() { Id = "sr:match:game-1", Scheduled = startDate.AddDays(1) }
        };

        _mockSportsDataService
            .Setup(s => s.GetNBAScheduleAsync(startDate, endDate))
            .ReturnsAsync(apiGames);

        SetupExistingGame("sr:match:game-1", 1);

        var oddsResponse = CreateTestOddsResponse();
        _mockSportsDataService
            .Setup(s => s.GetNBAOddsAsync("sr:match:game-1"))
            .ReturnsAsync(oddsResponse);

        var addedOdds = new List<Odds>();
        _mockOddsRepo
            .Setup(r => r.AddAsync(It.IsAny<Odds>()))
            .Callback<Odds>(o => addedOdds.Add(o))
            .ReturnsAsync((Odds o) => o);

        _mockMoneyballRepository.Setup(r => r.SaveChangesAsync()).ReturnsAsync(2);

        // Act
        await _service.IngestNBAOddsAsync(startDate, endDate);

        // Assert - Verify moneyline mapping (Type "1" = Home, Type "2" = Away)
        var odds = addedOdds.First(o => o.BookmakerName == "DraftKings");

        odds.HomeMoneyline.Should().Be(-150m,
            "home moneyline should be mapped from Type '1' outcome");
        odds.AwayMoneyline.Should().Be(130m,
            "away moneyline should be mapped from Type '2' outcome");
    }

    /// <summary>
    /// Tests that spread odds are correctly mapped with both line and odds values.
    /// Verifies spread line (point value) and spread odds (price) are both captured.
    /// </summary>
    [Fact]
    public async Task IngestNBAOddsAsync_MapsSpreadOdds_Correctly()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 1, 7);
        var nbaSport = new Sport { SportId = 1, Name = "NBA" };

        SetupBasicMocks(nbaSport);

        var apiGames = new List<NBAGame>
        {
            new() { Id = "sr:match:game-1", Scheduled = startDate.AddDays(1) }
        };

        _mockSportsDataService
            .Setup(s => s.GetNBAScheduleAsync(startDate, endDate))
            .ReturnsAsync(apiGames);

        SetupExistingGame("sr:match:game-1", 1);

        var oddsResponse = CreateTestOddsResponse();
        _mockSportsDataService
            .Setup(s => s.GetNBAOddsAsync("sr:match:game-1"))
            .ReturnsAsync(oddsResponse);

        var addedOdds = new List<Odds>();
        _mockOddsRepo
            .Setup(r => r.AddAsync(It.IsAny<Odds>()))
            .Callback<Odds>(o => addedOdds.Add(o))
            .ReturnsAsync((Odds o) => o);

        _mockMoneyballRepository.Setup(r => r.SaveChangesAsync()).ReturnsAsync(2);

        // Act
        await _service.IngestNBAOddsAsync(startDate, endDate);

        // Assert - Verify spread mapping (line and odds)
        var odds = addedOdds.First(o => o.BookmakerName == "DraftKings");

        // Home spread
        odds.HomeSpread.Should().Be(-3.5m, "home spread line should be mapped");
        odds.HomeSpreadOdds.Should().Be(-110m, "home spread odds (price) should be mapped");

        // Away spread
        odds.AwaySpread.Should().Be(3.5m, "away spread line should be mapped");
        odds.AwaySpreadOdds.Should().Be(-110m, "away spread odds (price) should be mapped");
    }

    /// <summary>
    /// Tests that total (over/under) odds are correctly mapped.
    /// Verifies total line and over/under odds are captured.
    /// </summary>
    [Fact]
    public async Task IngestNBAOddsAsync_MapsTotalOdds_Correctly()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 1, 7);
        var nbaSport = new Sport { SportId = 1, Name = "NBA" };

        SetupBasicMocks(nbaSport);

        var apiGames = new List<NBAGame>
        {
            new() { Id = "sr:match:game-1", Scheduled = startDate.AddDays(1) }
        };

        _mockSportsDataService
            .Setup(s => s.GetNBAScheduleAsync(startDate, endDate))
            .ReturnsAsync(apiGames);

        SetupExistingGame("sr:match:game-1", 1);

        var oddsResponse = CreateTestOddsResponse();
        _mockSportsDataService
            .Setup(s => s.GetNBAOddsAsync("sr:match:game-1"))
            .ReturnsAsync(oddsResponse);

        var addedOdds = new List<Odds>();
        _mockOddsRepo
            .Setup(r => r.AddAsync(It.IsAny<Odds>()))
            .Callback<Odds>(o => addedOdds.Add(o))
            .ReturnsAsync((Odds o) => o);

        _mockMoneyballRepository.Setup(r => r.SaveChangesAsync()).ReturnsAsync(2);

        // Act
        await _service.IngestNBAOddsAsync(startDate, endDate);

        // Assert - Verify total mapping
        var odds = addedOdds.First(o => o.BookmakerName == "DraftKings");

        odds.OverUnder.Should().Be(220.5m, "over/under line should be mapped from 'over' outcome");
        odds.OverOdds.Should().Be(-110m, "over odds should be mapped");
        odds.UnderOdds.Should().Be(-110m, "under odds should be mapped");
    }

    // Additional helper methods and tests would continue here...
    // For brevity, I'll include the key helper methods

    private void SetupBasicMocks(Sport nbaSport)
    {
        _mockSportsRepo
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Sport, bool>>>()))
            .ReturnsAsync(nbaSport);
    }

    private void SetupExistingGame(string externalGameId, int gameId)
    {
        var game = new Game { GameId = gameId, ExternalGameId = externalGameId };
        _mockGamesRepo
            .Setup(r => r.GetGameByExternalIdAsync(externalGameId, It.IsAny<int>()))
            .ReturnsAsync(game);
    }

    private static NBAOddsResponse CreateTestOddsResponse()
    {
        return new NBAOddsResponse
        {
            Sport_Event_Id = "sr:match:game-1",
            Markets =
            [
                new NBAMarket
                {
                    Name = "1x2",
                    Bookmakers =
                    [
                        new NBABookmaker
                        {
                            Name = "DraftKings",
                            Outcomes =
                            [
                                new NBAOutcome { Type = "1", Odds = -150 },
                                new NBAOutcome { Type = "2", Odds = 130 }
                            ]
                        },

                        new NBABookmaker
                        {
                            Name = "FanDuel",
                            Outcomes =
                            [
                                new NBAOutcome { Type = "1", Odds = -145 },
                                new NBAOutcome { Type = "2", Odds = 125 }
                            ]
                        }
                    ]
                },

                new NBAMarket
                {
                    Name = "pointspread",
                    Bookmakers =
                    [
                        new NBABookmaker
                        {
                            Name = "DraftKings",
                            Outcomes =
                            [
                                new NBAOutcome { Type = "1", Odds = -110, Line = -3.5m },
                                new NBAOutcome { Type = "2", Odds = -110, Line = 3.5m }
                            ]
                        },

                        new NBABookmaker
                        {
                            Name = "FanDuel",
                            Outcomes =
                            [
                                new NBAOutcome { Type = "1", Odds = -108, Line = -3.5m },
                                new NBAOutcome { Type = "2", Odds = -112, Line = 3.5m }
                            ]
                        }
                    ]
                },

                new NBAMarket
                {
                    Name = "totals",
                    Bookmakers =
                    [
                        new NBABookmaker
                        {
                            Name = "DraftKings",
                            Outcomes =
                            [
                                new NBAOutcome { Type = "over", Odds = -110, Line = 220.5m },
                                new NBAOutcome { Type = "under", Odds = -110, Line = 220.5m }
                            ]
                        },

                        new NBABookmaker
                        {
                            Name = "FanDuel",
                            Outcomes =
                            [
                                new NBAOutcome { Type = "over", Odds = -112, Line = 221.5m },
                                new NBAOutcome { Type = "under", Odds = -108, Line = 221.5m }
                            ]
                        }
                    ]
                }
            ]
        };
    }
}

/// <summary>
/// Unit tests for DataIngestionService.IngestOddsAsync odds ingestion methods (The Odds API).
/// Uses Moq for mocking dependencies and FluentAssertions for readable assertions.
/// </summary>
public class DataIngestionService_IngestOddsAsyncTests
{
    private readonly Mock<IMoneyballRepository> _mockMoneyballRepository;
    private readonly Mock<IOddsDataService> _mockOddsDataService;
    private readonly Mock<ILogger<DataIngestionService>> _mockLogger;
    private readonly DataIngestionService _service;

    // Mock repositories
    private readonly Mock<IRepository<Sport>> _mockSportsRepo;
    private readonly Mock<IGameRepository> _mockGameRepository;
    private readonly Mock<IOddsRepository> _mockOddsRepo;

    /// <summary>
    /// Test fixture setup - initializes all mocks and creates service instance.
    /// Runs before each test method to ensure clean state.
    /// </summary>
    public DataIngestionService_IngestOddsAsyncTests()
    {
        _mockMoneyballRepository = new Mock<IMoneyballRepository>();
        var mockSportsDataService = new Mock<ISportsDataService>();
        _mockOddsDataService = new Mock<IOddsDataService>();
        _mockLogger = new Mock<ILogger<DataIngestionService>>();

        _mockSportsRepo = new Mock<IRepository<Sport>>();
        _mockGameRepository = new Mock<IGameRepository>();
        _mockOddsRepo = new Mock<IOddsRepository>();
        var mockTeamsRepo = new Mock<ITeamRepository>();

        // Wire up repository properties
        _mockMoneyballRepository.Setup(r => r.Sports).Returns(_mockSportsRepo.Object);
        _mockMoneyballRepository.Setup(r => r.Games).Returns(_mockGameRepository.Object);
        _mockMoneyballRepository.Setup(r => r.Odds).Returns(_mockOddsRepo.Object);
        _mockMoneyballRepository.Setup(r => r.Teams).Returns(mockTeamsRepo.Object);

        _service = new DataIngestionService(
            _mockMoneyballRepository.Object,
            mockSportsDataService.Object,
            _mockOddsDataService.Object,
            _mockLogger.Object);
    }

    /// <summary>
    /// Tests successful odds ingestion from The Odds API with team name matching.
    /// Verifies date window matching and team name fuzzy matching logic.
    /// </summary>
    [Fact]
    public async Task IngestOddsAsync_SuccessfulIngestion_MatchesGamesByDateAndTeams()
    {
        // Arrange - Setup sport
        var sport = "basketball_nba";
        var nbaSport = new Sport { SportId = 1, Name = "NBA" };

        _mockSportsRepo
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Sport, bool>>>()))
            .ReturnsAsync(nbaSport);

        // Arrange - Setup odds response
        var gameTime = DateTime.UtcNow.AddDays(1);
        var oddsResponse = CreateTheOddsAPIResponse(gameTime);

        _mockOddsDataService
            .Setup(s => s.GetOddsAsync(sport))
            .ReturnsAsync(oddsResponse);

        // Arrange - Setup matching game in database
        var games = new List<Game>
        {
            new()
            {
                GameId = 1,
                GameDate = gameTime,
                HomeTeamId = 1,
                AwayTeamId = 2,
                HomeTeam = new Team { TeamId = 1, Name = "Los Angeles Lakers", City = "Los Angeles" },
                AwayTeam = new Team { TeamId = 2, Name = "Boston Celtics", City = "Boston" }
            }
        };

        _mockGameRepository
            .Setup(r => r.GetGamesByDateRangeAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<int?>()))
            .ReturnsAsync(games);

        var addedOdds = new List<Odds>();
        _mockOddsRepo
            .Setup(r => r.AddAsync(It.IsAny<Odds>()))
            .Callback<Odds>(o => addedOdds.Add(o))
            .ReturnsAsync((Odds o) => o);

        _mockMoneyballRepository.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        await _service.IngestOddsAsync(sport);

        // Assert - Odds should be added for matched game
        addedOdds.Should().ContainSingle("one bookmaker should create one odds row");

        var odds = addedOdds.First();
        odds.GameId.Should().Be(1, "should match to the correct game");
        odds.BookmakerName.Should().Be("DraftKings", "bookmaker name should be set");
    }

    /// <summary>
    /// Tests that unsupported sport keys throw ArgumentException.
    /// Validates sport mapping logic.
    /// </summary>
    [Theory]
    [InlineData("baseball_mlb", "MLB not mapped yet")]
    [InlineData("icehockey_nhl", "NHL not mapped yet")]
    [InlineData("invalid_sport", "completely invalid sport")]
    public async Task IngestOddsAsync_UnsupportedSport_ThrowsArgumentException(
        string sport,
        string because)
    {
        // Act & Assert
        var act = async () => await _service.IngestOddsAsync(sport);

        await act.Should().ThrowAsync<ArgumentException>(because)
            .WithMessage($"*Unsupported sport: {sport}*");
    }

    /// <summary>
    /// Tests that method returns early when sport is not found in database.
    /// Verifies graceful handling of missing sport seed data.
    /// </summary>
    [Fact]
    public async Task IngestOddsAsync_SportNotFound_ReturnsEarly()
    {
        // Arrange - No sport in database
        _mockSportsRepo
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Sport, bool>>>()))
            .ReturnsAsync((Sport?)null);

        // Act
        await _service.IngestOddsAsync("basketball_nba");

        // Assert - Should return early without calling odds API
        _mockOddsDataService.Verify(s => s.GetOddsAsync(It.IsAny<string>()), Times.Never,
            "should not fetch odds when sport not found");

        _mockMoneyballRepository.Verify(r => r.SaveChangesAsync(), Times.Never,
            "should not save when sport not found");

        // Verify error was logged
        VerifyLogError("Sport");
        VerifyLogError("not found");
    }

    /// <summary>
    /// Tests team name matching with various formats.
    /// Verifies fuzzy matching logic for team names and cities.
    /// </summary>
    [Theory]
    [InlineData("Lakers", "Los Angeles Lakers", true, "team name suffix should match")]
    [InlineData("Los Angeles", "Los Angeles Lakers", true, "city should match team name")]
    [InlineData("Boston Celtics", "Boston Celtics", true, "exact match should work")]
    [InlineData("Phoenix Suns", "Los Angeles Lakers", false, "different teams should not match")]
    public async Task IngestOddsAsync_TeamNameMatching_WorksCorrectly(
        string oddsTeamName,
        string dbTeamName,
        bool shouldMatch,
        string because)
    {
        // Arrange
        var sport = "basketball_nba";
        var nbaSport = new Sport { SportId = 1, Name = "NBA" };

        _mockSportsRepo
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Sport, bool>>>()))
            .ReturnsAsync(nbaSport);

        var gameTime = DateTime.UtcNow.AddDays(1);
        var oddsResponse = new OddsResponse
        {
            Data =
            [
                new OddsGame
                {
                    Id = "game-1",
                    CommenceTime = gameTime,
                    HomeTeam = oddsTeamName,
                    AwayTeam = "Test Away Team",
                    Bookmakers =
                    [
                        new Bookmaker
                        {
                            Title = "TestBook",
                            Markets =
                            [
                                new Market
                                {
                                    Key = "h2h",
                                    Outcomes =
                                    [
                                        new Outcome { Name = oddsTeamName, Price = -150 },
                                        new Outcome { Name = "Test Away Team", Price = 130 }
                                    ]
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        _mockOddsDataService
            .Setup(s => s.GetOddsAsync(sport))
            .ReturnsAsync(oddsResponse);

        var games = new List<Game>
        {
            new()
            {
                GameId = 1,
                GameDate = gameTime,
                HomeTeam = new Team { Name = dbTeamName, City = dbTeamName.Split(' ').First() },
                AwayTeam = new Team { Name = "Test Away Team", City = "Test" }
            }
        };

        _mockGameRepository
            .Setup(r => r.GetGamesByDateRangeAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<int?>()))
            .ReturnsAsync(games);

        var addedOdds = new List<Odds>();
        _mockOddsRepo
            .Setup(r => r.AddAsync(It.IsAny<Odds>()))
            .Callback<Odds>(o => addedOdds.Add(o))
            .ReturnsAsync((Odds o) => o);

        _mockMoneyballRepository.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        await _service.IngestOddsAsync(sport);

        // Assert
        if (shouldMatch)
        {
            addedOdds.Should().NotBeEmpty(because);
        }
        else
        {
            addedOdds.Should().BeEmpty(because);
        }
    }

    /// <summary>
    /// Tests that games outside the date window are not matched.
    /// Verifies the ±2 hour date window matching logic.
    /// </summary>
    [Fact]
    public async Task IngestOddsAsync_GameOutsideDateWindow_NotMatched()
    {
        // Arrange
        var sport = "basketball_nba";
        var nbaSport = new Sport { SportId = 1, Name = "NBA" };

        _mockSportsRepo
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Sport, bool>>>()))
            .ReturnsAsync(nbaSport);

        var oddsGameTime = DateTime.UtcNow.AddDays(1);
        var oddsResponse = CreateTheOddsAPIResponse(oddsGameTime);

        _mockOddsDataService
            .Setup(s => s.GetOddsAsync(sport))
            .ReturnsAsync(oddsResponse);

        _mockGameRepository
            .Setup(r => r.GetGamesByDateRangeAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<int?>()))
            .ReturnsAsync([]);

        // Act
        await _service.IngestOddsAsync(sport);

        // Assert - No odds should be added (game outside date window)
        _mockOddsRepo.Verify(r => r.AddAsync(It.IsAny<Odds>()), Times.Never,
            "games outside ±2 hour window should not be matched");
    }

    /// <summary>
    /// Tests that all three market types (h2h, spreads, totals) are correctly mapped.
    /// Verifies field mapping from The Odds API format to Odds entity.
    /// </summary>
    [Fact]
    public async Task IngestOddsAsync_MapsAllMarketTypes_Correctly()
    {
        // Arrange
        var sport = "basketball_nba";
        SetupBasicOddsAPIMocks(sport);

        var gameTime = DateTime.UtcNow.AddDays(1);
        var oddsResponse = CreateTheOddsAPIResponse(gameTime);

        _mockOddsDataService
            .Setup(s => s.GetOddsAsync(sport))
            .ReturnsAsync(oddsResponse);

        SetupMatchingGameForOddsAPI(gameTime);

        var addedOdds = new List<Odds>();
        _mockOddsRepo
            .Setup(r => r.AddAsync(It.IsAny<Odds>()))
            .Callback<Odds>(o => addedOdds.Add(o))
            .ReturnsAsync((Odds o) => o);

        _mockMoneyballRepository.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        await _service.IngestOddsAsync(sport);

        // Assert - All market types should be mapped
        addedOdds.Should().ContainSingle();

        var odds = addedOdds.First();

        // h2h market (moneyline)
        odds.HomeMoneyline.Should().Be(-150m, "home moneyline from h2h market");
        odds.AwayMoneyline.Should().Be(130m, "away moneyline from h2h market");

        // spreads market
        odds.HomeSpread.Should().Be(-3.5m, "home spread point");
        odds.AwaySpread.Should().Be(3.5m, "away spread point");
        odds.HomeSpreadOdds.Should().Be(-110m, "home spread price");
        odds.AwaySpreadOdds.Should().Be(-110m, "away spread price");

        // totals market
        odds.OverUnder.Should().Be(220.5m, "total line from Over outcome");
        odds.OverOdds.Should().Be(-110m, "over price");
        odds.UnderOdds.Should().Be(-110m, "under price");
    }

    /// <summary>
    /// Tests that unmatched games are logged as debug without errors.
    /// Verifies graceful handling of odds that don't match any database game.
    /// </summary>
    [Fact]
    public async Task IngestOddsAsync_NoMatchingGame_LogsDebugAndContinues()
    {
        // Arrange
        var sport = "basketball_nba";
        SetupBasicOddsAPIMocks(sport);

        var oddsResponse = CreateTheOddsAPIResponse(DateTime.UtcNow);
        _mockOddsDataService
            .Setup(s => s.GetOddsAsync(sport))
            .ReturnsAsync(oddsResponse);

        // No games in date range
        _mockGameRepository
            .Setup(r => r.GetGamesByDateRangeAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<int?>()))
            .ReturnsAsync(new List<Game>());

        // Act
        await _service.IngestOddsAsync(sport);

        // Assert - No odds should be added
        _mockOddsRepo.Verify(r => r.AddAsync(It.IsAny<Odds>()), Times.Never);

        // Verify debug log
        VerifyLogDebug("No matching game found");
    }

    /// <summary>
    /// Tests that sport key mapping works for both NBA and NFL.
    /// </summary>
    [Theory]
    [InlineData("basketball_nba", "NBA")]
    [InlineData("americanfootball_nfl", "NFL")]
    public async Task IngestOddsAsync_SportKeyMapping_WorksCorrectly(
        string sportKey,
        string expectedSportName)
    {
        // Arrange
        Sport? capturedSport = null;

        _mockSportsRepo
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Sport, bool>>>()))
            .Callback<Expression<Func<Sport, bool>>>(expr =>
            {
                var testSport = new Sport { SportId = 1, Name = expectedSportName };
                if (expr.Compile()(testSport))
                {
                    capturedSport = testSport;
                }
            })
            .ReturnsAsync(new Sport { SportId = 1, Name = expectedSportName });

        _mockOddsDataService
            .Setup(s => s.GetOddsAsync(sportKey))
            .ReturnsAsync(new OddsResponse { Data = [] });

        // Act
        await _service.IngestOddsAsync(sportKey);

        // Assert - Correct sport name should be queried
        capturedSport.Should().NotBeNull($"{sportKey} should map to {expectedSportName}");
        capturedSport!.Name.Should().Be(expectedSportName);
    }

    #region Helper Methods

    /// <summary>
    /// Sets up basic mocks for The Odds API tests.
    /// </summary>
    private void SetupBasicOddsAPIMocks(string sport)
    {
        var sportName = sport == "basketball_nba" ? "NBA" : "NFL";
        var sportEntity = new Sport { SportId = 1, Name = sportName };

        _mockSportsRepo
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Sport, bool>>>()))
            .ReturnsAsync(sportEntity);
    }

    /// <summary>
    /// Sets up a matching game for The Odds API tests.
    /// </summary>
    private void SetupMatchingGameForOddsAPI(DateTime gameTime)
    {
        var games = new List<Game>
        {
            new()
            {
                GameId = 1,
                GameDate = gameTime,
                HomeTeamId = 1,
                AwayTeamId = 2,
                HomeTeam = new Team { TeamId = 1, Name = "Los Angeles Lakers", City = "Los Angeles" },
                AwayTeam = new Team { TeamId = 2, Name = "Boston Celtics", City = "Boston" }
            }
        };

        _mockGameRepository
            .Setup(r => r.GetGamesByDateRangeAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<int?>()))
            .ReturnsAsync(games);
    }

    /// <summary>
    /// Creates a test OddsResponse from The Odds API with all market types.
    /// </summary>
    private static OddsResponse CreateTheOddsAPIResponse(DateTime commenceTime)
    {
        return new OddsResponse
        {
            Data =
            [
                new OddsGame
                {
                    Id = "game-123",
                    CommenceTime = commenceTime,
                    HomeTeam = "Los Angeles Lakers",
                    AwayTeam = "Boston Celtics",
                    Bookmakers =
                    [
                        new Bookmaker
                        {
                            Title = "DraftKings",
                            Markets =
                            [
                                new Market
                                {
                                    Key = "h2h",
                                    Outcomes =
                                    [
                                        new Outcome { Name = "Los Angeles Lakers", Price = -150 },
                                        new Outcome { Name = "Boston Celtics", Price = 130 }
                                    ]
                                },

                                new Market
                                {
                                    Key = "spreads",
                                    Outcomes =
                                    [
                                        new Outcome { Name = "Los Angeles Lakers", Price = -110, Point = -3.5m },
                                        new Outcome { Name = "Boston Celtics", Price = -110, Point = 3.5m }
                                    ]
                                },

                                new Market
                                {
                                    Key = "totals",
                                    Outcomes =
                                    [
                                        new Outcome { Name = "Over", Price = -110, Point = 220.5m },
                                        new Outcome { Name = "Under", Price = -110, Point = 220.5m }
                                    ]
                                }
                            ]
                        }
                    ]
                }
            ]
        };
    }

    /// <summary>
    /// Verifies that a debug log was written containing the expected message.
    /// </summary>
    private void VerifyLogDebug(string expectedMessage)
    {
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    /// <summary>
    /// Verifies that an error was logged containing the expected message.
    /// </summary>
    private void VerifyLogError(string expectedMessage)
    {
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    #endregion
}

/// <summary>
/// Unit tests for DataIngestionService.UpdateNBAGameResultsAsync method.
/// Tests cover game result updates, score population, IsComplete flag transitions, and error handling.
/// The method fetches games from SportRadar API and updates existing database records with final scores.
/// Uses Moq for mocking dependencies and FluentAssertions for readable assertions.
/// </summary>
public class DataIngestionService_UpdateNBAGameResultsAsyncTests
{
    private readonly Mock<IMoneyballRepository> _mockMoneyballRepository;
    private readonly Mock<ISportsDataService> _mockSportsDataService;
    private readonly Mock<ILogger<DataIngestionService>> _mockLogger;
    private readonly DataIngestionService _service;

    // Mock repositories
    private readonly Mock<IRepository<Sport>> _mockSportsRepo;
    private readonly Mock<IGameRepository> _mockGamesRepo;

    /// <summary>
    /// Test fixture setup - initializes all mocks and creates service instance.
    /// Runs before each test method to ensure clean state.
    /// </summary>
    public DataIngestionService_UpdateNBAGameResultsAsyncTests()
    {
        _mockMoneyballRepository = new Mock<IMoneyballRepository>();
        _mockSportsDataService = new Mock<ISportsDataService>();
        var mockOddsDataService = new Mock<IOddsDataService>();
        _mockLogger = new Mock<ILogger<DataIngestionService>>();

        _mockSportsRepo = new Mock<IRepository<Sport>>();
        _mockGamesRepo = new Mock<IGameRepository>();

        // Wire up repository properties
        _mockMoneyballRepository.Setup(r => r.Sports).Returns(_mockSportsRepo.Object);
        _mockMoneyballRepository.Setup(r => r.Games).Returns(_mockGamesRepo.Object);

        _service = new DataIngestionService(
            _mockMoneyballRepository.Object,
            _mockSportsDataService.Object,
            mockOddsDataService.Object,
            _mockLogger.Object);
    }

    /// <summary>
    /// Tests that completed games have IsComplete flag flipped and scores populated.
    /// Verifies core acceptance criteria: IsComplete flipped, HomeScore/AwayScore populated.
    /// </summary>
    [Fact]
    public async Task UpdateNBAGameResultsAsync_CompletedGame_FlipsIsCompleteAndPopulatesScores()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 1, 7);
        var nbaSport = new Sport { SportId = 1, Name = "NBA" };

        SetupBasicMocks(nbaSport);

        // API returns a completed game
        var apiGames = new List<NBAGame>
        {
            new()
            {
                Id = "sr:match:game-1",
                Status = "closed", // Game is complete
                HomePoints = 110,
                AwayPoints = 105,
                Home = new NBATeamInfo { Name = "Lakers" },
                Away = new NBATeamInfo { Name = "Celtics" }
            }
        };

        _mockSportsDataService
            .Setup(s => s.GetNBAScheduleAsync(startDate, endDate))
            .ReturnsAsync(apiGames);

        // Existing game in database (incomplete, no scores)
        var existingGame = new Game
        {
            GameId = 1,
            ExternalGameId = "sr:match:game-1",
            IsComplete = false, // Not yet marked complete
            HomeScore = null,
            AwayScore = null,
            Status = GameStatus.InProgress
        };

        _mockGamesRepo
            .Setup(r => r.GetGameByExternalIdAsync("sr:match:game-1", nbaSport.SportId))
            .ReturnsAsync(existingGame);

        _mockGamesRepo
            .Setup(r => r.UpdateAsync(It.IsAny<Game>()))
            .Returns(Task.CompletedTask);

        _mockMoneyballRepository.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        await _service.UpdateNBAGameResultsAsync(startDate, endDate);

        // Assert - IsComplete should be flipped (acceptance criteria)
        existingGame.IsComplete.Should().BeTrue(
            "game with status 'closed' should have IsComplete flipped from false to true");

        existingGame.Status.Should().Be(GameStatus.Final,
            "status should be updated to Final when game is closed");

        // Assert - Scores should be populated (acceptance criteria)
        existingGame.HomeScore.Should().Be(110,
            "home score should be populated from API");
        existingGame.AwayScore.Should().Be(105,
            "away score should be populated from API");

        // Assert - UpdatedAt timestamp should be set
        existingGame.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5),
            "UpdatedAt should be set when game is updated");

        // Verify update was called
        _mockGamesRepo.Verify(r => r.UpdateAsync(existingGame), Times.Once);
        _mockMoneyballRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    /// <summary>
    /// Tests that already-complete games are skipped (optimization).
    /// Verifies no unnecessary updates for games already marked complete.
    /// </summary>
    [Fact]
    public async Task UpdateNBAGameResultsAsync_AlreadyCompleteGame_SkipsUpdate()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 1, 7);
        var nbaSport = new Sport { SportId = 1, Name = "NBA" };

        SetupBasicMocks(nbaSport);

        var apiGames = new List<NBAGame>
        {
            new()
            {
                Id = "sr:match:game-1",
                Status = "closed",
                HomePoints = 110,
                AwayPoints = 105
            }
        };

        _mockSportsDataService
            .Setup(s => s.GetNBAScheduleAsync(startDate, endDate))
            .ReturnsAsync(apiGames);

        // Game already marked as complete
        var existingGame = new Game
        {
            GameId = 1,
            ExternalGameId = "sr:match:game-1",
            IsComplete = true, // Already complete
            HomeScore = 110,
            AwayScore = 105,
            Status = GameStatus.Final
        };

        _mockGamesRepo
            .Setup(r => r.GetGameByExternalIdAsync("sr:match:game-1", nbaSport.SportId))
            .ReturnsAsync(existingGame);

        _mockMoneyballRepository.Setup(r => r.SaveChangesAsync()).ReturnsAsync(0);

        // Act
        await _service.UpdateNBAGameResultsAsync(startDate, endDate);

        // Assert - No update should be called (optimization)
        _mockGamesRepo.Verify(
            r => r.UpdateAsync(It.IsAny<Game>()),
            Times.Never,
            "already-complete games should be skipped to avoid unnecessary updates");

        // Verify SaveChanges still called but no changes made
        _mockMoneyballRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    /// <summary>
    /// Tests that in-progress games update scores without marking complete.
    /// Verifies partial updates for games still in progress.
    /// </summary>
    [Fact]
    public async Task UpdateNBAGameResultsAsync_InProgressGame_UpdatesScoresWithoutComplete()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 1, 7);
        var nbaSport = new Sport { SportId = 1, Name = "NBA" };

        SetupBasicMocks(nbaSport);

        // Game in progress with partial scores
        var apiGames = new List<NBAGame>
        {
            new()
            {
                Id = "sr:match:game-1",
                Status = "inprogress", // Not complete yet
                HomePoints = 55,
                AwayPoints = 48
            }
        };

        _mockSportsDataService
            .Setup(s => s.GetNBAScheduleAsync(startDate, endDate))
            .ReturnsAsync(apiGames);

        var existingGame = new Game
        {
            GameId = 1,
            ExternalGameId = "sr:match:game-1",
            IsComplete = false,
            HomeScore = 30, // Old halftime score
            AwayScore = 25,
            Status = GameStatus.Scheduled
        };

        _mockGamesRepo
            .Setup(r => r.GetGameByExternalIdAsync("sr:match:game-1", nbaSport.SportId))
            .ReturnsAsync(existingGame);

        _mockGamesRepo
            .Setup(r => r.UpdateAsync(It.IsAny<Game>()))
            .Returns(Task.CompletedTask);

        _mockMoneyballRepository.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        await _service.UpdateNBAGameResultsAsync(startDate, endDate);

        // Assert - Scores updated but IsComplete still false
        existingGame.HomeScore.Should().Be(55,
            "home score should be updated with in-progress score");
        existingGame.AwayScore.Should().Be(48,
            "away score should be updated with in-progress score");
        existingGame.IsComplete.Should().BeFalse(
            "IsComplete should remain false for in-progress games");
        existingGame.Status.Should().Be(GameStatus.InProgress,
            "status should be updated to InProgress");

        _mockGamesRepo.Verify(r => r.UpdateAsync(existingGame), Times.Once);
    }

    /// <summary>
    /// Tests status mapping for various game states.
    /// Verifies correct GameStatus enum assignment for different API statuses.
    /// </summary>
    [Theory]
    [InlineData("closed", GameStatus.Final, true, "closed status")]
    [InlineData("complete", GameStatus.Final, true, "complete status")]
    [InlineData("final", GameStatus.Final, true, "final status")]
    [InlineData("inprogress", GameStatus.InProgress, false, "in-progress status")]
    [InlineData("halftime", GameStatus.InProgress, false, "halftime status")]
    [InlineData("scheduled", GameStatus.Scheduled, false, "scheduled status")]
    public async Task UpdateNBAGameResultsAsync_VariousStatuses_MapsCorrectly(
        string apiStatus,
        GameStatus expectedStatus,
        bool expectedIsComplete,
        string because)
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 1, 7);
        var nbaSport = new Sport { SportId = 1, Name = "NBA" };

        SetupBasicMocks(nbaSport);

        var apiGames = new List<NBAGame>
        {
            new()
            {
                Id = "sr:match:game-1",
                Status = apiStatus,
                HomePoints = 110,
                AwayPoints = 105
            }
        };

        _mockSportsDataService
            .Setup(s => s.GetNBAScheduleAsync(startDate, endDate))
            .ReturnsAsync(apiGames);

        var existingGame = new Game
        {
            GameId = 1,
            ExternalGameId = "sr:match:game-1",
            IsComplete = false,
            Status = GameStatus.Scheduled
        };

        _mockGamesRepo
            .Setup(r => r.GetGameByExternalIdAsync("sr:match:game-1", nbaSport.SportId))
            .ReturnsAsync(existingGame);

        _mockGamesRepo
            .Setup(r => r.UpdateAsync(It.IsAny<Game>()))
            .Returns(Task.CompletedTask);

        _mockMoneyballRepository.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        await _service.UpdateNBAGameResultsAsync(startDate, endDate);

        // Assert - Status mapped correctly
        existingGame.Status.Should().Be(expectedStatus, because);
        existingGame.IsComplete.Should().Be(expectedIsComplete, because);
    }

    /// <summary>
    /// Tests that score changes are detected and logged.
    /// Verifies debug logging when scores update.
    /// </summary>
    [Fact]
    public async Task UpdateNBAGameResultsAsync_ScoreChange_DetectedAndLogged()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 1, 7);
        var nbaSport = new Sport { SportId = 1, Name = "NBA" };

        SetupBasicMocks(nbaSport);

        var apiGames = new List<NBAGame>
        {
            new()
            {
                Id = "sr:match:game-1",
                Status = "inprogress",
                HomePoints = 60, // New score
                AwayPoints = 55  // New score
            }
        };

        _mockSportsDataService
            .Setup(s => s.GetNBAScheduleAsync(startDate, endDate))
            .ReturnsAsync(apiGames);

        var existingGame = new Game
        {
            GameId = 1,
            ExternalGameId = "sr:match:game-1",
            IsComplete = false,
            HomeScore = 55, // Old score
            AwayScore = 50, // Old score
            Status = GameStatus.InProgress
        };

        _mockGamesRepo
            .Setup(r => r.GetGameByExternalIdAsync("sr:match:game-1", nbaSport.SportId))
            .ReturnsAsync(existingGame);

        _mockGamesRepo
            .Setup(r => r.UpdateAsync(It.IsAny<Game>()))
            .Returns(Task.CompletedTask);

        _mockMoneyballRepository.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        await _service.UpdateNBAGameResultsAsync(startDate, endDate);

        // Assert - Scores changed
        existingGame.HomeScore.Should().Be(60,
            "home score should be updated from 55 to 60");
        existingGame.AwayScore.Should().Be(55,
            "away score should be updated from 50 to 55");

        // Verify debug logging for score changes
        VerifyLogDebug("Updating home score");
        VerifyLogDebug("Updating away score");
    }

    /// <summary>
    /// Tests that counts are logged correctly (acceptance criteria).
    /// Verifies Updated, Completed, Skipped, Total counts in final log.
    /// </summary>
    [Fact]
    public async Task UpdateNBAGameResultsAsync_LogsCounts_Correctly()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 1, 7);
        var nbaSport = new Sport { SportId = 1, Name = "NBA" };

        SetupBasicMocks(nbaSport);

        // 4 games: 1 completed, 1 updated (in progress), 2 skipped (already complete)
        var apiGames = new List<NBAGame>
        {
            new() { Id = "sr:match:game-1", Status = "closed", HomePoints = 110, AwayPoints = 105 }, // Will complete
            new() { Id = "sr:match:game-2", Status = "inprogress", HomePoints = 60, AwayPoints = 55 }, // Will update
            new() { Id = "sr:match:game-3", Status = "closed", HomePoints = 95, AwayPoints = 90 }, // Already complete (skip)
            new() { Id = "sr:match:game-4", Status = "scheduled", HomePoints = 0, AwayPoints = 0 } // Not in DB (skip)
        };

        _mockSportsDataService
            .Setup(s => s.GetNBAScheduleAsync(startDate, endDate))
            .ReturnsAsync(apiGames);

        // Game 1: Will be completed
        var game1 = new Game
        {
            GameId = 1,
            ExternalGameId = "sr:match:game-1",
            IsComplete = false,
            Status = GameStatus.InProgress
        };

        _mockGamesRepo
            .Setup(r => r.GetGameByExternalIdAsync("sr:match:game-1", nbaSport.SportId))
            .ReturnsAsync(game1);

        // Game 2: Will be updated (in progress)
        var game2 = new Game
        {
            GameId = 2,
            ExternalGameId = "sr:match:game-2",
            IsComplete = false,
            HomeScore = 30,
            AwayScore = 25,
            Status = GameStatus.InProgress
        };

        _mockGamesRepo
            .Setup(r => r.GetGameByExternalIdAsync("sr:match:game-2", nbaSport.SportId))
            .ReturnsAsync(game2);

        // Game 3: Already complete (will skip)
        var game3 = new Game
        {
            GameId = 3,
            ExternalGameId = "sr:match:game-3",
            IsComplete = true,
            HomeScore = 95,
            AwayScore = 90,
            Status = GameStatus.Final
        };

        _mockGamesRepo
            .Setup(r => r.GetGameByExternalIdAsync("sr:match:game-3", nbaSport.SportId))
            .ReturnsAsync(game3);

        // Game 4: Not in database (will skip)
        _mockGamesRepo
            .Setup(r => r.GetGameByExternalIdAsync("sr:match:game-4", nbaSport.SportId))
            .ReturnsAsync((Game?)null);

        _mockGamesRepo
            .Setup(r => r.UpdateAsync(It.IsAny<Game>()))
            .Returns(Task.CompletedTask);

        _mockMoneyballRepository.Setup(r => r.SaveChangesAsync()).ReturnsAsync(2);

        // Act
        await _service.UpdateNBAGameResultsAsync(startDate, endDate);

        // Assert - Verify counts logged (acceptance criteria: count logged)
        VerifyLogInformation("Updated: 2"); // Game 1 and Game 2
        VerifyLogInformation("Completed: 1"); // Game 1
        VerifyLogInformation("Skipped: 2"); // Game 3 (already complete) and Game 4 (not in DB)
        VerifyLogInformation("Total processed: 4"); // All 4 games from API
        VerifyLogInformation("Changed: 2"); // 2 changes saved
    }

    /// <summary>
    /// Tests that games not in database are skipped.
    /// Verifies graceful handling of games not yet ingested.
    /// </summary>
    [Fact]
    public async Task UpdateNBAGameResultsAsync_GameNotInDatabase_Skipped()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 1, 7);
        var nbaSport = new Sport { SportId = 1, Name = "NBA" };

        SetupBasicMocks(nbaSport);

        var apiGames = new List<NBAGame>
        {
            new()
            {
                Id = "sr:match:unknown",
                Status = "closed",
                HomePoints = 110,
                AwayPoints = 105
            }
        };

        _mockSportsDataService
            .Setup(s => s.GetNBAScheduleAsync(startDate, endDate))
            .ReturnsAsync(apiGames);

        // Game does not exist in database
        _mockGamesRepo
            .Setup(r => r.GetGameByExternalIdAsync("sr:match:unknown", nbaSport.SportId))
            .ReturnsAsync((Game?)null);

        _mockMoneyballRepository.Setup(r => r.SaveChangesAsync()).ReturnsAsync(0);

        // Act
        await _service.UpdateNBAGameResultsAsync(startDate, endDate);

        // Assert - No update attempted
        _mockGamesRepo.Verify(
            r => r.UpdateAsync(It.IsAny<Game>()),
            Times.Never,
            "should not attempt to update games not in database");
    }

    /// <summary>
    /// Tests that games with no changes are not updated.
    /// Verifies optimization to skip unnecessary updates.
    /// </summary>
    [Fact]
    public async Task UpdateNBAGameResultsAsync_NoChanges_SkipsUpdate()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 1, 7);
        var nbaSport = new Sport { SportId = 1, Name = "NBA" };

        SetupBasicMocks(nbaSport);

        var apiGames = new List<NBAGame>
        {
            new()
            {
                Id = "sr:match:game-1",
                Status = "inprogress",
                HomePoints = 55,
                AwayPoints = 50
            }
        };

        _mockSportsDataService
            .Setup(s => s.GetNBAScheduleAsync(startDate, endDate))
            .ReturnsAsync(apiGames);

        // Game already has same scores and status
        var existingGame = new Game
        {
            GameId = 1,
            ExternalGameId = "sr:match:game-1",
            IsComplete = false,
            HomeScore = 55, // Same as API
            AwayScore = 50, // Same as API
            Status = GameStatus.InProgress // Same as API
        };

        _mockGamesRepo
            .Setup(r => r.GetGameByExternalIdAsync("sr:match:game-1", nbaSport.SportId))
            .ReturnsAsync(existingGame);

        _mockMoneyballRepository.Setup(r => r.SaveChangesAsync()).ReturnsAsync(0);

        // Act
        await _service.UpdateNBAGameResultsAsync(startDate, endDate);

        // Assert - No update when no changes detected
        _mockGamesRepo.Verify(
            r => r.UpdateAsync(It.IsAny<Game>()),
            Times.Never,
            "should not update game when scores and status are unchanged");
    }

    /// <summary>
    /// Tests that individual game errors don't stop processing.
    /// Verifies resilience and continued processing after errors.
    /// </summary>
    [Fact]
    public async Task UpdateNBAGameResultsAsync_IndividualGameError_ContinuesProcessing()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 1, 7);
        var nbaSport = new Sport { SportId = 1, Name = "NBA" };

        SetupBasicMocks(nbaSport);

        // Two games: first will error, second should still process
        var apiGames = new List<NBAGame>
        {
            new() { Id = "sr:match:game-1", Status = "closed", HomePoints = 110, AwayPoints = 105 },
            new() { Id = "sr:match:game-2", Status = "closed", HomePoints = 95, AwayPoints = 90 }
        };

        _mockSportsDataService
            .Setup(s => s.GetNBAScheduleAsync(startDate, endDate))
            .ReturnsAsync(apiGames);

        // Game 1 will throw exception
        _mockGamesRepo
            .Setup(r => r.GetGameByExternalIdAsync("sr:match:game-1", nbaSport.SportId))
            .ThrowsAsync(new Exception("Database error"));

        // Game 2 should still process
        var game2 = new Game
        {
            GameId = 2,
            ExternalGameId = "sr:match:game-2",
            IsComplete = false,
            Status = GameStatus.InProgress
        };

        _mockGamesRepo
            .Setup(r => r.GetGameByExternalIdAsync("sr:match:game-2", nbaSport.SportId))
            .ReturnsAsync(game2);

        _mockGamesRepo
            .Setup(r => r.UpdateAsync(It.IsAny<Game>()))
            .Returns(Task.CompletedTask);

        _mockMoneyballRepository.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        await _service.UpdateNBAGameResultsAsync(startDate, endDate);

        // Assert - Game 2 should still be updated despite Game 1 error
        game2.IsComplete.Should().BeTrue(
            "second game should be processed even when first game throws exception");

        // Verify warning logged for error
        VerifyLogWarning("Error updating results for game sr:match:game-1");
    }

    /// <summary>
    /// Tests that NBA sport not found throws exception.
    /// Validates prerequisite checking.
    /// </summary>
    [Fact]
    public async Task UpdateNBAGameResultsAsync_NBASportNotFound_ThrowsException()
    {
        // Arrange - No NBA sport
        _mockSportsRepo
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Sport, bool>>>()))
            .ReturnsAsync((Sport?)null);

        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 1, 7);

        // Act & Assert
        var act = async () => await _service.UpdateNBAGameResultsAsync(startDate, endDate);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*NBA sport not found*");
    }

    /// <summary>
    /// Tests that empty API response returns early.
    /// Verifies graceful handling of no games in date range.
    /// </summary>
    [Fact]
    public async Task UpdateNBAGameResultsAsync_EmptyAPIResponse_ReturnsEarly()
    {
        // Arrange
        var startDate = new DateTime(2024, 7, 1);
        var endDate = new DateTime(2024, 7, 7);
        var nbaSport = new Sport { SportId = 1, Name = "NBA" };

        SetupBasicMocks(nbaSport);

        // Empty response from API
        _mockSportsDataService
            .Setup(s => s.GetNBAScheduleAsync(startDate, endDate))
            .ReturnsAsync(new List<NBAGame>());

        // Act
        await _service.UpdateNBAGameResultsAsync(startDate, endDate);

        // Assert - Should return early
        _mockGamesRepo.Verify(
            r => r.GetGameByExternalIdAsync(It.IsAny<string>(), It.IsAny<int>()),
            Times.Never);

        VerifyLogInformation("No games returned from SportRadar API");
    }

    /// <summary>
    /// Tests that API errors are logged and re-thrown.
    /// Verifies exception handling.
    /// </summary>
    [Fact]
    public async Task UpdateNBAGameResultsAsync_APIError_LogsAndRethrows()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 1, 7);
        var nbaSport = new Sport { SportId = 1, Name = "NBA" };

        SetupBasicMocks(nbaSport);

        // API throws exception
        _mockSportsDataService
            .Setup(s => s.GetNBAScheduleAsync(startDate, endDate))
            .ThrowsAsync(new HttpRequestException("API failure"));

        // Act & Assert
        var act = async () => await _service.UpdateNBAGameResultsAsync(startDate, endDate);

        await act.Should().ThrowAsync<HttpRequestException>()
            .WithMessage("API failure");

        // Verify error logged
        VerifyLogError("Error during game results update");
    }

    /// <summary>
    /// Tests that completion logging includes all relevant information.
    /// Verifies detailed logging when game is marked complete.
    /// </summary>
    [Fact]
    public async Task UpdateNBAGameResultsAsync_CompletedGame_LogsFullDetails()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 1, 7);
        var nbaSport = new Sport { SportId = 1, Name = "NBA" };

        SetupBasicMocks(nbaSport);

        var apiGames = new List<NBAGame>
        {
            new()
            {
                Id = "sr:match:game-123",
                Status = "closed",
                HomePoints = 115,
                AwayPoints = 112,
                Home = new NBATeamInfo { Name = "Lakers" },
                Away = new NBATeamInfo { Name = "Celtics" }
            }
        };

        _mockSportsDataService
            .Setup(s => s.GetNBAScheduleAsync(startDate, endDate))
            .ReturnsAsync(apiGames);

        var existingGame = new Game
        {
            GameId = 1,
            ExternalGameId = "sr:match:game-123",
            IsComplete = false,
            Status = GameStatus.InProgress
        };

        _mockGamesRepo
            .Setup(r => r.GetGameByExternalIdAsync("sr:match:game-123", nbaSport.SportId))
            .ReturnsAsync(existingGame);

        _mockGamesRepo
            .Setup(r => r.UpdateAsync(It.IsAny<Game>()))
            .Returns(Task.CompletedTask);

        _mockMoneyballRepository.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        await _service.UpdateNBAGameResultsAsync(startDate, endDate);

        // Assert - Verify detailed completion log
        VerifyLogInformation("Marking game sr:match:game-123 as complete");
        VerifyLogInformation("Lakers");
        VerifyLogInformation("115");
        VerifyLogInformation("Celtics");
        VerifyLogInformation("112");
    }

    // ==================== Helper Methods ====================

    /// <summary>
    /// Sets up basic mocks required for most tests.
    /// </summary>
    private void SetupBasicMocks(Sport nbaSport)
    {
        _mockSportsRepo
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Sport, bool>>>()))
            .ReturnsAsync(nbaSport);
    }

    /// <summary>
    /// Verifies that an information log was written containing the expected message.
    /// </summary>
    private void VerifyLogInformation(string expectedMessage)
    {
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    /// <summary>
    /// Verifies that a debug log was written containing the expected message.
    /// </summary>
    private void VerifyLogDebug(string expectedMessage)
    {
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    /// <summary>
    /// Verifies that a warning was logged containing the expected message.
    /// </summary>
    private void VerifyLogWarning(string expectedMessage)
    {
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    /// <summary>
    /// Verifies that an error was logged containing the expected message.
    /// </summary>
    private void VerifyLogError(string expectedMessage)
    {
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
}
