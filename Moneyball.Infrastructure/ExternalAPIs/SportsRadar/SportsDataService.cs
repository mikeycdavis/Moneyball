using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moneyball.Core.DTOs.ExternalAPIs.SportsRadar;
using Moneyball.Core.Interfaces.ExternalAPIs;
using System.Text.Json;

namespace Moneyball.Infrastructure.ExternalAPIs.SportsRadar;

public class SportsDataService : ISportsDataService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SportsDataService> _logger;
    private readonly string _apiKey;
    private readonly string _baseUrl;

    public SportsDataService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<SportsDataService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;

        // Read from configuration
        _apiKey = _configuration["SportsData:ApiKey"] ?? throw new InvalidOperationException("SportsData:ApiKey not configured");
        _baseUrl = _configuration["SportsData:BaseUrl"] ?? "https://api.sportradar.com/nba/trial/v8/en";
    }

    /// <summary>
    /// Fetches NBA game schedule from SportRadar API for a given date range.
    /// Iterates through each day in the range and fetches the daily schedule.
    /// </summary>
    /// <param name="startDate">Start date of the schedule range (inclusive)</param>
    /// <param name="endDate">End date of the schedule range (inclusive)</param>
    /// <returns>Collection of NBA games within the date range</returns>
    /// <remarks>
    /// SportRadar endpoint format: /games/{year}/{month}/{day}/schedule.json
    /// Includes 1-second delay between requests to respect API rate limits (1 req/sec on trial tier).
    /// Returns empty collection if no games found rather than throwing exceptions.
    /// </remarks>
    public async Task<IEnumerable<NBAGame>> GetNBAScheduleAsync(DateTime startDate, DateTime endDate)
    {
        try
        {
            // SportRadar endpoint format: /games/{year}/{month}/{day}/schedule.json
            var games = new List<NBAGame>();

            // Iterate through each day in the date range
            // Date.Date ensures we're working with date-only values (time set to 00:00:00)
            for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
            {
                // Construct URL with formatted date components (YYYY/MM/DD)
                var url = $"{_baseUrl}/games/{date:yyyy}/{date:MM}/{date:dd}/schedule.json?api_key={_apiKey}";

                _logger.LogInformation("Fetching NBA schedule for {Date}", date);

                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();

                    // Validate response content is not empty
                    if (string.IsNullOrWhiteSpace(jsonResponse))
                    {
                        _logger.LogWarning("Failed to fetch schedule for {Date}. API returned empty content.", date);
                    }
                    else
                    {
                        // Deserialize the daily schedule response
                        var scheduleResponse = JsonSerializer.Deserialize<NBAScheduleResponse>(jsonResponse);

                        // Add games from this day to the overall collection
                        if (scheduleResponse?.Games != null)
                        {
                            games.AddRange(scheduleResponse.Games);
                        }
                    }
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // 404 is expected when no games are scheduled for a particular day
                    // Log as info rather than warning since this is normal (e.g., off-season days)
                    _logger.LogInformation("No games found for {Date}", date);
                }
                else
                {
                    // Log other non-success status codes as warnings but continue processing
                    _logger.LogWarning("Failed to fetch schedule for {Date}. Status: {Status}",
                        date, response.StatusCode);
                }

                // Rate limiting: SportRadar trial tier allows 1 request per second
                // Add 1-second delay between daily schedule requests to respect this limit
                await Task.Delay(1000);
            }

            return games;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching NBA schedule from {StartDate} to {EndDate}",
                startDate, endDate);
            throw;
        }
    }

    /// <summary>
    /// Fetches detailed box-score statistics for a specific NBA game from SportRadar API.
    /// Returns team-level statistics including shooting percentages, rebounds, assists, etc.
    /// </summary>
    /// <param name="gameId">SportRadar game ID (SRID format: sr:match:xxxxx or game-specific ID)</param>
    /// <returns>Game statistics object if available, null if game hasn't started or stats unavailable</returns>
    /// <remarks>
    /// SportRadar endpoint format: /games/{gameId}/statistics.json
    /// Statistics are typically available during and after games complete.
    /// Returns null rather than throwing when stats are unavailable (e.g., scheduled games).
    /// </remarks>
    public async Task<NBAGameStatistics?> GetNBAGameStatisticsAsync(string gameId)
    {
        try
        {
            // Construct URL with game ID
            var url = $"{_baseUrl}/games/{gameId}/statistics.json?api_key={_apiKey}";

            _logger.LogInformation("Fetching NBA game statistics for game {GameId}", gameId);

            var response = await _httpClient.GetAsync(url);

            // Return null for non-success responses (stats may not be available yet)
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch game statistics for {GameId}. Status: {Status}",
                    gameId, response.StatusCode);
                return null;
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();

            // Validate response content exists
            if (string.IsNullOrWhiteSpace(jsonResponse))
            {
                _logger.LogWarning("Failed to fetch game statistics for {GameId}. API returned empty content.", gameId);
                return null;
            }

            // Deserialize and return game statistics
            var gameStatistics = JsonSerializer.Deserialize<NBAGameStatistics>(jsonResponse);
            return gameStatistics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching NBA game statistics for {GameId}", gameId);
            throw;
        }
    }

    /// <summary>
    /// Fetches the complete list of NBA teams from SportRadar's league hierarchy endpoint.
    /// Parses the hierarchical structure (conferences → divisions → teams) to extract all teams.
    /// </summary>
    /// <returns>Collection of all NBA teams (typically 30 teams)</returns>
    /// <remarks>
    /// SportRadar endpoint: /league/hierarchy.json
    /// Response structure is hierarchical: League → Conferences (Eastern/Western) → Divisions → Teams
    /// Each team includes: ID, Name, Alias (abbreviation), Market (city)
    /// Returns empty collection on failure rather than throwing exceptions.
    /// </remarks>
    public async Task<IEnumerable<NBATeamInfo>> GetNBATeamsAsync()
    {
        try
        {
            // SportRadar league hierarchy endpoint provides nested structure of conferences/divisions/teams
            var url = $"{_baseUrl}/league/hierarchy.json?api_key={_apiKey}";

            _logger.LogInformation("Fetching NBA teams");

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync();

            // Validate response content
            if (string.IsNullOrWhiteSpace(jsonResponse))
            {
                _logger.LogWarning("Failed to fetch teams. API returned empty content.");
                return [];
            }

            // Parse hierarchy response to extract teams from nested structure
            var hierarchyResponse = JsonSerializer.Deserialize<NBAHierarchyResponse>(jsonResponse);

            var teams = new List<NBATeamInfo>();

            // Validate hierarchy structure exists
            if (hierarchyResponse?.Conferences == null)
                return teams;

            // Navigate hierarchy: Conferences (Eastern/Western)
            foreach (var conference in hierarchyResponse.Conferences)
            {
                // Skip conferences with no divisions
                if (conference.Divisions == null || !conference.Divisions.Any())
                    continue;

                // Navigate hierarchy: Divisions (e.g., Atlantic, Central, Southeast, etc.)
                foreach (var division in conference.Divisions)
                {
                    // Skip divisions with no teams
                    if (division.Teams == null || !division.Teams.Any())
                        continue;

                    // Extract all teams from this division
                    // Each team includes: Id, Name, Alias, Market
                    teams.AddRange(division.Teams);
                }
            }

            return teams;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching NBA teams");
            throw;
        }
    }

    /// <summary>
    /// Fetches betting odds for a specific NBA game from SportRadar Odds Comparison API.
    /// Returns moneyline, spread, and totals from multiple bookmakers.
    /// </summary>
    /// <param name="gameId">SportRadar game ID (SRID format: sr:match:xxxxx)</param>
    /// <returns>NBAOddsResponse with all available odds</returns>
    public async Task<NBAOddsResponse?> GetNBAOddsAsync(string gameId)
    {
        try
        {
            // SportRadar Odds Comparison API endpoint
            // Format: /v1/sport_events/{event_id}/markets.json?api_key={key}
            var url = $"{_baseUrl}/sport_events/{gameId}/markets.json?api_key={_apiKey}";

            _logger.LogInformation("Fetching odds for game {GameId} from SportRadar Odds API", gameId);

            // Add delay to respect rate limits (SportRadar typically allows 1 request/second on trial)
            await Task.Delay(1000);

            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch odds for game {GameId}. Status: {Status}",
                    gameId, response.StatusCode);

                // Return null rather than throwing - odds may not be available yet
                return null;
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();

            if (string.IsNullOrWhiteSpace(jsonResponse))
            {
                _logger.LogWarning("Failed to fetch odds for game {GameId}. API returned empty content.",
                    gameId);

                // Return null rather than throwing - odds may not be available yet
                return null;
            }

            var oddsData = JsonSerializer.Deserialize<NBAOddsResponse>(jsonResponse);

            if (oddsData == null || !oddsData.Markets.Any())
            {
                _logger.LogInformation("No odds data returned for game: {GameId}", gameId);
                return null;
            }

            _logger.LogInformation(
                "Retrieved odds for game {GameId}: {MarketCount} markets from {BookmakerCount} bookmakers",
                gameId,
                oddsData.Markets.Count,
                oddsData.Markets.SelectMany(m => m.Bookmakers).Select(b => b.Name).Distinct().Count());

            return oddsData;
        }
        catch (HttpRequestException httpEx)
        {
            _logger.LogError(httpEx, "HTTP error fetching odds for game {GameId}", gameId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching odds for game {GameId}", gameId);
            throw;
        }
    }
}
