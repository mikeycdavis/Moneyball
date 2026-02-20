using FluentAssertions;
using Microsoft.Extensions.Logging;
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

public class DataIngestionService_IngestNBAGameStatisticsAsyncTests
{
    private readonly Mock<IMoneyballRepository> _mockMoneyballRepository;
    private readonly Mock<ISportsDataService> _mockSportsDataService;
    private readonly DataIngestionService _service;

    // Mock repositories
    private readonly Mock<IRepository<Sport>> _mockSportsRepo;
    private readonly Mock<IGameRepository> _mockGamesRepo;
    private readonly Mock<IRepository<TeamStatistic>> _mockTeamStatsRepo;

    public DataIngestionService_IngestNBAGameStatisticsAsyncTests()
    {
        _mockMoneyballRepository = new Mock<IMoneyballRepository>();
        _mockSportsDataService = new Mock<ISportsDataService>();
        var mockOddsDataService = new Mock<IOddsDataService>();
        var mockLogger = new Mock<ILogger<DataIngestionService>>();

        _mockSportsRepo = new Mock<IRepository<Sport>>();
        _mockGamesRepo = new Mock<IGameRepository>();
        _mockTeamStatsRepo = new Mock<IRepository<TeamStatistic>>();

        // Wire up unit of work
        _mockMoneyballRepository.Setup(u => u.Sports).Returns(_mockSportsRepo.Object);
        _mockMoneyballRepository.Setup(u => u.Games).Returns(_mockGamesRepo.Object);
        _mockMoneyballRepository.Setup(u => u.TeamStatistics).Returns(_mockTeamStatsRepo.Object);

        _service = new DataIngestionService(
            _mockMoneyballRepository.Object,
            _mockSportsDataService.Object,
            mockOddsDataService.Object,
            mockLogger.Object);
    }

