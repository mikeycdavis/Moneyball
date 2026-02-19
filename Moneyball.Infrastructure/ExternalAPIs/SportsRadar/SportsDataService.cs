using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moneyball.Core.Interfaces.ExternalAPIs;
using Moneyball.Service.ExternalAPIs.DTO;
using System.Net.Http.Json;
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

    public async Task<IEnumerable<NBAGame>> GetNBAScheduleAsync(DateTime startDate, DateTime endDate)
    {
        try
        {
            // SportRadar endpoint format: /games/{year}/{month}/{day}/schedule.json
            var games = new List<NBAGame>();

            for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
            {
                var url = $"{_baseUrl}/games/{date:yyyy}/{date:MM}/{date:dd}/schedule.json?api_key={_apiKey}";

                _logger.LogInformation("Fetching NBA schedule for {Date}", date);

                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();

                    if (string.IsNullOrWhiteSpace(jsonResponse))
                    {
                        _logger.LogWarning("Failed to fetch schedule for {Date}. API returned empty content.",date);
                    }
                    else
                    {
                        var scheduleResponse = JsonSerializer.Deserialize<NBAScheduleResponse>(jsonResponse);

                        if (scheduleResponse?.Games != null)
                        {
                            games.AddRange(scheduleResponse.Games);
                        }
                    }
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogInformation("No games found for {Date}", date);
                }
                else
                {
                    _logger.LogWarning("Failed to fetch schedule for {Date}. Status: {Status}",
                        date, response.StatusCode);
                }

                // Rate limiting - be respectful to API
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

    public async Task<NBAGameStatistics?> GetNBAGameStatisticsAsync(string gameId)
    {
        try
        {
            var url = $"{_baseUrl}/games/{gameId}/statistics.json?api_key={_apiKey}";

            _logger.LogInformation("Fetching NBA game statistics for game {GameId}", gameId);

            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch game statistics for {GameId}. Status: {Status}",
                    gameId, response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<NBAGameStatistics>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching NBA game statistics for {GameId}", gameId);
            throw;
        }
    }

    public async Task<IEnumerable<NBATeamInfo>> GetNBATeamsAsync()
    {
        try
        {
            var url = $"{_baseUrl}/league/hierarchy.json?api_key={_apiKey}";

            _logger.LogInformation("Fetching NBA teams");

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            // Parse hierarchy response to extract teams
            var hierarchyResponse = await response.Content.ReadFromJsonAsync<NBAHierarchyResponse>();

            var teams = new List<NBATeamInfo>();
            if (hierarchyResponse?.Conferences != null)
            {
                foreach (var conference in hierarchyResponse.Conferences)
                {
                    if (conference.Divisions != null)
                    {
                        foreach (var division in conference.Divisions)
                        {
                            if (division.Teams != null)
                            {
                                teams.AddRange(division.Teams);
                            }
                        }
                    }
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
}

// Additional DTO for hierarchy response
public class NBAHierarchyResponse
{
    public List<NBAConference> Conferences { get; set; } = [];
}

public class NBAConference
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<NBADivision> Divisions { get; set; } = [];
}

public class NBADivision
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<NBATeamInfo> Teams { get; set; } = [];
}
