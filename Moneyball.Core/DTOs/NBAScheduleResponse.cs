namespace Moneyball.Service.ExternalAPIs.DTO;

// NBA API Response Models (based on SportRadar API structure)
public class NBAScheduleResponse
{
    public List<NBAGame> Games { get; set; } = [];
}

public class NBAGame
{
    public string Id { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime Scheduled { get; set; }
    public NBATeamInfo Home { get; set; } = new();
    public NBATeamInfo Away { get; set; } = new();
    public int? HomePoints { get; set; }
    public int? AwayPoints { get; set; }
}

public class NBATeamInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Alias { get; set; } = string.Empty;
    public string Market { get; set; } = string.Empty;
}

public class NBAGameStatistics
{
    public string Id { get; set; } = string.Empty;
    public NBATeamStatistics Home { get; set; } = new();
    public NBATeamStatistics Away { get; set; } = new();
}

public class NBATeamStatistics
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public NBAStatistics Statistics { get; set; } = new();
}

public class NBAStatistics
{
    public int FieldGoalsMade { get; set; }
    public int FieldGoalsAttempted { get; set; }
    public decimal FieldGoalPercentage { get; set; }
    public int ThreePointsMade { get; set; }
    public int ThreePointsAttempted { get; set; }
    public decimal ThreePointPercentage { get; set; }
    public int FreeThrowsMade { get; set; }
    public int FreeThrowsAttempted { get; set; }
    public decimal FreeThrowPercentage { get; set; }
    public int Rebounds { get; set; }
    public int OffensiveRebounds { get; set; }
    public int DefensiveRebounds { get; set; }
    public int Assists { get; set; }
    public int Steals { get; set; }
    public int Blocks { get; set; }
    public int Turnovers { get; set; }
    public int PersonalFouls { get; set; }
    public int Points { get; set; }
}

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