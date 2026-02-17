using Microsoft.AspNetCore.Mvc;
using Moneyball.Core.Interfaces.ExternalAPIs;

namespace Moneyball.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DataIngestionController : ControllerBase
{
    private readonly IDataIngestionService _dataIngestionService;
    private readonly IDataIngestionOrchestrator _orchestrator;
    private readonly ILogger<DataIngestionController> _logger;

    public DataIngestionController(
        IDataIngestionService dataIngestionService,
        IDataIngestionOrchestrator orchestrator,
        ILogger<DataIngestionController> logger)
    {
        _dataIngestionService = dataIngestionService;
        _orchestrator = orchestrator;
        _logger = logger;
    }

    /// <summary>
    /// Run full data ingestion for a sport (teams, schedule, odds)
    /// </summary>
    /// <param name="sportId">Sport ID (1=NBA, 2=NFL)</param>
    [HttpPost("full/{sportId}")]
    public async Task<IActionResult> RunFullIngestion(int sportId)
    {
        try
        {
            _logger.LogInformation("Manual full ingestion triggered for sport {SportId}", sportId);
            await _orchestrator.RunFullIngestionAsync(sportId);
            return Ok(new { message = $"Full ingestion completed for sport {sportId}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during full ingestion");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Ingest teams for NBA
    /// </summary>
    [HttpPost("nba/teams")]
    public async Task<IActionResult> IngestNBATeams()
    {
        try
        {
            _logger.LogInformation("Manual NBA teams ingestion triggered");
            await _dataIngestionService.IngestNBATeamsAsync();
            return Ok(new { message = "NBA teams ingestion completed" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ingesting NBA teams");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Ingest NBA schedule for a date range
    /// </summary>
    /// <param name="startDate">Start date (YYYY-MM-DD)</param>
    /// <param name="endDate">End date (YYYY-MM-DD)</param>
    [HttpPost("nba/schedule")]
    public async Task<IActionResult> IngestNBASchedule(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var start = startDate ?? DateTime.UtcNow.Date;
            var end = endDate ?? DateTime.UtcNow.Date.AddDays(7);

            _logger.LogInformation("Manual NBA schedule ingestion triggered from {Start} to {End}", start, end);
            await _dataIngestionService.IngestNBAScheduleAsync(start, end);
            return Ok(new { message = $"NBA schedule ingestion completed from {start:yyyy-MM-dd} to {end:yyyy-MM-dd}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ingesting NBA schedule");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Ingest odds for a sport
    /// </summary>
    /// <param name="sport">Sport key (basketball_nba, americanfootball_nfl)</param>
    [HttpPost("odds/{sport}")]
    public async Task<IActionResult> IngestOdds(string sport)
    {
        try
        {
            _logger.LogInformation("Manual odds ingestion triggered for {Sport}", sport);
            await _dataIngestionService.IngestOddsAsync(sport);
            return Ok(new { message = $"Odds ingestion completed for {sport}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ingesting odds");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Run scheduled updates (schedule, odds, results)
    /// </summary>
    [HttpPost("update")]
    public async Task<IActionResult> RunScheduledUpdates()
    {
        try
        {
            _logger.LogInformation("Manual scheduled updates triggered");
            await _orchestrator.RunScheduledUpdatesAsync();
            return Ok(new { message = "Scheduled updates completed" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during scheduled updates");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Update game results for recent games
    /// </summary>
    /// <param name="sportId">Sport ID</param>
    [HttpPost("results/{sportId}")]
    public async Task<IActionResult> UpdateGameResults(int sportId)
    {
        try
        {
            _logger.LogInformation("Manual game results update triggered for sport {SportId}", sportId);
            await _dataIngestionService.UpdateGameResultsAsync(sportId);
            return Ok(new { message = $"Game results updated for sport {sportId}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating game results");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}