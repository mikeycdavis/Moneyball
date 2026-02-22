using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moneyball.API.Controllers;
using Moneyball.Core.Interfaces.ExternalServices;
using Moq;

namespace Moneyball.Tests.Controllers;

/// <summary>
/// Unit tests for DataIngestionController.
/// Tests cover all endpoints for success (200 OK) and failure (500) paths,
/// default date range logic, and correct delegation to service/orchestrator.
/// Uses Moq for mocking dependencies and FluentAssertions for readable assertions.
/// </summary>
public class DataIngestionControllerTests
{
    private readonly Mock<IDataIngestionService> _mockDataIngestionService;
    private readonly Mock<IDataIngestionOrchestrator> _mockOrchestrator;
    private readonly DataIngestionController _controller;

    /// <summary>
    /// Initializes mock and creates the controller under test.
    /// All service methods default to completing successfully.
    /// </summary>
    public DataIngestionControllerTests()
    {
        _mockDataIngestionService = new Mock<IDataIngestionService>();
        _mockOrchestrator = new Mock<IDataIngestionOrchestrator>();
        var mockLogger = new Mock<ILogger<DataIngestionController>>();

        _controller = new DataIngestionController(
            _mockDataIngestionService.Object,
            _mockOrchestrator.Object,
            mockLogger.Object);

        // Default all service methods to succeed
        _mockOrchestrator
            .Setup(o => o.RunFullIngestionAsync(It.IsAny<int>()))
            .Returns(Task.CompletedTask);
        _mockOrchestrator
            .Setup(o => o.RunScheduledUpdatesAsync())
            .Returns(Task.CompletedTask);
        _mockDataIngestionService
            .Setup(s => s.IngestNBATeamsAsync())
            .Returns(Task.CompletedTask);
        _mockDataIngestionService
            .Setup(s => s.IngestNBAScheduleAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(Task.CompletedTask);
        _mockDataIngestionService
            .Setup(s => s.IngestNBAGameStatisticsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(Task.CompletedTask);
        _mockDataIngestionService
            .Setup(s => s.IngestNBAOddsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(Task.CompletedTask);
        _mockDataIngestionService
            .Setup(s => s.IngestOddsAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _mockDataIngestionService
            .Setup(s => s.UpdateNBAGameResultsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(Task.CompletedTask);
    }

    // ==================== POST api/dataingestion/full/{sportId} ====================

    /// <summary>
    /// Happy path: full ingestion returns 200 OK with a confirmation message.
    /// </summary>
    [Fact]
    public async Task RunFullIngestion_Success_Returns200WithMessage()
    {
        // Act
        var result = await _controller.RunFullIngestion(sportId: 1);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        ok.Value.Should().BeEquivalentTo(new { message = "Full ingestion completed for sport 1" });
    }

    /// <summary>
    /// Full ingestion delegates to the orchestrator with the correct sport ID.
    /// </summary>
    [Theory]
    [InlineData(1)] // NBA
    [InlineData(2)] // NFL
    public async Task RunFullIngestion_CallsOrchestratorWithCorrectSportId(int sportId)
    {
        // Act
        await _controller.RunFullIngestion(sportId);

        // Assert
        _mockOrchestrator.Verify(
            o => o.RunFullIngestionAsync(sportId),
            Times.Once,
            "orchestrator should be called exactly once with the supplied sport ID");
    }

    /// <summary>
    /// When the orchestrator throws, the endpoint returns 500 with the error message.
    /// The exception must not propagate to the caller.
    /// </summary>
    [Fact]
    public async Task RunFullIngestion_OrchestratorThrows_Returns500WithError()
    {
        // Arrange
        _mockOrchestrator
            .Setup(o => o.RunFullIngestionAsync(It.IsAny<int>()))
            .ThrowsAsync(new InvalidOperationException("Orchestrator failure"));

        // Act
        var result = await _controller.RunFullIngestion(sportId: 1);

        // Assert
        var error = result.Should().BeOfType<ObjectResult>().Subject;
        error.StatusCode.Should().Be(500);
        error.Value.Should().BeEquivalentTo(new { error = "Orchestrator failure" });
    }

    // ==================== POST api/dataingestion/nba/teams ====================

    /// <summary>
    /// Happy path: NBA teams ingestion returns 200 OK with a confirmation message.
    /// </summary>
    [Fact]
    public async Task IngestNBATeams_Success_Returns200WithMessage()
    {
        // Act
        var result = await _controller.IngestNBATeams();

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        ok.Value.Should().BeEquivalentTo(new { message = "NBA teams ingestion completed" });
    }

    /// <summary>
    /// NBA teams ingestion delegates to the data ingestion service.
    /// </summary>
    [Fact]
    public async Task IngestNBATeams_CallsService()
    {
        // Act
        await _controller.IngestNBATeams();

        // Assert
        _mockDataIngestionService.Verify(
            s => s.IngestNBATeamsAsync(),
            Times.Once,
            "service should be called exactly once");
    }

    /// <summary>
    /// When the service throws, the endpoint returns 500 with the error message.
    /// </summary>
    [Fact]
    public async Task IngestNBATeams_ServiceThrows_Returns500WithError()
    {
        // Arrange
        _mockDataIngestionService
            .Setup(s => s.IngestNBATeamsAsync())
            .ThrowsAsync(new Exception("Teams service failure"));

        // Act
        var result = await _controller.IngestNBATeams();

        // Assert
        var error = result.Should().BeOfType<ObjectResult>().Subject;
        error.StatusCode.Should().Be(500);
        error.Value.Should().BeEquivalentTo(new { error = "Teams service failure" });
    }

    // ==================== POST api/dataingestion/nba/schedule ====================

    /// <summary>
    /// Happy path: schedule ingestion with explicit dates returns 200 OK.
    /// </summary>
    [Fact]
    public async Task IngestNBASchedule_ExplicitDates_Returns200WithMessage()
    {
        // Arrange
        var start = new DateTime(2025, 1, 1);
        var end = new DateTime(2025, 1, 31);

        // Act
        var result = await _controller.IngestNBASchedule(start, end);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        ok.Value.Should().BeEquivalentTo(
            new { message = "NBA schedule ingestion completed from 2025-01-01 to 2025-01-31" });
    }

    /// <summary>
    /// When no dates are supplied the endpoint defaults to today → today+7 days.
    /// </summary>
    [Fact]
    public async Task IngestNBASchedule_NullDates_UsesDefaultDateRange()
    {
        // Arrange — capture the dates the service actually receives
        DateTime? capturedStart = null;
        DateTime? capturedEnd = null;
        var before = DateTime.UtcNow.Date;

        _mockDataIngestionService
            .Setup(s => s.IngestNBAScheduleAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Callback<DateTime, DateTime>((s, e) => { capturedStart = s; capturedEnd = e; })
            .Returns(Task.CompletedTask);

        // Act
        await _controller.IngestNBASchedule(startDate: null, endDate: null);

        var after = DateTime.UtcNow.Date;

        // Assert — start should be today's date (UTC)
        capturedStart.Should().NotBeNull();
        capturedStart!.Value.Date.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);

        // Assert — end should be start + 7 days
        capturedEnd.Should().NotBeNull();
        (capturedEnd!.Value.Date - capturedStart.Value.Date).Days
            .Should().Be(7, "default end date should be 7 days after start");
    }

    /// <summary>
    /// Schedule ingestion passes the supplied dates through to the service unchanged.
    /// </summary>
    [Fact]
    public async Task IngestNBASchedule_ExplicitDates_PassesCorrectDatesToService()
    {
        // Arrange
        var start = new DateTime(2025, 3, 1);
        var end = new DateTime(2025, 3, 15);

        // Act
        await _controller.IngestNBASchedule(start, end);

        // Assert
        _mockDataIngestionService.Verify(
            s => s.IngestNBAScheduleAsync(start, end),
            Times.Once,
            "service should receive the exact dates provided by the caller");
    }

    /// <summary>
    /// When the service throws, the endpoint returns 500.
    /// </summary>
    [Fact]
    public async Task IngestNBASchedule_ServiceThrows_Returns500WithError()
    {
        // Arrange
        _mockDataIngestionService
            .Setup(s => s.IngestNBAScheduleAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ThrowsAsync(new Exception("Schedule service failure"));

        // Act
        var result = await _controller.IngestNBASchedule();

        // Assert
        var error = result.Should().BeOfType<ObjectResult>().Subject;
        error.StatusCode.Should().Be(500);
        error.Value.Should().BeEquivalentTo(new { error = "Schedule service failure" });
    }

    // ==================== POST api/dataingestion/nba/statistics ====================

    /// <summary>
    /// Happy path: statistics ingestion with explicit dates returns 200 OK.
    /// </summary>
    [Fact]
    public async Task IngestNBAGameStatisticsAsync_ExplicitDates_Returns200WithMessage()
    {
        // Arrange
        var start = new DateTime(2025, 2, 1);
        var end = new DateTime(2025, 2, 28);

        // Act
        var result = await _controller.IngestNBAGameStatisticsAsync(start, end);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        ok.Value.Should().BeEquivalentTo(
            new { message = "Statistics ingestion completed from 2025-02-01 to 2025-02-28" });
    }

    /// <summary>
    /// When no dates are supplied the endpoint defaults to today → today+7 days.
    /// </summary>
    [Fact]
    public async Task IngestNBAGameStatisticsAsync_NullDates_UsesDefaultDateRange()
    {
        // Arrange
        DateTime? capturedStart = null;
        DateTime? capturedEnd = null;
        var before = DateTime.UtcNow.Date;

        _mockDataIngestionService
            .Setup(s => s.IngestNBAGameStatisticsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Callback<DateTime, DateTime>((s, e) => { capturedStart = s; capturedEnd = e; })
            .Returns(Task.CompletedTask);

        // Act
        await _controller.IngestNBAGameStatisticsAsync(startDate: null, endDate: null);

        var after = DateTime.UtcNow.Date;

        // Assert
        capturedStart!.Value.Date.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        (capturedEnd!.Value.Date - capturedStart.Value.Date).Days
            .Should().Be(7, "default end date should be 7 days after start");
    }

    /// <summary>
    /// Statistics ingestion passes the supplied dates through to the service unchanged.
    /// </summary>
    [Fact]
    public async Task IngestNBAGameStatisticsAsync_ExplicitDates_PassesCorrectDatesToService()
    {
        // Arrange
        var start = new DateTime(2025, 4, 1);
        var end = new DateTime(2025, 4, 10);

        // Act
        await _controller.IngestNBAGameStatisticsAsync(start, end);

        // Assert
        _mockDataIngestionService.Verify(
            s => s.IngestNBAGameStatisticsAsync(start, end),
            Times.Once);
    }

    /// <summary>
    /// When the service throws, the endpoint returns 500.
    /// </summary>
    [Fact]
    public async Task IngestNBAGameStatisticsAsync_ServiceThrows_Returns500WithError()
    {
        // Arrange
        _mockDataIngestionService
            .Setup(s => s.IngestNBAGameStatisticsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ThrowsAsync(new Exception("Statistics service failure"));

        // Act
        var result = await _controller.IngestNBAGameStatisticsAsync();

        // Assert
        var error = result.Should().BeOfType<ObjectResult>().Subject;
        error.StatusCode.Should().Be(500);
        error.Value.Should().BeEquivalentTo(new { error = "Statistics service failure" });
    }

    // ==================== POST api/dataingestion/nba/odds ====================

    /// <summary>
    /// Happy path: NBA odds ingestion with explicit dates returns 200 OK.
    /// </summary>
    [Fact]
    public async Task IngestNBAOdds_ExplicitDates_Returns200WithMessage()
    {
        // Arrange
        var start = new DateTime(2025, 1, 5);
        var end = new DateTime(2025, 1, 6);

        // Act
        var result = await _controller.IngestNBAOdds(start, end);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        ok.Value.Should().BeEquivalentTo(
            new { message = "Odds ingestion completed from 2025-01-05 to 2025-01-06" });
    }

    /// <summary>
    /// When no dates are supplied the endpoint defaults to UtcNow-48h → UtcNow+1h.
    /// We verify the window width is approximately 49 hours (allowing a few seconds
    /// of clock drift during the test).
    /// </summary>
    [Fact]
    public async Task IngestNBAOdds_NullDates_UsesDefaultDateRange()
    {
        // Arrange
        DateTime? capturedStart = null;
        DateTime? capturedEnd = null;
        var callTime = DateTime.UtcNow;

        _mockDataIngestionService
            .Setup(s => s.IngestNBAOddsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Callback<DateTime, DateTime>((s, e) => { capturedStart = s; capturedEnd = e; })
            .Returns(Task.CompletedTask);

        // Act
        await _controller.IngestNBAOdds(startDate: null, endDate: null);

        // Assert — start should be ~48 hours before the call
        capturedStart.Should().NotBeNull();
        capturedStart!.Value.Should()
            .BeCloseTo(callTime.AddHours(-48), precision: TimeSpan.FromSeconds(5),
                because: "default start should be 48 hours before now");

        // Assert — end should be ~1 hour after the call
        capturedEnd.Should().NotBeNull();
        capturedEnd!.Value.Should()
            .BeCloseTo(callTime.AddHours(1), precision: TimeSpan.FromSeconds(5),
                because: "default end should be 1 hour after now");
    }

    /// <summary>
    /// NBA odds ingestion passes the supplied dates through to the service unchanged.
    /// </summary>
    [Fact]
    public async Task IngestNBAOdds_ExplicitDates_PassesCorrectDatesToService()
    {
        // Arrange
        var start = new DateTime(2025, 5, 1);
        var end = new DateTime(2025, 5, 2);

        // Act
        await _controller.IngestNBAOdds(start, end);

        // Assert
        _mockDataIngestionService.Verify(
            s => s.IngestNBAOddsAsync(start, end),
            Times.Once);
    }

    /// <summary>
    /// When the service throws, the endpoint returns 500.
    /// </summary>
    [Fact]
    public async Task IngestNBAOdds_ServiceThrows_Returns500WithError()
    {
        // Arrange
        _mockDataIngestionService
            .Setup(s => s.IngestNBAOddsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ThrowsAsync(new Exception("Odds service failure"));

        // Act
        var result = await _controller.IngestNBAOdds();

        // Assert
        var error = result.Should().BeOfType<ObjectResult>().Subject;
        error.StatusCode.Should().Be(500);
        error.Value.Should().BeEquivalentTo(new { error = "Odds service failure" });
    }

    // ==================== POST api/dataingestion/odds/{sport} ====================

    /// <summary>
    /// Happy path: sport odds ingestion returns 200 OK with a confirmation message.
    /// </summary>
    [Fact]
    public async Task IngestOdds_Success_Returns200WithMessage()
    {
        // Act
        var result = await _controller.IngestOdds("basketball_nba");

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        ok.Value.Should().BeEquivalentTo(
            new { message = "Odds ingestion completed for basketball_nba" });
    }

    /// <summary>
    /// Sport odds ingestion delegates to the service with the correct sport key.
    /// </summary>
    [Theory]
    [InlineData("basketball_nba")]
    [InlineData("americanfootball_nfl")]
    public async Task IngestOdds_CallsServiceWithCorrectSport(string sport)
    {
        // Act
        await _controller.IngestOdds(sport);

        // Assert
        _mockDataIngestionService.Verify(
            s => s.IngestOddsAsync(sport),
            Times.Once,
            "service should be called with the exact sport key from the route");
    }

    /// <summary>
    /// When the service throws, the endpoint returns 500.
    /// </summary>
    [Fact]
    public async Task IngestOdds_ServiceThrows_Returns500WithError()
    {
        // Arrange
        _mockDataIngestionService
            .Setup(s => s.IngestOddsAsync(It.IsAny<string>()))
            .ThrowsAsync(new Exception("Odds sport failure"));

        // Act
        var result = await _controller.IngestOdds("basketball_nba");

        // Assert
        var error = result.Should().BeOfType<ObjectResult>().Subject;
        error.StatusCode.Should().Be(500);
        error.Value.Should().BeEquivalentTo(new { error = "Odds sport failure" });
    }

    // ==================== POST api/dataingestion/update ====================

    /// <summary>
    /// Happy path: scheduled updates returns 200 OK with a confirmation message.
    /// </summary>
    [Fact]
    public async Task RunScheduledUpdates_Success_Returns200WithMessage()
    {
        // Act
        var result = await _controller.RunScheduledUpdates();

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        ok.Value.Should().BeEquivalentTo(new { message = "Scheduled updates completed" });
    }

    /// <summary>
    /// Scheduled updates delegates to the orchestrator.
    /// </summary>
    [Fact]
    public async Task RunScheduledUpdates_CallsOrchestrator()
    {
        // Act
        await _controller.RunScheduledUpdates();

        // Assert
        _mockOrchestrator.Verify(
            o => o.RunScheduledUpdatesAsync(),
            Times.Once,
            "orchestrator should be called exactly once");
    }

    /// <summary>
    /// When the orchestrator throws, the endpoint returns 500.
    /// </summary>
    [Fact]
    public async Task RunScheduledUpdates_OrchestratorThrows_Returns500WithError()
    {
        // Arrange
        _mockOrchestrator
            .Setup(o => o.RunScheduledUpdatesAsync())
            .ThrowsAsync(new Exception("Update failure"));

        // Act
        var result = await _controller.RunScheduledUpdates();

        // Assert
        var error = result.Should().BeOfType<ObjectResult>().Subject;
        error.StatusCode.Should().Be(500);
        error.Value.Should().BeEquivalentTo(new { error = "Update failure" });
    }

    // ==================== POST api/dataingestion/nba/results ====================

    /// <summary>
    /// Happy path: game results update with explicit dates returns 200 OK.
    /// </summary>
    [Fact]
    public async Task UpdateNBAGameResults_ExplicitDates_Returns200WithMessage()
    {
        // Arrange
        var start = new DateTime(2025, 1, 10);
        var end = new DateTime(2025, 1, 11);

        // Act
        var result = await _controller.UpdateNBAGameResults(start, end);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        ok.Value.Should().BeEquivalentTo(
            new { message = "Game results updated for sport from 2025-01-10 to 2025-01-11" });
    }

    /// <summary>
    /// When no dates are supplied the endpoint defaults to UtcNow-48h → UtcNow+1h,
    /// matching the same window as NBA odds (recent + near-future games).
    /// </summary>
    [Fact]
    public async Task UpdateNBAGameResults_NullDates_UsesDefaultDateRange()
    {
        // Arrange
        DateTime? capturedStart = null;
        DateTime? capturedEnd = null;
        var callTime = DateTime.UtcNow;

        _mockDataIngestionService
            .Setup(s => s.UpdateNBAGameResultsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Callback<DateTime, DateTime>((s, e) => { capturedStart = s; capturedEnd = e; })
            .Returns(Task.CompletedTask);

        // Act
        await _controller.UpdateNBAGameResults(startDate: null, endDate: null);

        // Assert — start should be ~48 hours before the call
        capturedStart.Should().NotBeNull();
        capturedStart!.Value.Should()
            .BeCloseTo(callTime.AddHours(-48), precision: TimeSpan.FromSeconds(5),
                because: "default start should be 48 hours before now");

        // Assert — end should be ~1 hour after the call
        capturedEnd.Should().NotBeNull();
        capturedEnd!.Value.Should()
            .BeCloseTo(callTime.AddHours(1), precision: TimeSpan.FromSeconds(5),
                because: "default end should be 1 hour after now");
    }

    /// <summary>
    /// Game results update passes the supplied dates through to the service unchanged.
    /// </summary>
    [Fact]
    public async Task UpdateNBAGameResults_ExplicitDates_PassesCorrectDatesToService()
    {
        // Arrange
        var start = new DateTime(2025, 6, 1);
        var end = new DateTime(2025, 6, 2);

        // Act
        await _controller.UpdateNBAGameResults(start, end);

        // Assert
        _mockDataIngestionService.Verify(
            s => s.UpdateNBAGameResultsAsync(start, end),
            Times.Once);
    }

    /// <summary>
    /// When the service throws, the endpoint returns 500.
    /// </summary>
    [Fact]
    public async Task UpdateNBAGameResults_ServiceThrows_Returns500WithError()
    {
        // Arrange
        _mockDataIngestionService
            .Setup(s => s.UpdateNBAGameResultsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ThrowsAsync(new Exception("Results service failure"));

        // Act
        var result = await _controller.UpdateNBAGameResults();

        // Assert
        var error = result.Should().BeOfType<ObjectResult>().Subject;
        error.StatusCode.Should().Be(500);
        error.Value.Should().BeEquivalentTo(new { error = "Results service failure" });
    }

    // ==================== Cross-cutting: no cross-contamination ====================

    /// <summary>
    /// Verifies that the data ingestion service is never called when the orchestrator
    /// endpoints are used, and vice versa. Guards against accidental wiring mistakes.
    /// </summary>
    [Fact]
    public async Task RunFullIngestion_DoesNotCallDataIngestionService()
    {
        // Act
        await _controller.RunFullIngestion(sportId: 1);

        // Assert
        _mockDataIngestionService.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Verifies that the orchestrator is never called when a service-level endpoint
    /// (e.g. teams ingestion) is used.
    /// </summary>
    [Fact]
    public async Task IngestNBATeams_DoesNotCallOrchestrator()
    {
        // Act
        await _controller.IngestNBATeams();

        // Assert
        _mockOrchestrator.VerifyNoOtherCalls();
    }
}