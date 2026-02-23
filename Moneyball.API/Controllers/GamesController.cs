using Microsoft.AspNetCore.Mvc;
using Moneyball.Core.DTOs;
using Moneyball.Core.Entities;
using Moneyball.Core.Interfaces.Repositories;
using Swashbuckle.AspNetCore.Annotations;

namespace Moneyball.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GamesController(IMoneyballRepository moneyballRepository, ILogger<GamesController> logger) : ControllerBase
{
    /// <summary>
    /// Get upcoming games for a sport
    /// </summary>
    /// <param name="sportId">Sport ID (1=NBA, 2=NFL, etc.)</param>
    /// <param name="daysAhead">Number of days ahead to fetch</param>
    [HttpGet("upcoming")]
    [SwaggerResponse(StatusCodes.Status200OK, "List of upcoming games", typeof(IEnumerable<Game>))]
    public async Task<IActionResult> GetUpcomingGames(
        [FromQuery] int? sportId = null,
        [FromQuery] int daysAhead = 7)
    {
        try
        {
            var games = await moneyballRepository.Games.GetUpcomingGamesAsync(sportId, daysAhead);

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
            logger.LogError(ex, "Error fetching upcoming games");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get a specific game with all details
    /// </summary>
    /// <param name="gameId">Game ID</param>
    [HttpGet("{gameId}")]
    [SwaggerResponse(StatusCodes.Status200OK, "Specific game with all details")]
    public async Task<IActionResult> GetGame(int gameId)
    {
        try
        {
            var game = await moneyballRepository.Games.GetGameWithDetailsAsync(gameId);

            if (game == null)
            {
                return NotFound(new { error = "Game not found" });
            }

            var result = new GameResult
            {
                GameId = game.GameId,
                ExternalGameId = game.ExternalGameId,
                Sport = game.Sport.Name,
                HomeTeam = new TeamResult
                {
                    TeamId = game.HomeTeam.TeamId,
                    Name = game.HomeTeam.Name,
                    Abbreviation = game.HomeTeam.Abbreviation,
                    City = game.HomeTeam.City
                },
                AwayTeam = new TeamResult
                {
                    TeamId = game.AwayTeam.TeamId,
                    Name = game.AwayTeam.Name,
                    Abbreviation = game.AwayTeam.Abbreviation,
                    City = game.AwayTeam.City
                },
                GameDate = game.GameDate,
                Status = game.Status,
                Score = new ScoreResult
                {
                    Home = game.HomeScore,
                    Away = game.AwayScore
                },
                Odds = game.Odds.Select(o => new OddsResult
                {
                    BookmakerName = o.BookmakerName,
                    Moneyline = new MoneylineResult
                    {
                        Home = o.HomeMoneyline,
                        Away = o.AwayMoneyline
                    },
                    Spread = new SpreadResult
                    {
                        Home = o.HomeSpread,
                        HomeOdds = o.HomeSpreadOdds,
                        Away = o.AwaySpread,
                        AwayOdds = o.AwaySpreadOdds
                    },
                    Total = new TotalResult
                    {
                        Line = o.OverUnder,
                        Over = o.OverOdds,
                        Under = o.UnderOdds
                    },
                    RecordedAt = o.RecordedAt
                }),
                Statistics = game.TeamStatistics.Select(s => new StatisticResult
                {
                    HomeOrAway = s.IsHomeTeam ? "Home" : "Away",
                    Points = s.Points,
                    FieldGoalsMade = s.FieldGoalsMade,
                    FieldGoalsAttempted = s.FieldGoalsAttempted,
                    FieldGoalPercentage = s.FieldGoalPercentage,
                    ThreePointsMade = s.ThreePointsMade,
                    Assists = s.Assists,
                    Rebounds = s.Rebounds,
                    Turnovers = s.Turnovers
                }),
                Predictions = game.Predictions.Select(p => new PredictionResult
                {
                    Model = p.Model.Name,
                    Version = p.Model.Version,
                    HomeWinProbability = p.PredictedHomeWinProbability,
                    AwayWinProbability = p.PredictedAwayWinProbability,
                    Edge = p.Edge,
                    Confidence = p.Confidence,
                    PredictedAt = p.CreatedAt
                })
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching game {GameId}", gameId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get games by date range
    /// </summary>
    [HttpGet("range")]
    [SwaggerResponse(StatusCodes.Status200OK, "List of games by date range")]
    public async Task<IActionResult> GetGamesByDateRange(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        [FromQuery] int? sportId = null)
    {
        try
        {
            var games = await moneyballRepository.Games.GetGamesByDateRangeAsync(startDate, endDate, sportId);

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
            logger.LogError(ex, "Error fetching games by date range");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get latest odds for upcoming games
    /// </summary>
    [HttpGet("odds/latest")]
    [SwaggerResponse(StatusCodes.Status200OK, "List of latest odds for upcoming games")]
    public async Task<IActionResult> GetLatestOdds([FromQuery] int? sportId = null)
    {
        try
        {
            var upcomingGames = await moneyballRepository.Games.GetUpcomingGamesAsync(sportId, 3);
            var gameIds = upcomingGames.Select(g => g.GameId).ToList();

            var odds = await moneyballRepository.Odds.GetLatestOddsForGamesAsync(gameIds);

            var result = odds.Select(o => new
            {
                o.GameId,
                Game = $"{o.Game.AwayTeam.Name} @ {o.Game.HomeTeam.Name}",
                o.Game.GameDate,
                o.BookmakerName,
                Moneyline = new
                {
                    Home = o.HomeMoneyline,
                    Away = o.AwayMoneyline
                },
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
                    o.OverOdds,
                    o.UnderOdds
                },
                o.RecordedAt
            });

            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching latest odds");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}