using Moneyball.Core.Enums;

namespace Moneyball.Core.DTOs
{
    public class GameResponse
    {
        public int GameId { get; set; }
        public string? ExternalGameId { get; set; }
        public string Sport { get; set; } = string.Empty;
        public TeamResponse HomeTeam { get; set; } = new();
        public TeamResponse AwayTeam { get; set; } = new();
        public DateTime GameDate { get; set; }
        public GameStatus Status { get; set; }
        public ScoreResponse Score { get; set; } = new();
        public IEnumerable<OddsResponse> Odds { get; set; } = [];
        public IEnumerable<StatisticResponse> Statistics { get; set; } = [];
        public IEnumerable<PredictionResponse> Predictions { get; set; } = [];
    }

    public class TeamResponse
    {
        public int TeamId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Abbreviation { get; set; }
        public string? City { get; set; }
    }

    public class ScoreResponse
    {
        public int? Home { get; set; }
        public int? Away { get; set; }
    }

    public class OddsResponse
    {
        public string BookmakerName { get; set; } = string.Empty;
        public MoneylineResponse Moneyline { get; set; } = new();
        public SpreadResponse Spread { get; set; } = new();
        public TotalResponse Total { get; set; } = new();
        public DateTime RecordedAt { get; set; }
    }

    public class MoneylineResponse
    {
        public decimal? Home { get; set; }
        public decimal? Away { get; set; }
    }

    public class SpreadResponse
    {
        public decimal? Home { get; set; }
        public decimal? HomeOdds { get; set; }
        public decimal? Away { get; set; }
        public decimal? AwayOdds { get; set; }
    }

    public class TotalResponse
    {
        public decimal? Line { get; set; }
        public decimal? Over { get; set; }
        public decimal? Under { get; set; }
    }

    public class StatisticResponse
    {
        public string HomeOrAway { get; set; } = string.Empty;
        public int? Points { get; set; }
        public int? FieldGoalsMade { get; set; }
        public int? FieldGoalsAttempted { get; set; }
        public decimal? FieldGoalPercentage { get; set; }
        public int? ThreePointsMade { get; set; }
        public int? Assists { get; set; }
        public int? Rebounds { get; set; }
        public int? Turnovers { get; set; }
    }

    public class PredictionResponse
    {
        public string Model { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public decimal PredictedHomeWinProbability { get; set; }
        public decimal PredictedAwayWinProbability { get; set; }
        public decimal? Edge { get; set; }
        public decimal? Confidence { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
