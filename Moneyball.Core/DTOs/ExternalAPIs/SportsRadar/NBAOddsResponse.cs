namespace Moneyball.Core.DTOs.ExternalAPIs.SportsRadar;

/// <summary>
/// Response from SportsRadar Odds Comparison API
/// Endpoint: /v1/sport_events/{event_id}/markets
/// </summary>
public class NBAOddsResponse
{
    /// <summary>
    /// Sport event ID
    /// </summary>
    public string Sport_Event_Id { get; set; } = string.Empty;

    /// <summary>
    /// List of betting markets with odds from multiple bookmakers
    /// </summary>
    public List<NBAMarket> Markets { get; set; } = new();
}

/// <summary>
/// Represents a betting market (moneyline, spread, total)
/// </summary>
public class NBAMarket
{
    /// <summary>
    /// Market ID
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Market name: "1x2" (moneyline), "pointspread", "totals"
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// List of bookmakers offering this market
    /// </summary>
    public List<NBABookmaker> Bookmakers { get; set; } = new();
}

/// <summary>
/// Represents a bookmaker with their odds
/// </summary>
public class NBABookmaker
{
    /// <summary>
    /// Bookmaker ID
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Bookmaker name (e.g., "DraftKings", "FanDuel")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// List of outcomes (home, away, over, under)
    /// </summary>
    public List<NBAOutcome> Outcomes { get; set; } = new();
}

/// <summary>
/// Represents an outcome with odds
/// </summary>
public class NBAOutcome
{
    /// <summary>
    /// Outcome type: "1" (home), "2" (away), "over", "under"
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// American odds (e.g., -110, +150)
    /// </summary>
    public decimal Odds { get; set; }

    /// <summary>
    /// Spread or total line (e.g., -3.5 for spread, 220.5 for total)
    /// </summary>
    public decimal? Line { get; set; }

    /// <summary>
    /// Probability (0-1)
    /// </summary>
    public decimal? Probability { get; set; }
}