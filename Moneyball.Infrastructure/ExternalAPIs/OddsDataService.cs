using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moneyball.Core.Interfaces.ExternalAPIs;
using Moneyball.Service.ExternalAPIs.DTO;
using System.Net.Http.Json;

namespace Moneyball.Infrastructure.ExternalAPIs;

public class OddsDataService : IOddsDataService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OddsDataService> _logger;
    private readonly string _apiKey;
    private readonly string _baseUrl;

    public OddsDataService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<OddsDataService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;

        _apiKey = _configuration["OddsAPI:ApiKey"] ?? throw new InvalidOperationException("OddsAPI:ApiKey not configured");
        _baseUrl = _configuration["OddsAPI:BaseUrl"] ?? "https://api.the-odds-api.com/v4";
    }

    public async Task<OddsResponse> GetOddsAsync(string sport, string region = "us", string market = "h2h")
    {
        try
        {
            // The Odds API format: /sports/{sport}/odds
            // sport: basketball_nba, americanfootball_nfl, etc.
            var url = $"{_baseUrl}/sports/{sport}/odds?" +
                      $"apiKey={_apiKey}" +
                      $"&regions={region}" +
                      $"&markets={market},spreads,totals" +
                      $"&oddsFormat=american";

            _logger.LogInformation("Fetching odds for sport: {Sport}", sport);

            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch odds for {Sport}. Status: {Status}",
                    sport, response.StatusCode);
                return new OddsResponse();
            }

            // Check remaining requests header
            if (response.Headers.TryGetValues("x-requests-remaining", out var remainingValues))
            {
                var remaining = remainingValues.FirstOrDefault();
                _logger.LogInformation("Odds API requests remaining: {Remaining}", remaining);
            }

            var oddsData = await response.Content.ReadFromJsonAsync<List<OddsGame>>();

            return new OddsResponse
            {
                Data = oddsData ?? []
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching odds for sport {Sport}", sport);
            throw;
        }
    }
}