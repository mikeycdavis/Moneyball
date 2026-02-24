using Microsoft.Extensions.Logging;
using Moneyball.Core.DTOs.ExternalAPIs.SportsRadar;
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
            var nbaSport = await moneyballRepository.Sports.FirstOrDefaultAsync(s => s.Name == SportType.NBA);

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

    /// <summary>
    /// Ingests NBA schedule from SportRadar API for a given date range.
    /// Upserts games: creates new games, updates existing games with scores and status changes.
    /// </summary>
    /// <param name="startDate">Start date for schedule fetch</param>
    /// <param name="endDate">End date for schedule fetch</param>
    public async Task IngestNBAScheduleAsync(DateTime startDate, DateTime endDate)
    {
        logger.LogInformation("Starting NBA schedule ingestion from {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}",
            startDate, endDate);

        try
        {
            // Step 1: Get NBA sport entity
            var nbaSport = await moneyballRepository.Sports.FirstOrDefaultAsync(s => s.Name == SportType.NBA);

            if (nbaSport == null)
            {
                logger.LogError("NBA sport not found in database. Ensure Sports seed data exists.");
                throw new InvalidOperationException("NBA sport not found in database. Run database migrations or execute DatabaseSetup.sql to seed Sports table.");
            }

            // Step 2: Fetch schedule from SportRadar API
            logger.LogInformation("Fetching NBA schedule from SportRadar API");
            var apiGames = (await sportsDataService.GetNBAScheduleAsync(startDate, endDate)).ToList();

            if (!apiGames.Any())
            {
                logger.LogInformation("No games returned from SportRadar API for date range {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}",
                    startDate, endDate);
                return;
            }

            logger.LogInformation("Received {Count} games from SportRadar API", apiGames.Count);

            // Step 3: Process each game (upsert logic)
            var gamesAdded = 0;
            var gamesUpdated = 0;
            var gamesUnchanged = 0;

            foreach (var apiGame in apiGames)
            {
                // Validate API data
                if (string.IsNullOrWhiteSpace(apiGame.Id))
                {
                    logger.LogWarning("Skipping game with missing ID");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(apiGame.Home.Id) || string.IsNullOrWhiteSpace(apiGame.Away.Id))
                {
                    logger.LogWarning("Skipping game {GameId} with missing team IDs", apiGame.Id);
                    continue;
                }

                // Look up teams by external ID
                var homeTeam = await moneyballRepository.Teams.GetByExternalIdAsync(apiGame.Home.Id, nbaSport.SportId);
                var awayTeam = await moneyballRepository.Teams.GetByExternalIdAsync(apiGame.Away.Id, nbaSport.SportId);

                if (homeTeam == null || awayTeam == null)
                {
                    logger.LogWarning("Teams not found for game {GameId}. Home: {HomeId}, Away: {AwayId}. Run team ingestion first.",
                        apiGame.Id, apiGame.Home.Id, apiGame.Away.Id);
                    continue;
                }

                // Check if game already exists
                var existingGame = await moneyballRepository.Games.GetGameByExternalIdAsync(apiGame.Id, nbaSport.SportId);

                if (existingGame == null)
                {
                    // CREATE: Game doesn't exist, create new
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
                        IsComplete = IsGameComplete(apiGame.Status),
                        CreatedAt = DateTime.UtcNow
                    };

                    await moneyballRepository.Games.AddAsync(newGame);
                    gamesAdded++;

                    logger.LogDebug("Creating new game: {Away} @ {Home} on {Date:yyyy-MM-dd} (Status: {Status})",
                        awayTeam.Abbreviation, homeTeam.Abbreviation, apiGame.Scheduled, apiGame.Status);
                }
                else
                {
                    // UPDATE: Game exists, check if any fields changed
                    var hasChanges = false;

                    // Map the new status
                    var newStatus = MapGameStatus(apiGame.Status);
                    var newIsComplete = IsGameComplete(apiGame.Status);

                    // Check status change
                    if (existingGame.Status != newStatus)
                    {
                        logger.LogInformation("Game {ExternalId} status changed: {OldStatus} → {NewStatus}",
                            apiGame.Id, existingGame.Status, newStatus);
                        existingGame.Status = newStatus;
                        hasChanges = true;
                    }

                    // Check IsComplete flag (acceptance criteria: flipped when status is closed)
                    if (existingGame.IsComplete != newIsComplete)
                    {
                        logger.LogInformation("Game {ExternalId} completion status changed: IsComplete={IsComplete}",
                            apiGame.Id, newIsComplete);
                        existingGame.IsComplete = newIsComplete;
                        hasChanges = true;
                    }

                    // Check home score change
                    if (apiGame.HomePoints.HasValue && existingGame.HomeScore != apiGame.HomePoints)
                    {
                        logger.LogDebug("Game {ExternalId} home score updated: {OldScore} → {NewScore}",
                            apiGame.Id, existingGame.HomeScore, apiGame.HomePoints);
                        existingGame.HomeScore = apiGame.HomePoints;
                        hasChanges = true;
                    }

                    // Check away score change
                    if (apiGame.AwayPoints.HasValue && existingGame.AwayScore != apiGame.AwayPoints)
                    {
                        logger.LogDebug("Game {ExternalId} away score updated: {OldScore} → {NewScore}",
                            apiGame.Id, existingGame.AwayScore, apiGame.AwayPoints);
                        existingGame.AwayScore = apiGame.AwayPoints;
                        hasChanges = true;
                    }

                    if (hasChanges)
                    {
                        existingGame.UpdatedAt = DateTime.UtcNow;
                        await moneyballRepository.Games.UpdateAsync(existingGame);
                        gamesUpdated++;
                    }
                    else
                    {
                        gamesUnchanged++;
                    }
                }
            }

            // Step 4: Save all changes in a single transaction
            var changeCount = await moneyballRepository.SaveChangesAsync();

            // Step 5: Log final counts (acceptance criteria: counts logged)
            logger.LogInformation(
                "NBA schedule ingestion complete. Added: {Added}, Updated: {Updated}, Unchanged: {Unchanged}, Changed: {Changed}, Total processed: {Total}",
                gamesAdded, gamesUpdated, gamesUnchanged, changeCount, apiGames.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during NBA schedule ingestion");
            throw;
        }
    }

    /// <summary>
    /// Ingests detailed box-score statistics for a given date range.
    /// Maps all NBAStatistics fields to TeamStatistic columns and upserts for both home and away teams.
    /// </summary>
    /// <param name="startDate">Start date for schedule fetch</param>
    /// <param name="endDate">End date for schedule fetch</param>
    public async Task IngestNBAGameStatisticsAsync(DateTime startDate, DateTime endDate)
    {
        logger.LogInformation("Starting NBA game statistics ingestion from {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}",
            startDate, endDate);

        try
        {
            // Step 1: Get NBA sport entity
            var nbaSport = await moneyballRepository.Sports.FirstOrDefaultAsync(s => s.Name == SportType.NBA);

            if (nbaSport == null)
            {
                logger.LogError("NBA sport not found in database. Ensure Sports seed data exists.");
                throw new InvalidOperationException("NBA sport not found in database. Run database migrations or execute DatabaseSetup.sql to seed Sports table.");
            }

            // Step 2: Fetch schedule from SportRadar API
            logger.LogInformation("Fetching NBA schedule from SportRadar API");
            var apiGames = (await sportsDataService.GetNBAScheduleAsync(startDate, endDate)).ToList();

            if (!apiGames.Any())
            {
                logger.LogInformation("No games returned from SportRadar API for date range {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}",
                    startDate, endDate);
                return;
            }

            logger.LogInformation("Received {Count} games from SportRadar API", apiGames.Count);

            foreach (var apiGame in apiGames)
            {
                // Step 3: Validate API data
                if (string.IsNullOrWhiteSpace(apiGame.Id))
                {
                    logger.LogWarning("Skipping game with missing ID");
                    continue;
                }

                // Step 4: Check if game already exists
                var existingGame = await moneyballRepository.Games.GetGameByExternalIdAsync(apiGame.Id, nbaSport.SportId);

                if (existingGame == null)
                    continue;

                // Step 5: Fetch statistics from SportRadar API
                logger.LogInformation("Fetching statistics for game {GameId} from SportRadar API", apiGame.Id);
                var statistics = await sportsDataService.GetNBAGameStatisticsAsync(apiGame.Id);

                if (statistics == null)
                {
                    logger.LogWarning("No statistics returned from SportRadar API for game {GameId}. Game may not have started yet.", apiGame.Id);
                    continue;
                }

                // Step 5: Upsert home team statistics
                await UpsertTeamStatisticsAsync(
                    existingGame.GameId,
                    existingGame.HomeTeamId,
                    true, // isHomeTeam
                    statistics.Home.Statistics,
                    "Home");

                // Step 6: Upsert away team statistics
                await UpsertTeamStatisticsAsync(
                    existingGame.GameId,
                    existingGame.AwayTeamId,
                    false, // isHomeTeam
                    statistics.Away.Statistics,
                    "Away");

                // Step 7: Save all changes in a single transaction
                var changeCount = await moneyballRepository.SaveChangesAsync();

                logger.LogInformation(
                    "NBA game statistics ingestion complete for game {GameId}. Changes saved: {ChangeCount}",
                    apiGame.Id, changeCount);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during NBA game statistics ingestion");
            throw;
        }
    }

    /// <summary>
    /// Ingests betting odds from SportRadar Odds Comparison API for a given date range.
    /// Creates one Odds row per bookmaker.
    /// </summary>
    public async Task IngestNBAOddsAsync(DateTime startDate, DateTime endDate)
    {
        logger.LogInformation("Starting SportRadar odds ingestion from {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}",
            startDate, endDate);

        try
        {
            // Step 1: Get NBA sport entity
            var nbaSport = await moneyballRepository.Sports.FirstOrDefaultAsync(s => s.Name == SportType.NBA);

            if (nbaSport == null)
            {
                logger.LogError("NBA sport not found in database. Ensure Sports seed data exists.");
                throw new InvalidOperationException("NBA sport not found in database. Run database migrations or execute DatabaseSetup.sql to seed Sports table.");
            }

            // Step 2: Fetch schedule from SportRadar API
            logger.LogInformation("Fetching NBA schedule from SportRadar API");
            var apiGames = (await sportsDataService.GetNBAScheduleAsync(startDate, endDate)).ToList();

            if (!apiGames.Any())
            {
                logger.LogInformation("No games returned from SportRadar API for date range {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}",
                    startDate, endDate);
                return;
            }

            logger.LogInformation("Received {Count} games from SportRadar API", apiGames.Count);

            foreach (var apiGame in apiGames)
            {
                // Step 3: Validate API data
                if (string.IsNullOrWhiteSpace(apiGame.Id))
                {
                    logger.LogWarning("Skipping game with missing ID");
                    continue;
                }

                // Step 4: Check if game already exists
                var existingGame = await moneyballRepository.Games.GetGameByExternalIdAsync(apiGame.Id, nbaSport.SportId);

                if (existingGame == null)
                    continue;

                // Step 5: Fetch odds from SportRadar API
                logger.LogInformation("Fetching odds for game {GameId} from SportRadar API", apiGame.Id);
                var oddsResponse = await sportsDataService.GetNBAOddsAsync(apiGame.Id);

                if (oddsResponse == null || !oddsResponse.Markets.Any())
                {
                    logger.LogInformation("No odds data returned for game {GameId}. Odds may not be available yet.", apiGame.Id);
                    continue;
                }

                // Step 6: Process odds for each bookmaker
                // Group markets by bookmaker to create one Odds row per bookmaker
                var bookmakerOddsMap = new Dictionary<string, Odds>();

                foreach (var market in oddsResponse.Markets)
                {
                    foreach (var bookmaker in market.Bookmakers)
                    {
                        // Get or create Odds entry for this bookmaker
                        if (!bookmakerOddsMap.ContainsKey(bookmaker.Name))
                        {
                            bookmakerOddsMap[bookmaker.Name] = new Odds
                            {
                                GameId = existingGame.GameId,
                                BookmakerName = bookmaker.Name,
                                RecordedAt = DateTime.UtcNow
                            };
                        }

                        var odds = bookmakerOddsMap[bookmaker.Name];

                        // Map odds based on market type
                        switch (market.Name.ToLower())
                        {
                            case "1x2": // Moneyline market
                            case "moneyline":
                                MapMoneylineOdds(bookmaker, odds);
                                break;

                            case "pointspread":
                            case "spread":
                            case "handicap":
                                MapSpreadOdds(bookmaker, odds);
                                break;

                            case "totals":
                            case "total":
                            case "over/under":
                                MapTotalOdds(bookmaker, odds);
                                break;

                            default:
                                logger.LogDebug("Skipping unknown market type: {MarketName}", market.Name);
                                break;
                        }
                    }
                }

                // Step 6: Save all odds in a single transaction
                var oddsAdded = 0;
                foreach (var odds in bookmakerOddsMap.Values)
                {
                    await moneyballRepository.Odds.AddAsync(odds);
                    oddsAdded++;

                    logger.LogDebug(
                        "Added odds from {Bookmaker} for game {GameId}: ML(H:{HomeML}/A:{AwayML}), Spread(H:{HomeSpread}@{HomeSpreadOdds}/A:{AwaySpread}@{AwaySpreadOdds}), Total:{OverUnder}(O:{OverOdds}/U:{UnderOdds})",
                        odds.BookmakerName, existingGame.GameId,
                        odds.HomeMoneyline, odds.AwayMoneyline,
                        odds.HomeSpread, odds.HomeSpreadOdds,
                        odds.AwaySpread, odds.AwaySpreadOdds,
                        odds.OverUnder, odds.OverOdds, odds.UnderOdds);
                }

                var changeCount = await moneyballRepository.SaveChangesAsync();

                logger.LogInformation(
                    "SportRadar odds ingestion complete for game {GameId}. Added: {Added} bookmaker odds. Changes saved: {ChangeCount}",
                    apiGame.Id, oddsAdded, changeCount);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during SportRadar odds ingestion");
            throw;
        }
    }

    /// <summary>
    /// Ingests betting odds from The Odds API for a specific sport.
    /// </summary>
    /// <param name="sport"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public async Task IngestOddsAsync(string sport)
    {
        logger.LogInformation("Starting odds ingestion for {Sport}", sport);

        try
        {
            // Map sport key to our sport name
            var sportName = sport switch
            {
                "basketball_nba" => SportType.NBA,
                "americanfootball_nfl" => SportType.NFL,
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
                    var odds = new Odds
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
                                if (homeML != null) odds.HomeMoneyline = homeML.Price;
                                if (awayML != null) odds.AwayMoneyline = awayML.Price;
                                break;

                            case "spreads":
                                var homeSpread = market.Outcomes.FirstOrDefault(o => o.Name == oddsGame.HomeTeam);
                                var awaySpread = market.Outcomes.FirstOrDefault(o => o.Name == oddsGame.AwayTeam);
                                if (homeSpread != null)
                                {
                                    odds.HomeSpread = homeSpread.Point;
                                    odds.HomeSpreadOdds = homeSpread.Price;
                                }
                                if (awaySpread != null)
                                {
                                    odds.AwaySpread = awaySpread.Point;
                                    odds.AwaySpreadOdds = awaySpread.Price;
                                }
                                break;

                            case "totals":
                                var over = market.Outcomes.FirstOrDefault(o => o.Name == "Over");
                                var under = market.Outcomes.FirstOrDefault(o => o.Name == "Under");
                                if (over != null)
                                {
                                    odds.OverUnder = over.Point;
                                    odds.OverOdds = over.Price;
                                }
                                if (under != null)
                                {
                                    odds.UnderOdds = under.Price;
                                }
                                break;
                        }
                    }

                    await moneyballRepository.Odds.AddAsync(odds);
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

    /// <summary>
    /// Updates NBA game results for completed games for a given date range.
    /// Fetches final scores from SportRadar and updates IsComplete flag and scores.
    /// Acceptance criteria: Games within last 48 hours checked; IsComplete flipped; 
    /// HomeScore/AwayScore populated; count logged.
    /// </summary>
    public async Task UpdateNBAGameResultsAsync(DateTime startDate, DateTime endDate)
    {
        logger.LogInformation("Starting NBA game result updates from {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}",
            startDate, endDate);

        try
        {
            // Step 1: Get NBA sport entity
            var nbaSport = await moneyballRepository.Sports.FirstOrDefaultAsync(s => s.Name == SportType.NBA);

            if (nbaSport == null)
            {
                logger.LogError("NBA sport not found in database");
                throw new InvalidOperationException("NBA sport not found in database. Ensure Sports seed data exists.");
            }

            // Step 2: Fetch schedule from SportRadar API
            logger.LogInformation("Fetching NBA schedule from SportRadar API");
            var apiGames = (await sportsDataService.GetNBAScheduleAsync(
                startDate, endDate)).ToList();

            if (!apiGames.Any())
            {
                logger.LogInformation("No games returned from SportRadar API for date range {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}",
                    startDate, endDate);
                return;
            }

            logger.LogInformation("Received {Count} games from SportRadar API", apiGames.Count);

            var gamesUpdated = 0;
            var gamesCompleted = 0;
            var gamesSkipped = 0;

            // Step 3: Process each game
            foreach (var apiGame in apiGames)
            {
                try
                {
                    // Step 4: Validate API data
                    if (string.IsNullOrWhiteSpace(apiGame.Id))
                    {
                        logger.LogWarning("Skipping game with missing ID");
                        continue;
                    }

                    // Step 5: Check if game already exists
                    var existingGame = await moneyballRepository.Games.GetGameByExternalIdAsync(apiGame.Id, nbaSport.SportId);

                    // Skip games that don't exist
                    if (existingGame == null)
                    {
                        gamesSkipped++;
                        continue;
                    }

                    // Skip games that are already marked as complete
                    // (This is an optimization - we could still update them, but typically final scores don't change)
                    if (existingGame.IsComplete)
                    {
                        gamesSkipped++;
                        continue;
                    }

                    // Step 4: Check if game is complete and update scores
                    var hasChanges = false;

                    // Update home score (acceptance criteria: HomeScore populated)
                    var homePoints = apiGame.HomePoints;
                    if (existingGame.HomeScore != homePoints)
                    {
                        logger.LogDebug(
                            "Updating home score for existingGame {ExternalGameId}: {OldScore} → {NewScore}",
                            existingGame.ExternalGameId, existingGame.HomeScore, homePoints);
                        existingGame.HomeScore = homePoints;
                        hasChanges = true;
                    }

                    // Update away score (acceptance criteria: AwayScore populated)
                    var awayPoints = apiGame.AwayPoints;
                    if (existingGame.AwayScore != awayPoints)
                    {
                        logger.LogDebug(
                            "Updating away score for existingGame {ExternalGameId}: {OldScore} → {NewScore}",
                            existingGame.ExternalGameId, existingGame.AwayScore, awayPoints);
                        existingGame.AwayScore = awayPoints;
                        hasChanges = true;
                    }

                    // Determine if game is complete based on status
                    // SportRadar statuses: "scheduled", "inprogress", "halftime", "closed", "complete"
                    var isGameComplete = IsGameComplete(apiGame.Status);

                    // Flip IsComplete flag (acceptance criteria: IsComplete flipped)
                    if (!existingGame.IsComplete && isGameComplete)
                    {
                        logger.LogInformation(
                            "Marking game {ExternalGameId} as complete. Final score: {Home} {HomeScore} - {Away} {AwayScore}",
                            existingGame.ExternalGameId,
                            apiGame.Home.Name,
                            existingGame.HomeScore,
                            apiGame.Away.Name,
                            existingGame.AwayScore);

                        existingGame.IsComplete = true;
                        existingGame.Status = GameStatus.Final;
                        gamesCompleted++;
                        hasChanges = true;
                    }
                    else if (existingGame.Status != MapGameStatus(apiGame.Status))
                    {
                        // Update status even if not complete (e.g., scheduled → in progress)
                        existingGame.Status = MapGameStatus(apiGame.Status);
                        hasChanges = true;
                    }

                    // Save changes if any updates were made
                    if (hasChanges)
                    {
                        existingGame.UpdatedAt = DateTime.UtcNow;
                        await moneyballRepository.Games.UpdateAsync(existingGame);
                        gamesUpdated++;
                    }
                    else
                    {
                        gamesSkipped++;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex,
                        "Error updating results for game {ExternalGameId}. Continuing with next game.",
                        apiGame.Id);
                    gamesSkipped++;
                }
            }

            // Step 5: Save all changes in a single transaction
            var changeCount = await moneyballRepository.SaveChangesAsync();

            // Step 6: Log final counts (acceptance criteria: count logged)
            logger.LogInformation(
                "Game results update complete. Updated: {Updated}, Completed: {Completed}, Changed: {Changed}, Skipped: {Skipped}, Total processed: {Total}",
                gamesUpdated, gamesCompleted, changeCount, gamesSkipped, apiGames.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during game results update");
            throw;
        }
    }

    /// <summary>
    /// Upserts team statistics for a specific game and team.
    /// If statistics already exist, they are updated (replaced) with the latest data.
    /// </summary>
    private async Task UpsertTeamStatisticsAsync(
        int gameId,
        int teamId,
        bool isHomeTeam,
        NBAStatistics stats,
        string teamLabel)
    {
        logger.LogDebug("Upserting {TeamLabel} team statistics for GameId={GameId}, TeamId={TeamId}",
            teamLabel, gameId, teamId);

        // Check if statistics already exist (acceptance criteria: upsert replaces stale rows)
        var existing = await moneyballRepository.TeamStatistics.FirstOrDefaultAsync(
            ts => ts.GameId == gameId && ts.TeamId == teamId);

        if (existing == null)
        {
            // CREATE: Statistics don't exist, create new
            var teamStats = new TeamStatistic
            {
                GameId = gameId,
                TeamId = teamId,
                IsHomeTeam = isHomeTeam,

                // Map all NBAStatistics fields (acceptance criteria: all fields mapped)
                Points = stats.Points,

                // Field goals
                FieldGoalsMade = stats.FieldGoalsMade,
                FieldGoalsAttempted = stats.FieldGoalsAttempted,
                FieldGoalPercentage = stats.FieldGoalPercentage,

                // Three-pointers
                ThreePointsMade = stats.ThreePointsMade,
                ThreePointsAttempted = stats.ThreePointsAttempted,
                ThreePointPercentage = stats.ThreePointPercentage,

                // Free throws
                FreeThrowsMade = stats.FreeThrowsMade,
                FreeThrowsAttempted = stats.FreeThrowsAttempted,
                FreeThrowPercentage = stats.FreeThrowPercentage,

                // Rebounds
                Rebounds = stats.Rebounds,
                OffensiveRebounds = stats.OffensiveRebounds,
                DefensiveRebounds = stats.DefensiveRebounds,

                // Other stats
                Assists = stats.Assists,
                Steals = stats.Steals,
                Blocks = stats.Blocks,
                Turnovers = stats.Turnovers,
                PersonalFouls = stats.PersonalFouls,

                CreatedAt = DateTime.UtcNow
            };

            await moneyballRepository.TeamStatistics.AddAsync(teamStats);
            logger.LogDebug("Created new statistics for {TeamLabel} team (GameId={GameId})", teamLabel, gameId);
        }
        else
        {
            // UPDATE: Statistics exist, replace with latest data (acceptance criteria: replaces stale rows)
            logger.LogDebug("Updating existing statistics for {TeamLabel} team (GameId={GameId})", teamLabel, gameId);

            existing.Points = stats.Points;

            // Field goals
            existing.FieldGoalsMade = stats.FieldGoalsMade;
            existing.FieldGoalsAttempted = stats.FieldGoalsAttempted;
            existing.FieldGoalPercentage = stats.FieldGoalPercentage;

            // Three-pointers
            existing.ThreePointsMade = stats.ThreePointsMade;
            existing.ThreePointsAttempted = stats.ThreePointsAttempted;
            existing.ThreePointPercentage = stats.ThreePointPercentage;

            // Free throws
            existing.FreeThrowsMade = stats.FreeThrowsMade;
            existing.FreeThrowsAttempted = stats.FreeThrowsAttempted;
            existing.FreeThrowPercentage = stats.FreeThrowPercentage;

            // Rebounds
            existing.Rebounds = stats.Rebounds;
            existing.OffensiveRebounds = stats.OffensiveRebounds;
            existing.DefensiveRebounds = stats.DefensiveRebounds;

            // Other stats
            existing.Assists = stats.Assists;
            existing.Steals = stats.Steals;
            existing.Blocks = stats.Blocks;
            existing.Turnovers = stats.Turnovers;
            existing.PersonalFouls = stats.PersonalFouls;

            await moneyballRepository.TeamStatistics.UpdateAsync(existing);
        }
    }

    /// <summary>
    /// Maps SportRadar API status strings to GameStatus enum.
    /// </summary>
    private static GameStatus MapGameStatus(string apiStatus)
    {
        return apiStatus.ToLower() switch
        {
            "scheduled" => GameStatus.Scheduled,
            "created" => GameStatus.Scheduled,
            "inprogress" => GameStatus.InProgress,
            "halftime" => GameStatus.InProgress,
            "closed" => GameStatus.Final,
            "complete" => GameStatus.Final,
            "final" => GameStatus.Final,
            "postponed" => GameStatus.Postponed,
            "delayed" => GameStatus.Postponed,
            "cancelled" => GameStatus.Cancelled,
            "canceled" => GameStatus.Cancelled,
            "suspended" => GameStatus.Postponed,
            _ => GameStatus.Unknown // Default to unknown for unknown statuses
        };
    }

    /// <summary>
    /// Determines if a game is complete based on its status.
    /// </summary>
    private static bool IsGameComplete(string apiStatus)
    {
        var status = apiStatus.ToLower();
        return status == "closed" || status == "complete" || status == "final";
    }

    /// <summary>
    /// Maps moneyline odds from SportRadar format to Odds.
    /// SportRadar uses "1" for home and "2" for away in outcome types.
    /// </summary>
    private static void MapMoneylineOdds(NBABookmaker bookmaker, Odds odds)
    {
        // Type "1" = Home, Type "2" = Away
        var homeOutcome = bookmaker.Outcomes.FirstOrDefault(o => o.Type == "1");
        var awayOutcome = bookmaker.Outcomes.FirstOrDefault(o => o.Type == "2");

        if (homeOutcome != null)
        {
            // SportRadar odds are already in American format (e.g., -110, +150)
            odds.HomeMoneyline = homeOutcome.Odds;
        }

        if (awayOutcome != null)
        {
            odds.AwayMoneyline = awayOutcome.Odds;
        }
    }

    /// <summary>
    /// Maps spread odds from SportRadar format to Odds.
    /// Extracts spread line and odds for both home and away.
    /// </summary>
    private static void MapSpreadOdds(NBABookmaker bookmaker, Odds odds)
    {
        // Type "1" = Home, Type "2" = Away
        var homeOutcome = bookmaker.Outcomes.FirstOrDefault(o => o.Type == "1");
        var awayOutcome = bookmaker.Outcomes.FirstOrDefault(o => o.Type == "2");

        if (homeOutcome != null)
        {
            // Line is the spread (e.g., -3.5)
            odds.HomeSpread = homeOutcome.Line;
            // Odds are the price on that spread (e.g., -110)
            odds.HomeSpreadOdds = homeOutcome.Odds;
        }

        if (awayOutcome != null)
        {
            odds.AwaySpread = awayOutcome.Line;
            odds.AwaySpreadOdds = awayOutcome.Odds;
        }
    }

    /// <summary>
    /// Maps total (over/under) odds from SportRadar format to Odds.
    /// Extracts the total line and odds for both over and under.
    /// </summary>
    private static void MapTotalOdds(NBABookmaker bookmaker, Odds odds)
    {
        // Type "over" and "under"
        var overOutcome = bookmaker.Outcomes.FirstOrDefault(o =>
            o.Type.Equals("over", StringComparison.OrdinalIgnoreCase));
        var underOutcome = bookmaker.Outcomes.FirstOrDefault(o =>
            o.Type.Equals("under", StringComparison.OrdinalIgnoreCase));

        if (overOutcome != null)
        {
            // Line is the total (e.g., 220.5)
            odds.OverUnder = overOutcome.Line;
            // Odds are the price on the over (e.g., -110)
            odds.OverOdds = overOutcome.Odds;
        }

        if (underOutcome != null)
        {
            // Under odds (e.g., -110)
            odds.UnderOdds = underOutcome.Odds;
        }
    }
}
