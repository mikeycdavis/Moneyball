namespace Moneyball.Core.DTOs.ML;

/// <summary>
/// ML.NET input class for game prediction features.
/// Properties should match the features expected by trained models.
/// Add/modify properties based on your specific feature engineering.
/// </summary>
public class GameFeatures
{
    // Win rates
    public float HomeWinRate { get; set; }
    public float AwayWinRate { get; set; }

    // Scoring averages
    public float HomePointsAvg { get; set; }
    public float AwayPointsAvg { get; set; }
    public float HomePointsAllowedAvg { get; set; }
    public float AwayPointsAllowedAvg { get; set; }

    // Rest and schedule
    public float RestDaysHome { get; set; }
    public float RestDaysAway { get; set; }
    public float IsHomeTeam { get; set; } // Binary: 1 for home, 0 for away

    // Streaks and momentum
    public float HomeWinStreak { get; set; }
    public float AwayWinStreak { get; set; }
    public float HomeLast5Wins { get; set; }
    public float AwayLast5Wins { get; set; }

    // Advanced metrics
    public float HomeOffensiveEfficiency { get; set; }
    public float AwayOffensiveEfficiency { get; set; }
    public float HomeDefensiveEfficiency { get; set; }
    public float AwayDefensiveEfficiency { get; set; }
}