    [Fact]
    public async Task IngestNBAGameStatisticsAsync_NewStatistics_CreatesBothHomeAndAway()
    {
        // Arrange
        var gameId = "game-123";
        var nbaSport = new Sport { SportId = 1, Name = "NBA" };
        var game = new Game { GameId = 1, ExternalGameId = gameId, HomeTeamId = 10, AwayTeamId = 20 };

        SetupBasicMocks(nbaSport, game);

        var statistics = CreateTestStatistics();
        _mockSportsDataService
            .Setup(s => s.GetNBAGameStatisticsAsync(gameId))
            .ReturnsAsync(statistics);

        // No existing statistics (FirstOrDefaultAsync returns null)
        _mockTeamStatsRepo
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<TeamStatistic, bool>>>()))
            .ReturnsAsync((TeamStatistic?)null);

        var addedStats = new List<TeamStatistic>();
        _mockTeamStatsRepo
            .Setup(r => r.AddAsync(It.IsAny<TeamStatistic>()))
            .Callback<TeamStatistic>(s => addedStats.Add(s))
            .ReturnsAsync((TeamStatistic s) => s);

        _mockMoneyballRepository.Setup(u => u.SaveChangesAsync()).ReturnsAsync(2);

        // Act
        await _service.IngestNBAGameStatisticsAsync(gameId);

        // Assert
        addedStats.Should().HaveCount(2, "both home and away statistics should be created");

        var homeStat = addedStats.FirstOrDefault(s => s.IsHomeTeam);
        var awayStat = addedStats.FirstOrDefault(s => !s.IsHomeTeam);

        homeStat.Should().NotBeNull();
        awayStat.Should().NotBeNull();

        // Verify home team stats
        homeStat.GameId.Should().Be(1);
        homeStat.TeamId.Should().Be(10);
        homeStat.IsHomeTeam.Should().BeTrue();
        homeStat.Points.Should().Be(110);
        homeStat.Assists.Should().Be(25);

        // Verify away team stats
        awayStat.GameId.Should().Be(1);
        awayStat.TeamId.Should().Be(20);
        awayStat.IsHomeTeam.Should().BeFalse();
        awayStat.Points.Should().Be(105);
        awayStat.Assists.Should().Be(22);
    }

    [Fact]
    public async Task IngestNBAGameStatisticsAsync_AllFieldsMapped_MapsCorrectly()
    {
        // Arrange
        var gameId = "game-123";
        var nbaSport = new Sport { SportId = 1, Name = "NBA" };
        var game = new Game { GameId = 1, ExternalGameId = gameId, HomeTeamId = 10, AwayTeamId = 20 };

        SetupBasicMocks(nbaSport, game);

        var statistics = CreateTestStatistics();
        _mockSportsDataService
            .Setup(s => s.GetNBAGameStatisticsAsync(gameId))
            .ReturnsAsync(statistics);

        _mockTeamStatsRepo
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<TeamStatistic, bool>>>()))
            .ReturnsAsync((TeamStatistic?)null);

        var addedStats = new List<TeamStatistic>();
        _mockTeamStatsRepo
            .Setup(r => r.AddAsync(It.IsAny<TeamStatistic>()))
            .Callback<TeamStatistic>(s => addedStats.Add(s))
            .ReturnsAsync((TeamStatistic s) => s);

        _mockMoneyballRepository.Setup(u => u.SaveChangesAsync()).ReturnsAsync(2);

        // Act
        await _service.IngestNBAGameStatisticsAsync(gameId);

        // Assert - verify ALL fields mapped (acceptance criteria)
        var homeStat = addedStats.First(s => s.IsHomeTeam);

        homeStat.Points.Should().Be(110);
        homeStat.FieldGoalsMade.Should().Be(42);
        homeStat.FieldGoalsAttempted.Should().Be(88);
        homeStat.FieldGoalPercentage.Should().Be(0.477m);
        homeStat.ThreePointsMade.Should().Be(12);
        homeStat.ThreePointsAttempted.Should().Be(35);
        homeStat.ThreePointPercentage.Should().Be(0.343m);
        homeStat.FreeThrowsMade.Should().Be(14);
        homeStat.FreeThrowsAttempted.Should().Be(18);
        homeStat.FreeThrowPercentage.Should().Be(0.778m);
        homeStat.Rebounds.Should().Be(48);
        homeStat.OffensiveRebounds.Should().Be(10);
        homeStat.DefensiveRebounds.Should().Be(38);
        homeStat.Assists.Should().Be(25);
        homeStat.Steals.Should().Be(8);
        homeStat.Blocks.Should().Be(5);
        homeStat.Turnovers.Should().Be(12);
        homeStat.PersonalFouls.Should().Be(20);
    }

    [Fact]
    public async Task IngestNBAGameStatisticsAsync_ExistingStatistics_UpdatesStaleRows()
    {
        // Arrange
        var gameId = "game-123";
        var nbaSport = new Sport { SportId = 1, Name = "NBA" };
        var game = new Game { GameId = 1, ExternalGameId = gameId, HomeTeamId = 10, AwayTeamId = 20 };

        SetupBasicMocks(nbaSport, game);

        var statistics = CreateTestStatistics();
        _mockSportsDataService
            .Setup(s => s.GetNBAGameStatisticsAsync(gameId))
            .ReturnsAsync(statistics);

        // Existing stale statistics
        var existingHomeStat = new TeamStatistic
        {
            TeamStatisticId = 1,
            GameId = 1,
            TeamId = 10,
            IsHomeTeam = true,
            Points = 50, // Stale halftime score
            Assists = 12,
            Rebounds = 20
        };

        var existingAwayStat = new TeamStatistic
        {
            TeamStatisticId = 2,
            GameId = 1,
            TeamId = 20,
            IsHomeTeam = false,
            Points = 48,
            Assists = 10,
            Rebounds = 18
        };

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

        _mockMoneyballRepository.Setup(u => u.SaveChangesAsync()).ReturnsAsync(2);

        // Act
        await _service.IngestNBAGameStatisticsAsync(gameId);

        // Assert - verify stale rows replaced (acceptance criteria)
        updatedStats.Should().HaveCount(2, "both home and away statistics should be updated");

        existingHomeStat.Points.Should().Be(110, "stale score should be replaced");
        existingHomeStat.Assists.Should().Be(25, "stale assists should be replaced");
        existingHomeStat.Rebounds.Should().Be(48, "stale rebounds should be replaced");

        existingAwayStat.Points.Should().Be(105);
        existingAwayStat.Assists.Should().Be(22);
        existingAwayStat.Rebounds.Should().Be(44);

        _mockTeamStatsRepo.Verify(r => r.AddAsync(It.IsAny<TeamStatistic>()), Times.Never,
            "should not add new rows when updating existing");
    }

    [Fact]
    public async Task IngestNBAGameStatisticsAsync_NullOrEmptyGameId_ThrowsArgumentException()
    {
        // Act & Assert - null
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.IngestNBAGameStatisticsAsync(null!));
        exception.ParamName.Should().Be("externalGameId");

        // Act & Assert - empty
        exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.IngestNBAGameStatisticsAsync(""));
        exception.ParamName.Should().Be("externalGameId");

        // Act & Assert - whitespace
        exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.IngestNBAGameStatisticsAsync("   "));
        exception.ParamName.Should().Be("externalGameId");
    }

    [Fact]
    public async Task IngestNBAGameStatisticsAsync_NBASportNotFound_ThrowsException()
    {
        // Arrange
        _mockSportsRepo
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Sport, bool>>>()))
            .ReturnsAsync((Sport?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.IngestNBAGameStatisticsAsync("game-123"));

        exception.Message.Should().Contain("NBA sport not found");
    }

    [Fact]
    public async Task IngestNBAGameStatisticsAsync_GameNotFound_ThrowsException()
    {
        // Arrange
        var gameId = "nonexistent-game";
        var nbaSport = new Sport { SportId = 1, Name = "NBA" };

        _mockSportsRepo
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Sport, bool>>>()))
            .ReturnsAsync(nbaSport);

        _mockGamesRepo
            .Setup(r => r.GetGameByExternalIdAsync(gameId, 1))
            .ReturnsAsync((Game?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.IngestNBAGameStatisticsAsync(gameId));

        exception.Message.Should().Contain("not found in database");
        exception.Message.Should().Contain("IngestNBAScheduleAsync");
    }

    [Fact]
    public async Task IngestNBAGameStatisticsAsync_APIReturnsNull_LogsWarningAndReturns()
    {
        // Arrange
        var gameId = "game-123";
        var nbaSport = new Sport { SportId = 1, Name = "NBA" };
        var game = new Game { GameId = 1, ExternalGameId = gameId, HomeTeamId = 10, AwayTeamId = 20 };

        SetupBasicMocks(nbaSport, game);

        _mockSportsDataService
            .Setup(s => s.GetNBAGameStatisticsAsync(gameId))
            .ReturnsAsync((NBAGameStatistics?)null);

        // Act
        await _service.IngestNBAGameStatisticsAsync(gameId);

        // Assert
        _mockTeamStatsRepo.Verify(r => r.AddAsync(It.IsAny<TeamStatistic>()), Times.Never);
        _mockMoneyballRepository.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task IngestNBAGameStatisticsAsync_PartialUpdate_HomeExists_AwayNew()
    {
        // Arrange
        var gameId = "game-123";
        var nbaSport = new Sport { SportId = 1, Name = "NBA" };
        var game = new Game { GameId = 1, ExternalGameId = gameId, HomeTeamId = 10, AwayTeamId = 20 };

        SetupBasicMocks(nbaSport, game);

        var statistics = CreateTestStatistics();
        _mockSportsDataService
            .Setup(s => s.GetNBAGameStatisticsAsync(gameId))
            .ReturnsAsync(statistics);

        // Home exists, away is new
        var existingHomeStat = new TeamStatistic
        {
            TeamStatisticId = 1,
            GameId = 1,
            TeamId = 10,
            IsHomeTeam = true
        };

        _mockTeamStatsRepo
            .Setup(r => r.FirstOrDefaultAsync(It.Is<Expression<Func<TeamStatistic, bool>>>(
                expr => expr.Compile()(existingHomeStat))))
            .ReturnsAsync(existingHomeStat);

        _mockTeamStatsRepo
            .Setup(r => r.FirstOrDefaultAsync(It.Is<Expression<Func<TeamStatistic, bool>>>(
                expr => !expr.Compile()(existingHomeStat))))
            .ReturnsAsync((TeamStatistic?)null);

        var addedStats = new List<TeamStatistic>();
        _mockTeamStatsRepo
            .Setup(r => r.AddAsync(It.IsAny<TeamStatistic>()))
            .Callback<TeamStatistic>(s => addedStats.Add(s))
            .ReturnsAsync((TeamStatistic s) => s);

        _mockTeamStatsRepo
            .Setup(r => r.UpdateAsync(It.IsAny<TeamStatistic>()))
            .Returns(Task.CompletedTask);

        _mockMoneyballRepository.Setup(u => u.SaveChangesAsync()).ReturnsAsync(2);

        // Act
        await _service.IngestNBAGameStatisticsAsync(gameId);

        // Assert
        addedStats.Should().ContainSingle("only away stats should be added");
        addedStats[0].IsHomeTeam.Should().BeFalse();
        _mockTeamStatsRepo.Verify(r => r.UpdateAsync(It.IsAny<TeamStatistic>()), Times.Once,
            "home stats should be updated");
    }

    [Fact]
    public async Task IngestNBAGameStatisticsAsync_MultipleCallsSameGame_AlwaysUpdates()
    {
        // Arrange - simulate calling the method twice (e.g., halftime and final)
        var gameId = "game-123";
        var nbaSport = new Sport { SportId = 1, Name = "NBA" };
        var game = new Game { GameId = 1, ExternalGameId = gameId, HomeTeamId = 10, AwayTeamId = 20 };

        SetupBasicMocks(nbaSport, game);

        // First call - halftime stats
        var halftimeStats = CreateTestStatistics();
        halftimeStats.Home.Statistics.Points = 55;
        halftimeStats.Away.Statistics.Points = 50;

        _mockSportsDataService
            .Setup(s => s.GetNBAGameStatisticsAsync(gameId))
            .ReturnsAsync(halftimeStats);

        _mockTeamStatsRepo
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<TeamStatistic, bool>>>()))
            .ReturnsAsync((TeamStatistic?)null);

        var addedStats = new List<TeamStatistic>();
        _mockTeamStatsRepo
            .Setup(r => r.AddAsync(It.IsAny<TeamStatistic>()))
            .Callback<TeamStatistic>(s => addedStats.Add(s))
            .ReturnsAsync((TeamStatistic s) => s);

        _mockMoneyballRepository.Setup(u => u.SaveChangesAsync()).ReturnsAsync(2);

        // Act - first call
        await _service.IngestNBAGameStatisticsAsync(gameId);

        // Assert first call
        addedStats.Should().HaveCount(2);
        var homeStatFirst = addedStats.First(s => s.IsHomeTeam);
        homeStatFirst.Points.Should().Be(55);

        // Arrange - second call with final stats
        var finalStats = CreateTestStatistics();
        finalStats.Home.Statistics.Points = 110;
        finalStats.Away.Statistics.Points = 105;

        _mockSportsDataService
            .Setup(s => s.GetNBAGameStatisticsAsync(gameId))
            .ReturnsAsync(finalStats);

        // Now return the previously added stats as existing
        _mockTeamStatsRepo
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<TeamStatistic, bool>>>()))
            .ReturnsAsync((Expression<Func<TeamStatistic, bool>> expr) =>
                addedStats.FirstOrDefault(s => expr.Compile()(s)));

        var updatedStats = new List<TeamStatistic>();
        _mockTeamStatsRepo
            .Setup(r => r.UpdateAsync(It.IsAny<TeamStatistic>()))
            .Callback<TeamStatistic>(s => updatedStats.Add(s))
            .Returns(Task.CompletedTask);

        // Act - second call
        await _service.IngestNBAGameStatisticsAsync(gameId);

        // Assert second call - stale rows replaced
        updatedStats.Should().HaveCount(2, "both stats should be updated on second call");
        homeStatFirst.Points.Should().Be(110, "halftime score should be replaced with final");
    }

    [Fact]
    public async Task IngestNBAGameStatisticsAsync_ZeroValues_StoresCorrectly()
    {
        // Arrange
        var gameId = "game-123";
        var nbaSport = new Sport { SportId = 1, Name = "NBA" };
        var game = new Game { GameId = 1, ExternalGameId = gameId, HomeTeamId = 10, AwayTeamId = 20 };

        SetupBasicMocks(nbaSport, game);

        var statistics = new NBAGameStatistics
        {
            Id = gameId,
            Home = new NBATeamStatistics
            {
                Id = "home-team",
                Name = "Home Team",
                Statistics = new NBAStatistics
                {
                    Points = 0,
                    FieldGoalsMade = 0,
                    Assists = 0,
                    Rebounds = 0,
                    Turnovers = 0
                }
            },
            Away = new NBATeamStatistics
            {
                Id = "away-team",
                Name = "Away Team",
                Statistics = new NBAStatistics
                {
                    Points = 0,
                    FieldGoalsMade = 0,
                    Assists = 0,
                    Rebounds = 0,
                    Turnovers = 0
                }
            }
        };

        _mockSportsDataService
            .Setup(s => s.GetNBAGameStatisticsAsync(gameId))
            .ReturnsAsync(statistics);

        _mockTeamStatsRepo
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<TeamStatistic, bool>>>()))
            .ReturnsAsync((TeamStatistic?)null);

        var addedStats = new List<TeamStatistic>();
        _mockTeamStatsRepo
            .Setup(r => r.AddAsync(It.IsAny<TeamStatistic>()))
            .Callback<TeamStatistic>(s => addedStats.Add(s))
            .ReturnsAsync((TeamStatistic s) => s);

        _mockMoneyballRepository.Setup(u => u.SaveChangesAsync()).ReturnsAsync(2);

        // Act
        await _service.IngestNBAGameStatisticsAsync(gameId);

        // Assert - zero values should be stored (not treated as null/missing)
        addedStats.Should().HaveCount(2);
        addedStats.Should().AllSatisfy(s =>
        {
            s.Points.Should().Be(0);
            s.Assists.Should().Be(0);
            s.Rebounds.Should().Be(0);
        });
    }

    // Helper methods
    private void SetupBasicMocks(Sport nbaSport, Game game)
    {
        _mockSportsRepo
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Sport, bool>>>()))
            .ReturnsAsync(nbaSport);

        _mockGamesRepo
            .Setup(r => r.GetGameByExternalIdAsync(game.ExternalGameId!, nbaSport.SportId))
            .ReturnsAsync(game);
    }

    private static NBAGameStatistics CreateTestStatistics()
    {
        return new NBAGameStatistics
        {
            Id = "game-123",
            Home = new NBATeamStatistics
            {
                Id = "home-team",
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
                Id = "away-team",
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
                    Turnovers = 14,
                    PersonalFouls = 18
                }
            }
        };
    }
}
