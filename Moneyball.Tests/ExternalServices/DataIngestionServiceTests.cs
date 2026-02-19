using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moneyball.Core.DTOs;
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
    private readonly Mock<IOddsDataService> _mockOddsDataService;
    private readonly Mock<ILogger<DataIngestionService>> _mockLogger;
    private readonly DataIngestionService _service;

    // Mock repositories
    private readonly Mock<IRepository<Sport>> _mockSportsRepo;
    private readonly Mock<ITeamRepository> _mockTeamsRepo;

    public DataIngestionService_IngestNBATeamsAsyncTests()
    {
        _mockMoneyballRepository = new Mock<IMoneyballRepository>();
        _mockSportsDataService = new Mock<ISportsDataService>();
        _mockOddsDataService = new Mock<IOddsDataService>();
        _mockLogger = new Mock<ILogger<DataIngestionService>>();

        _mockSportsRepo = new Mock<IRepository<Sport>>();
        _mockTeamsRepo = new Mock<ITeamRepository>();

        // Wire up unit of work
        _mockMoneyballRepository.Setup(u => u.Sports).Returns(_mockSportsRepo.Object);
        _mockMoneyballRepository.Setup(u => u.Teams).Returns(_mockTeamsRepo.Object);

        _service = new DataIngestionService(
            _mockMoneyballRepository.Object,
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
    private readonly Mock<IOddsDataService> _mockOddsDataService;
    private readonly Mock<ILogger<DataIngestionService>> _mockLogger;
    private readonly DataIngestionService _service;

    // Mock repositories
    private readonly Mock<IRepository<Sport>> _mockSportsRepo;
    private readonly Mock<ITeamRepository> _mockTeamsRepo;
    private readonly Mock<IGameRepository> _mockGamesRepo;

    public DataIngestionService_IngestNBAScheduleAsyncTests()
    {
        _mockMoneyballRepository = new Mock<IMoneyballRepository>();
        _mockSportsDataService = new Mock<ISportsDataService>();
        _mockOddsDataService = new Mock<IOddsDataService>();
        _mockLogger = new Mock<ILogger<DataIngestionService>>();

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
            _mockOddsDataService.Object,
            _mockLogger.Object);
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
