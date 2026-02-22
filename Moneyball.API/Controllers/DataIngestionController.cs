using Microsoft.AspNetCore.Mvc;
using Moneyball.Core.Interfaces.ExternalServices;

namespace Moneyball.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DataIngestionController(
    IDataIngestionService dataIngestionService,
    IDataIngestionOrchestrator orchestrator,
    ILogger<DataIngestionController> logger) : ControllerBase
{
    /// <summary>
    /// Run full data ingestion for a sport (teams, schedule, odds)
    /// </summary>
    /// <param name="sportId">Sport ID (1=NBA, 2=NFL)</param>
    [HttpPost("full/{sportId}")]
    public async Task<IActionResult> RunFullIngestion(int sportId)
    {
        try
        {
            logger.LogInformation("Manual full ingestion triggered for sport {SportId}", sportId);
            await orchestrator.RunFullIngestionAsync(sportId);
            return Ok(new { message = $"Full ingestion completed for sport {sportId}" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during full ingestion");
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
            logger.LogInformation("Manual NBA teams ingestion triggered");
            await dataIngestionService.IngestNBATeamsAsync();
            return Ok(new { message = "NBA teams ingestion completed" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error ingesting NBA teams");
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

            logger.LogInformation("Manual NBA schedule ingestion triggered from {Start} to {End}", start, end);
            await dataIngestionService.IngestNBAScheduleAsync(start, end);
            return Ok(new { message = $"NBA schedule ingestion completed from {start:yyyy-MM-dd} to {end:yyyy-MM-dd}" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error ingesting NBA schedule");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Ingest NBA statistics for a date range
    /// </summary>
    /// <param name="startDate">Start date (YYYY-MM-DD)</param>
    /// <param name="endDate">End date (YYYY-MM-DD)</param>
    [HttpPost("nba/statistics")]
    public async Task<IActionResult> IngestNBAGameStatisticsAsync(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var start = startDate ?? DateTime.UtcNow.Date;
            var end = endDate ?? DateTime.UtcNow.Date.AddDays(7);

            logger.LogInformation("Manual statistics ingestion triggered from {Start} to {End}", start, end);
            await dataIngestionService.IngestNBAGameStatisticsAsync(start, end);
            return Ok(new { message = $"Statistics ingestion completed from {start:yyyy-MM-dd} to {end:yyyy-MM-dd}" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error ingesting statistics");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Ingest NBA odds for a date range
    /// </summary>
    /// <param name="startDate">Start date (YYYY-MM-DD)</param>
    /// <param name="endDate">End date (YYYY-MM-DD)</param>
    [HttpPost("nba/odds")]
    public async Task<IActionResult> IngestNBAOdds(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var start = startDate ?? DateTime.UtcNow.AddHours(-48);
            var end = endDate ?? DateTime.UtcNow.AddHours(1);

            logger.LogInformation("Manual odds ingestion triggered from {Start} to {End}", start, end);
            await dataIngestionService.IngestNBAOddsAsync(start, end);
            return Ok(new { message = $"Odds ingestion completed from {start:yyyy-MM-dd} to {end:yyyy-MM-dd}" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error ingesting odds");
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
            logger.LogInformation("Manual odds ingestion triggered for {Sport}", sport);
            await dataIngestionService.IngestOddsAsync(sport);
            return Ok(new { message = $"Odds ingestion completed for {sport}" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error ingesting odds");
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
            logger.LogInformation("Manual scheduled updates triggered");
            await orchestrator.RunScheduledUpdatesAsync();
            return Ok(new { message = "Scheduled updates completed" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during scheduled updates");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Update game results for a date range
    /// </summary>
    /// <param name="startDate">Start date (YYYY-MM-DD)</param>
    /// <param name="endDate">End date (YYYY-MM-DD)</param>
    [HttpPost("nba/results")]
    public async Task<IActionResult> UpdateNBAGameResults(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var start = startDate ?? DateTime.UtcNow.AddHours(-48);
            var end = endDate ?? DateTime.UtcNow.AddHours(1);

            logger.LogInformation("Manual game results update triggered from {Start} to {End}", start, end);
            await dataIngestionService.UpdateNBAGameResultsAsync(start, end);
            return Ok(new { message = $"Game results updated for sport from {start:yyyy-MM-dd} to {end:yyyy-MM-dd}" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating game results");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}