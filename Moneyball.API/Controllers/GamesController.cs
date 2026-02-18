using Microsoft.AspNetCore.Mvc;
using Moneyball.Infrastructure.Repositories;

namespace Moneyball.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GamesController : ControllerBase
{
    private readonly IMoneyballRepository _moneyballRepository;
    private readonly ILogger<GamesController> _logger;

    public GamesController(IMoneyballRepository moneyballRepository, ILogger<GamesController> logger)
    {
        _moneyballRepository = moneyballRepository;
        _logger = logger;
    }

    /// <summary>
    /// Get upcoming games for a sport
    /// </summary>
    /// <param name="sportId">Sport ID (1=NBA, 2=NFL, etc.)</param>
    /// <param name="daysAhead">Number of days ahead to fetch</param>
    [HttpGet("upcoming")]
    public async Task<IActionResult> GetUpcomingGames(
        [FromQuery] int? sportId = null,
        [FromQuery] int daysAhead = 7)
    {
        try
        {
            var games = await _moneyballRepository.Games.GetUpcomingGamesAsync(sportId, daysAhead);

            var result = games.Select(g => new
            {
                g.GameId,
                g.ExternalGameId,
                Sport = g.Sport.Name,
                HomeTeam = g.HomeTeam.Name,
                AwayTeam = g.AwayTeam.Name,
                g.GameDate,
                g.Status,
                g.HomeScore,
                g.AwayScore
            });

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching upcoming games");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get a specific game with all details
    /// </summary>
    /// <param name="gameId">Game ID</param>
    [HttpGet("{gameId}")]
    public async Task<IActionResult> GetGame(int gameId)
    {
        try
        {
            var game = await _moneyballRepository.Games.GetGameWithDetailsAsync(gameId);

            if (game == null)
            {
                return NotFound(new { error = "Game not found" });
            }

            var result = new
            {
                game.GameId,
                game.ExternalGameId,
                Sport = game.Sport.Name,
                HomeTeam = new
                {
                    game.HomeTeam.TeamId,
                    game.HomeTeam.Name,
                    game.HomeTeam.Abbreviation,
                    game.HomeTeam.City
                },
                AwayTeam = new
                {
                    game.AwayTeam.TeamId,
                    game.AwayTeam.Name,
                    game.AwayTeam.Abbreviation,
                    game.AwayTeam.City
                },
                game.GameDate,
                game.Status,
                Score = new
                {
                    Home = game.HomeScore,
                    Away = game.AwayScore
                },
                Odds = game.Odds.Select(o => new
                {
                    o.BookmakerName,
                    Moneyline = new { Home = o.HomeMoneyline, Away = o.AwayMoneyline },
                    Spread = new
                    {
                        Home = o.HomeSpread,
                        HomeOdds = o.HomeSpreadOdds,
                        Away = o.AwaySpread,
                        AwayOdds = o.AwaySpreadOdds
                    },
                    Total = new
                    {
                        Line = o.OverUnder,
                        OverOdds = o.OverOdds,
                        UnderOdds = o.UnderOdds
                    },
                    o.RecordedAt
                }),
                Statistics = game.TeamStatistics.Select(s => new
                {
                    Team = s.IsHomeTeam ? "Home" : "Away",
                    s.Points,
                    s.FieldGoalsMade,
                    s.FieldGoalsAttempted,
                    s.FieldGoalPercentage,
                    s.ThreePointsMade,
                    s.Assists,
                    s.Rebounds,
                    s.Turnovers
                }),
                Predictions = game.Predictions.Select(p => new
                {
                    Model = p.Model.Name,
                    Version = p.Model.Version,
                    p.PredictedHomeWinProbability,
                    p.PredictedAwayWinProbability,
                    p.Edge,
                    p.Confidence,
                    p.CreatedAt
                })
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching game {GameId}", gameId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get games by date range
    /// </summary>
    [HttpGet("range")]
    public async Task<IActionResult> GetGamesByDateRange(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        [FromQuery] int? sportId = null)
    {
        try
        {
            var games = await _moneyballRepository.Games.GetGamesByDateRangeAsync(startDate, endDate, sportId);

            var result = games.Select(g => new
            {
                g.GameId,
                Sport = g.Sport.Name,
                HomeTeam = g.HomeTeam.Name,
                AwayTeam = g.AwayTeam.Name,
                g.GameDate,
                g.Status,
                Score = new { Home = g.HomeScore, Away = g.AwayScore }
            });

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching games by date range");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get latest odds for upcoming games
    /// </summary>
    [HttpGet("odds/latest")]
    public async Task<IActionResult> GetLatestOdds([FromQuery] int? sportId = null)
    {
        try
        {
            var upcomingGames = await _moneyballRepository.Games.GetUpcomingGamesAsync(sportId, 3);
            var gameIds = upcomingGames.Select(g => g.GameId).ToList();

            var odds = await _moneyballRepository.GameOdds.GetLatestOddsForGamesAsync(gameIds);

            var result = odds.Select(o => new
            {
                GameId = o.GameId,
                Game = $"{o.Game.AwayTeam.Name} @ {o.Game.HomeTeam.Name}",
                o.Game.GameDate,
                o.BookmakerName,
                Moneyline = new { Home = o.HomeMoneyline, Away = o.AwayMoneyline },
                Spread = new
                {
                    Home = o.HomeSpread,
                    HomeOdds = o.HomeSpreadOdds,
                    Away = o.AwaySpread,
                    AwayOdds = o.AwaySpreadOdds
                },
                Total = new
                {
                    Line = o.OverUnder,
                    OverOdds = o.OverOdds,
                    UnderOdds = o.UnderOdds
                },
                o.RecordedAt
            });

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching latest odds");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}