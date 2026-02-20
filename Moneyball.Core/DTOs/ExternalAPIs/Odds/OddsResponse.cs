namespace Moneyball.Core.DTOs.ExternalAPIs.Odds;

// Odds API Response Models (The Odds API format)
public class OddsResponse
{
    public List<OddsGame> Data { get; set; } = [];
}

public class OddsGame
{
    public string Id { get; set; } = string.Empty;
    public string SportKey { get; set; } = string.Empty;
    public DateTime CommenceTime { get; set; }
    public string HomeTeam { get; set; } = string.Empty;
    public string AwayTeam { get; set; } = string.Empty;
    public List<Bookmaker> Bookmakers { get; set; } = [];
}

public class Bookmaker
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public List<Market> Markets { get; set; } = [];
}

public class Market
{
    public string Key { get; set; } = string.Empty; // h2h, spreads, totals
    public List<Outcome> Outcomes { get; set; } = [];
}

public class Outcome
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; } // American odds
    public decimal? Point { get; set; } // For spreads and totals
}