using Microsoft.Extensions.Logging;
using Moneyball.Core.DTOs;
using Moneyball.Core.Entities;
using Moneyball.Core.Enums;
using Moneyball.Core.Interfaces.ExternalAPIs;
using Moneyball.Core.Interfaces.ExternalServices;
using Moneyball.Core.Interfaces.Repositories;

namespace Moneyball.Infrastructure.ExternalServices;

public class DataIngestionService(
    IMoneyballRepository moneyballRepository,
    ISportsDataService sportsDataService,
    IOddsDataService oddsDataService,
    ILogger<DataIngestionService> logger)
    : IDataIngestionService
{
    /// <summary>
    /// Ingests NBA teams from SportRadar API.
    /// Upserts teams: creates new teams on first run, updates existing teams if data changes.
    /// </summary>
    public async Task IngestNBATeamsAsync()
    {
        logger.LogInformation("Starting NBA teams ingestion");

        try
        {
            // Step 1: Get NBA sport entity
            var nbaSport = await moneyballRepository.Sports.FirstOrDefaultAsync(s => s.Name == "NBA");

            if (nbaSport == null)
            {
                logger.LogError("NBA sport not found in database. Ensure Sports seed data exists.");
                throw new InvalidOperationException("NBA sport not found in database. Run database migrations or execute DatabaseSetup.sql to seed Sports table.");
            }

            // Step 2: Fetch teams from SportRadar API
            logger.LogInformation("Fetching NBA teams from SportRadar API");
            var apiTeams = (await sportsDataService.GetNBATeamsAsync()).ToList();

            if (!apiTeams.Any())
            {
                logger.LogWarning("No teams returned from SportRadar API. Check API key and network connectivity.");
                return;
            }

            logger.LogInformation("Received {Count} teams from SportRadar API", apiTeams.Count);

            // Step 3: Process each team (upsert logic)
            var teamsAdded = 0;
            var teamsUpdated = 0;
            var teamsUnchanged = 0;

            foreach (var apiTeam in apiTeams)
            {
                // Validate API data
                if (string.IsNullOrWhiteSpace(apiTeam.Id))
                {
                    logger.LogWarning("Skipping team with missing ID: {Name}", apiTeam.Name);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(apiTeam.Name))
                {
                    logger.LogWarning("Skipping team with missing Name. ID: {Id}", apiTeam.Id);
                    continue;
                }

                // Check if team already exists
                var existingTeam = await moneyballRepository.Teams.GetByExternalIdAsync(apiTeam.Id, nbaSport.SportId);

                if (existingTeam == null)
                {
                    // CREATE: Team doesn't exist, create new
                    var newTeam = new Team
                    {
                        SportId = nbaSport.SportId,
                        ExternalId = apiTeam.Id,
                        Name = apiTeam.Name,
                        Abbreviation = apiTeam.Alias,
                        City = apiTeam.Market
                    };

                    await moneyballRepository.Teams.AddAsync(newTeam);
                    teamsAdded++;

                    logger.LogDebug("Creating new team: {Name} ({Alias}) - {City}",
                        apiTeam.Name, apiTeam.Alias, apiTeam.Market);
                }
                else
                {
                    // UPDATE: Team exists, check if any fields changed
                    var hasChanges = false;

                    if (existingTeam.Name != apiTeam.Name)
                    {
                        logger.LogInformation("Team name changed: '{OldName}' → '{NewName}' (ID: {ExternalId})",
                            existingTeam.Name, apiTeam.Name, apiTeam.Id);
                        existingTeam.Name = apiTeam.Name;
                        hasChanges = true;
                    }

                    if (existingTeam.Abbreviation != apiTeam.Alias)
                    {
                        logger.LogInformation("Team abbreviation changed: '{OldAbbr}' → '{NewAbbr}' for {Name}",
                            existingTeam.Abbreviation, apiTeam.Alias, existingTeam.Name);
                        existingTeam.Abbreviation = apiTeam.Alias;
                        hasChanges = true;
                    }

                    if (existingTeam.City != apiTeam.Market)
                    {
                        logger.LogInformation("Team city changed: '{OldCity}' → '{NewCity}' for {Name}",
                            existingTeam.City, apiTeam.Market, existingTeam.Name);
                        existingTeam.City = apiTeam.Market;
                        hasChanges = true;
                    }

                    if (hasChanges)
                    {
                        await moneyballRepository.Teams.UpdateAsync(existingTeam);
                        teamsUpdated++;
                    }
                    else
                    {
                        teamsUnchanged++;
                    }
                }
            }

            // Step 4: Save all changes in a single transaction
            var changeCount = await moneyballRepository.SaveChangesAsync();

            // Step 5: Log final counts (acceptance criteria: count logged)
            logger.LogInformation(
                "NBA teams ingestion complete. Added: {Added}, Updated: {Updated}, Unchanged: {Unchanged}, Changed: {Changed}, Total processed: {Total}",
                teamsAdded, teamsUpdated, teamsUnchanged, changeCount, apiTeams.Count);

            // Validate acceptance criteria: 30 teams expected
            var totalTeamsInDb = await moneyballRepository.Teams.CountAsync(t => t.SportId == nbaSport.SportId);

            if (totalTeamsInDb != 30)
            {
                logger.LogWarning(
                    "Expected 30 NBA teams in database but found {Count}. Verify SportRadar API response.",
                    totalTeamsInDb);
            }
            else
            {
                logger.LogInformation("Verified: 30 NBA teams present in database");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during NBA teams ingestion");
            throw;
        }
    }

    public async Task IngestNBAScheduleAsync(DateTime startDate, DateTime endDate)
    {
        logger.LogInformation("Starting NBA schedule ingestion from {StartDate} to {EndDate}",
            startDate, endDate);

        try
        {
            var games = await sportsDataService.GetNBAScheduleAsync(startDate, endDate);
            var nbaSport = await moneyballRepository.Sports.FirstOrDefaultAsync(s => s.Name == "NBA");

            if (nbaSport == null)
            {
                logger.LogError("NBA sport not found in database");
                return;
            }

            var gamesAdded = 0;
            var gamesUpdated = 0;

            foreach (var apiGame in games)
            {
                var existingGame = await moneyballRepository.Games.GetGameByExternalIdAsync(
                    apiGame.Id, nbaSport.SportId);

                var homeTeam = await moneyballRepository.Teams.GetByExternalIdAsync(
                    apiGame.Home.Id, nbaSport.SportId);
                var awayTeam = await moneyballRepository.Teams.GetByExternalIdAsync(
                    apiGame.Away.Id, nbaSport.SportId);

                if (homeTeam == null || awayTeam == null)
                {
                    logger.LogWarning("Teams not found for game {GameId}. Home: {HomeId}, Away: {AwayId}",
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

                    await moneyballRepository.Games.AddAsync(newGame);
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
                        await moneyballRepository.Games.UpdateAsync(existingGame);
                        gamesUpdated++;
                    }
                }
            }

            await moneyballRepository.SaveChangesAsync();

            logger.LogInformation("NBA schedule ingestion complete. Added: {Added}, Updated: {Updated}",
                gamesAdded, gamesUpdated);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during NBA schedule ingestion");
            throw;
        }
    }

    public async Task IngestNBAGameStatisticsAsync(string externalGameId)
    {
        logger.LogInformation("Ingesting statistics for game {GameId}", externalGameId);

        try
        {
            var nbaSport = await moneyballRepository.Sports.FirstOrDefaultAsync(s => s.Name == "NBA");
            if (nbaSport == null) return;

            var game = await moneyballRepository.Games.GetGameByExternalIdAsync(externalGameId, nbaSport.SportId);
            if (game == null)
            {
                logger.LogWarning("Game {GameId} not found in database", externalGameId);
                return;
            }

            var statistics = await sportsDataService.GetNBAGameStatisticsAsync(externalGameId);
            if (statistics == null) return;

            // Process home team statistics
            await UpsertTeamStatistics(game.GameId, game.HomeTeamId, true, statistics.Home.Statistics);

            // Process away team statistics
            await UpsertTeamStatistics(game.GameId, game.AwayTeamId, false, statistics.Away.Statistics);

            await moneyballRepository.SaveChangesAsync();

            logger.LogInformation("Statistics ingested for game {GameId}", externalGameId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error ingesting statistics for game {GameId}", externalGameId);
            throw;
        }
    }

    public async Task IngestOddsAsync(string sport)
    {
        logger.LogInformation("Starting odds ingestion for {Sport}", sport);

        try
        {
            // Map sport key to our sport name
            var sportName = sport switch
            {
                "basketball_nba" => "NBA",
                "americanfootball_nfl" => "NFL",
                _ => throw new ArgumentException($"Unsupported sport: {sport}")
            };

            var sportEntity = await moneyballRepository.Sports.FirstOrDefaultAsync(s => s.Name == sportName);
            if (sportEntity == null)
            {
                logger.LogError("Sport {SportName} not found", sportName);
                return;
            }

            var oddsResponse = await oddsDataService.GetOddsAsync(sport);
            var oddsAdded = 0;

            foreach (var oddsGame in oddsResponse.Data)
            {
                // Find matching game by date and teams
                var gameDate = oddsGame.CommenceTime;
                var games = await moneyballRepository.Games.GetGamesByDateRangeAsync(
                    gameDate.AddHours(-2), gameDate.AddHours(2), sportEntity.SportId);

                var matchingGame = games.FirstOrDefault(g =>
                    (g.HomeTeam.Name.Contains(oddsGame.HomeTeam) ||
                     g.HomeTeam.City?.Contains(oddsGame.HomeTeam) == true) &&
                    (g.AwayTeam.Name.Contains(oddsGame.AwayTeam) ||
                     g.AwayTeam.City?.Contains(oddsGame.AwayTeam) == true));

                if (matchingGame == null)
                {
                    logger.LogDebug("No matching game found for odds: {Home} vs {Away} at {Date}",
                        oddsGame.HomeTeam, oddsGame.AwayTeam, oddsGame.CommenceTime);
                    continue;
                }

                // Process each bookmaker
                foreach (var bookmaker in oddsGame.Bookmakers)
                {
                    var gameOdds = new Odds
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

                    await moneyballRepository.Odds.AddAsync(gameOdds);
                    oddsAdded++;
                }
            }

            await moneyballRepository.SaveChangesAsync();

            logger.LogInformation("Odds ingestion complete. Added {Count} odds records", oddsAdded);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during odds ingestion for {Sport}", sport);
            throw;
        }
    }

    public async Task UpdateGameResultsAsync(int sportId)
    {
        logger.LogInformation("Updating game results for sport {SportId}", sportId);

        try
        {
            // Get recent games that might have finished
            var recentGames = await moneyballRepository.Games.GetGamesByDateRangeAsync(
                DateTime.UtcNow.AddDays(-2), DateTime.UtcNow, sportId);

            var incompleteGames = recentGames.Where(g => !g.IsComplete).ToList();

            logger.LogInformation("Found {Count} incomplete games to check", incompleteGames.Count);

            // This would call the sports data service to get updated scores
            // Implementation depends on the specific sport API

            await moneyballRepository.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating game results");
            throw;
        }
    }

    private async Task UpsertTeamStatistics(int gameId, int teamId, bool isHomeTeam,
        NBAStatistics stats)
    {
        var existing = await moneyballRepository.TeamStatistics.FirstOrDefaultAsync(
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

            await moneyballRepository.TeamStatistics.AddAsync(teamStats);
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

            await moneyballRepository.TeamStatistics.UpdateAsync(existing);
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
