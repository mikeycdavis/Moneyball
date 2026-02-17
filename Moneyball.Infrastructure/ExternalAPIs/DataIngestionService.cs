using Microsoft.Extensions.Logging;
using Moneyball.Core.Entities;
using Moneyball.Core.Enums;
using Moneyball.Core.Interfaces.ExternalAPIs;
using Moneyball.Infrastructure.Repositories;
using Moneyball.Service.ExternalAPIs.DTO;

namespace Moneyball.Infrastructure.ExternalAPIs;

public class DataIngestionService : IDataIngestionService
{
    private readonly IMoneyballRepository _moneyballRepository;
    private readonly ISportsDataService _sportsDataService;
    private readonly IOddsDataService _oddsDataService;
    private readonly ILogger<DataIngestionService> _logger;

    public DataIngestionService(
        IMoneyballRepository moneyballRepository,
        ISportsDataService sportsDataService,
        IOddsDataService oddsDataService,
        ILogger<DataIngestionService> logger)
    {
        _moneyballRepository = moneyballRepository;
        _sportsDataService = sportsDataService;
        _oddsDataService = oddsDataService;
        _logger = logger;
    }

    public async Task IngestNBATeamsAsync()
    {
        _logger.LogInformation("Starting NBA teams ingestion");

        try
        {
            var nbaTeams = await _sportsDataService.GetNBATeamsAsync();
            var nbaSport = await _moneyballRepository.Sports.FirstOrDefaultAsync(s => s.Name == "NBA");

            if (nbaSport == null)
            {
                _logger.LogError("NBA sport not found in database");
                return;
            }

            var teamsAdded = 0;
            var teamsUpdated = 0;

            foreach (var apiTeam in nbaTeams)
            {
                var existingTeam = await _moneyballRepository.Teams.GetByExternalIdAsync(apiTeam.Id, nbaSport.SportId);

                if (existingTeam == null)
                {
                    var newTeam = new Team
                    {
                        SportId = nbaSport.SportId,
                        ExternalId = apiTeam.Id,
                        Name = apiTeam.Name,
                        Abbreviation = apiTeam.Alias,
                        City = apiTeam.Market
                    };

                    await _moneyballRepository.Teams.AddAsync(newTeam);
                    teamsAdded++;
                }
                else
                {
                    // Update team info if changed
                    var updated = false;

                    if (existingTeam.Name != apiTeam.Name)
                    {
                        existingTeam.Name = apiTeam.Name;
                        updated = true;
                    }

                    if (existingTeam.Abbreviation != apiTeam.Alias)
                    {
                        existingTeam.Abbreviation = apiTeam.Alias;
                        updated = true;
                    }

                    if (existingTeam.City != apiTeam.Market)
                    {
                        existingTeam.City = apiTeam.Market;
                        updated = true;
                    }

                    if (updated)
                    {
                        await _moneyballRepository.Teams.UpdateAsync(existingTeam);
                        teamsUpdated++;
                    }
                }
            }

            await _moneyballRepository.SaveChangesAsync();

            _logger.LogInformation("NBA teams ingestion complete. Added: {Added}, Updated: {Updated}",
                teamsAdded, teamsUpdated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during NBA teams ingestion");
            throw;
        }
    }

    public async Task IngestNBAScheduleAsync(DateTime startDate, DateTime endDate)
    {
        _logger.LogInformation("Starting NBA schedule ingestion from {StartDate} to {EndDate}",
            startDate, endDate);

        try
        {
            var games = await _sportsDataService.GetNBAScheduleAsync(startDate, endDate);
            var nbaSport = await _moneyballRepository.Sports.FirstOrDefaultAsync(s => s.Name == "NBA");

            if (nbaSport == null)
            {
                _logger.LogError("NBA sport not found in database");
                return;
            }

            var gamesAdded = 0;
            var gamesUpdated = 0;

            foreach (var apiGame in games)
            {
                var existingGame = await _moneyballRepository.Games.GetGameByExternalIdAsync(
                    apiGame.Id, nbaSport.SportId);

                var homeTeam = await _moneyballRepository.Teams.GetByExternalIdAsync(
                    apiGame.Home.Id, nbaSport.SportId);
                var awayTeam = await _moneyballRepository.Teams.GetByExternalIdAsync(
                    apiGame.Away.Id, nbaSport.SportId);

                if (homeTeam == null || awayTeam == null)
                {
                    _logger.LogWarning("Teams not found for game {GameId}. Home: {HomeId}, Away: {AwayId}",
                        apiGame.Id, apiGame.Home.Id, apiGame.Away.Id);
                    continue;
                }

                if (existingGame == null)
                {
                    var newGame = new Game
                    {
                        SportId = nbaSport.SportId,
                        ExternalGameId = apiGame.Id,
                        HomeTeamId = homeTeam.TeamId,
                        AwayTeamId = awayTeam.TeamId,
                        GameDate = apiGame.Scheduled,
                        Status = MapGameStatus(apiGame.Status),
                        HomeScore = apiGame.HomePoints,
                        AwayScore = apiGame.AwayPoints,
                        IsComplete = apiGame.Status == "closed"
                    };

                    await _moneyballRepository.Games.AddAsync(newGame);
                    gamesAdded++;
                }
                else
                {
                    // Update game if there are changes
                    var updated = false;

                    if (existingGame.Status != MapGameStatus(apiGame.Status))
                    {
                        existingGame.Status = MapGameStatus(apiGame.Status);
                        updated = true;
                    }

                    if (apiGame.HomePoints.HasValue && existingGame.HomeScore != apiGame.HomePoints)
                    {
                        existingGame.HomeScore = apiGame.HomePoints;
                        updated = true;
                    }

                    if (apiGame.AwayPoints.HasValue && existingGame.AwayScore != apiGame.AwayPoints)
                    {
                        existingGame.AwayScore = apiGame.AwayPoints;
                        updated = true;
                    }

                    if (apiGame.Status == "closed" && !existingGame.IsComplete)
                    {
                        existingGame.IsComplete = true;
                        updated = true;
                    }

                    if (updated)
                    {
                        existingGame.UpdatedAt = DateTime.UtcNow;
                        await _moneyballRepository.Games.UpdateAsync(existingGame);
                        gamesUpdated++;
                    }
                }
            }

            await _moneyballRepository.SaveChangesAsync();

            _logger.LogInformation("NBA schedule ingestion complete. Added: {Added}, Updated: {Updated}",
                gamesAdded, gamesUpdated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during NBA schedule ingestion");
            throw;
        }
    }

    public async Task IngestNBAGameStatisticsAsync(string externalGameId)
    {
        _logger.LogInformation("Ingesting statistics for game {GameId}", externalGameId);

        try
        {
            var nbaSport = await _moneyballRepository.Sports.FirstOrDefaultAsync(s => s.Name == "NBA");
            if (nbaSport == null) return;

            var game = await _moneyballRepository.Games.GetGameByExternalIdAsync(externalGameId, nbaSport.SportId);
            if (game == null)
            {
                _logger.LogWarning("Game {GameId} not found in database", externalGameId);
                return;
            }

            var statistics = await _sportsDataService.GetNBAGameStatisticsAsync(externalGameId);
            if (statistics == null) return;

            // Process home team statistics
            await UpsertTeamStatistics(game.GameId, game.HomeTeamId, true, statistics.Home.Statistics);

            // Process away team statistics
            await UpsertTeamStatistics(game.GameId, game.AwayTeamId, false, statistics.Away.Statistics);

            await _moneyballRepository.SaveChangesAsync();

            _logger.LogInformation("Statistics ingested for game {GameId}", externalGameId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ingesting statistics for game {GameId}", externalGameId);
            throw;
        }
    }

    public async Task IngestOddsAsync(string sport)
    {
        _logger.LogInformation("Starting odds ingestion for {Sport}", sport);

        try
        {
            // Map sport key to our sport name
            var sportName = sport switch
            {
                "basketball_nba" => "NBA",
                "americanfootball_nfl" => "NFL",
                _ => throw new ArgumentException($"Unsupported sport: {sport}")
            };

            var sportEntity = await _moneyballRepository.Sports.FirstOrDefaultAsync(s => s.Name == sportName);
            if (sportEntity == null)
            {
                _logger.LogError("Sport {SportName} not found", sportName);
                return;
            }

            var oddsResponse = await _oddsDataService.GetOddsAsync(sport);
            var oddsAdded = 0;

            foreach (var oddsGame in oddsResponse.Data)
            {
                // Find matching game by date and teams
                var gameDate = oddsGame.CommenceTime;
                var games = await _moneyballRepository.Games.GetGamesByDateRangeAsync(
                    gameDate.AddHours(-2), gameDate.AddHours(2), sportEntity.SportId);

                var matchingGame = games.FirstOrDefault(g =>
                    (g.HomeTeam.Name.Contains(oddsGame.HomeTeam) ||
                     g.HomeTeam.City?.Contains(oddsGame.HomeTeam) == true) &&
                    (g.AwayTeam.Name.Contains(oddsGame.AwayTeam) ||
                     g.AwayTeam.City?.Contains(oddsGame.AwayTeam) == true));

                if (matchingGame == null)
                {
                    _logger.LogDebug("No matching game found for odds: {Home} vs {Away} at {Date}",
                        oddsGame.HomeTeam, oddsGame.AwayTeam, oddsGame.CommenceTime);
                    continue;
                }

                // Process each bookmaker
                foreach (var bookmaker in oddsGame.Bookmakers)
                {
                    var gameOdds = new GameOdds
                    {
                        GameId = matchingGame.GameId,
                        BookmakerName = bookmaker.Title,
                        RecordedAt = DateTime.UtcNow
                    };

                    // Extract odds from markets
                    foreach (var market in bookmaker.Markets)
                    {
                        switch (market.Key)
                        {
                            case "h2h": // Moneyline
                                var homeML = market.Outcomes.FirstOrDefault(o => o.Name == oddsGame.HomeTeam);
                                var awayML = market.Outcomes.FirstOrDefault(o => o.Name == oddsGame.AwayTeam);
                                if (homeML != null) gameOdds.HomeMoneyline = homeML.Price;
                                if (awayML != null) gameOdds.AwayMoneyline = awayML.Price;
                                break;

                            case "spreads":
                                var homeSpread = market.Outcomes.FirstOrDefault(o => o.Name == oddsGame.HomeTeam);
                                var awaySpread = market.Outcomes.FirstOrDefault(o => o.Name == oddsGame.AwayTeam);
                                if (homeSpread != null)
                                {
                                    gameOdds.HomeSpread = homeSpread.Point;
                                    gameOdds.HomeSpreadOdds = homeSpread.Price;
                                }
                                if (awaySpread != null)
                                {
                                    gameOdds.AwaySpread = awaySpread.Point;
                                    gameOdds.AwaySpreadOdds = awaySpread.Price;
                                }
                                break;

                            case "totals":
                                var over = market.Outcomes.FirstOrDefault(o => o.Name == "Over");
                                var under = market.Outcomes.FirstOrDefault(o => o.Name == "Under");
                                if (over != null)
                                {
                                    gameOdds.OverUnder = over.Point;
                                    gameOdds.OverOdds = over.Price;
                                }
                                if (under != null)
                                {
                                    gameOdds.UnderOdds = under.Price;
                                }
                                break;
                        }
                    }

                    await _moneyballRepository.GameOdds.AddAsync(gameOdds);
                    oddsAdded++;
                }
            }

            await _moneyballRepository.SaveChangesAsync();

            _logger.LogInformation("Odds ingestion complete. Added {Count} odds records", oddsAdded);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during odds ingestion for {Sport}", sport);
            throw;
        }
    }

    public async Task UpdateGameResultsAsync(int sportId)
    {
        _logger.LogInformation("Updating game results for sport {SportId}", sportId);

        try
        {
            // Get recent games that might have finished
            var recentGames = await _moneyballRepository.Games.GetGamesByDateRangeAsync(
                DateTime.UtcNow.AddDays(-2), DateTime.UtcNow, sportId);

            var incompleteGames = recentGames.Where(g => !g.IsComplete).ToList();

            _logger.LogInformation("Found {Count} incomplete games to check", incompleteGames.Count);

            // This would call the sports data service to get updated scores
            // Implementation depends on the specific sport API

            await _moneyballRepository.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating game results");
            throw;
        }
    }

    private async Task UpsertTeamStatistics(int gameId, int teamId, bool isHomeTeam,
        NBAStatistics stats)
    {
        var existing = await _moneyballRepository.TeamStatistics.FirstOrDefaultAsync(
            ts => ts.GameId == gameId && ts.TeamId == teamId);

        if (existing == null)
        {
            var teamStats = new TeamStatistic
            {
                GameId = gameId,
                TeamId = teamId,
                IsHomeTeam = isHomeTeam,
                Points = stats.Points,
                FieldGoalsMade = stats.FieldGoalsMade,
                FieldGoalsAttempted = stats.FieldGoalsAttempted,
                FieldGoalPercentage = stats.FieldGoalPercentage,
                ThreePointsMade = stats.ThreePointsMade,
                ThreePointsAttempted = stats.ThreePointsAttempted,
                ThreePointPercentage = stats.ThreePointPercentage,
                FreeThrowsMade = stats.FreeThrowsMade,
                FreeThrowsAttempted = stats.FreeThrowsAttempted,
                FreeThrowPercentage = stats.FreeThrowPercentage,
                Rebounds = stats.Rebounds,
                OffensiveRebounds = stats.OffensiveRebounds,
                DefensiveRebounds = stats.DefensiveRebounds,
                Assists = stats.Assists,
                Steals = stats.Steals,
                Blocks = stats.Blocks,
                Turnovers = stats.Turnovers,
                PersonalFouls = stats.PersonalFouls
            };

            await _moneyballRepository.TeamStatistics.AddAsync(teamStats);
        }
        else
        {
            // Update existing statistics
            existing.Points = stats.Points;
            existing.FieldGoalsMade = stats.FieldGoalsMade;
            existing.FieldGoalsAttempted = stats.FieldGoalsAttempted;
            existing.FieldGoalPercentage = stats.FieldGoalPercentage;
            existing.ThreePointsMade = stats.ThreePointsMade;
            existing.ThreePointsAttempted = stats.ThreePointsAttempted;
            existing.ThreePointPercentage = stats.ThreePointPercentage;
            existing.FreeThrowsMade = stats.FreeThrowsMade;
            existing.FreeThrowsAttempted = stats.FreeThrowsAttempted;
            existing.FreeThrowPercentage = stats.FreeThrowPercentage;
            existing.Rebounds = stats.Rebounds;
            existing.OffensiveRebounds = stats.OffensiveRebounds;
            existing.DefensiveRebounds = stats.DefensiveRebounds;
            existing.Assists = stats.Assists;
            existing.Steals = stats.Steals;
            existing.Blocks = stats.Blocks;
            existing.Turnovers = stats.Turnovers;
            existing.PersonalFouls = stats.PersonalFouls;

            await _moneyballRepository.TeamStatistics.UpdateAsync(existing);
        }
    }

    private GameStatus MapGameStatus(string apiStatus)
    {
        return apiStatus.ToLower() switch
        {
            "scheduled" => GameStatus.Scheduled,
            "inprogress" => GameStatus.InProgress,
            "closed" => GameStatus.Final,
            "complete" => GameStatus.Final,
            "postponed" => GameStatus.Postponed,
            "cancelled" => GameStatus.Cancelled,
            "canceled" => GameStatus.Cancelled,
            _ => GameStatus.Scheduled
        };
    }
}
