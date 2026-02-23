using Moneyball.Core.Enums;

namespace Moneyball.Core.DTOs
{
    public class GameResult
    {
        public int GameId { get; set; }
        public string? ExternalGameId { get; set; }
        public string Sport { get; set; } = string.Empty;
        public TeamResult HomeTeam { get; set; } = new();
        public TeamResult AwayTeam { get; set; } = new();
        public DateTime GameDate { get; set; }
        public GameStatus Status { get; set; }
        public ScoreResult Score { get; set; } = new();
        public IEnumerable<OddsResult> Odds { get; set; } = [];
        public IEnumerable<StatisticResult> Statistics { get; set; } = [];
        public IEnumerable<PredictionResult> Predictions { get; set; } = [];
    }

    public class TeamResult
    {
        public int TeamId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Abbreviation { get; set; }
        public string? City { get; set; }
    }

    public class ScoreResult
    {
        public int? Home { get; set; }
        public int? Away { get; set; }
    }

    public class OddsResult
    {
        public string BookmakerName { get; set; } = string.Empty;
        public MoneylineResult Moneyline { get; set; } = new();
        public SpreadResult Spread { get; set; } = new();
        public TotalResult Total { get; set; } = new();
        public DateTime RecordedAt { get; set; }
    }

    public class MoneylineResult
    {
        public decimal? Home { get; set; }
        public decimal? Away { get; set; }
    }

    public class SpreadResult
    {
        public decimal? Home { get; set; }
        public decimal? HomeOdds { get; set; }
        public decimal? Away { get; set; }
        public decimal? AwayOdds { get; set; }
    }

    public class TotalResult
    {
        public decimal? Line { get; set; }
        public decimal? Over { get; set; }
        public decimal? Under { get; set; }
    }

    public class StatisticResult
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
}
