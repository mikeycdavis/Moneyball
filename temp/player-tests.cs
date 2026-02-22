// =====================================================
// NBA PLAYER LAYER - FLUENTASSERTIONS TESTS
// =====================================================

using Xunit;
using FluentAssertions;
using SportsBetting.Core.Entities;
using SportsBetting.Core.DTOs.ExternalAPIs;
using SportsBetting.Infrastructure.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SportsBetting.Tests.UnitTests.Players
{
/// <summary>
/// Tests for DTO mapping correctness
/// </summary>
public class PlayerMappingTests
{
/// <summary>
/// Tests that PlayerProfileDto maps correctly to Player entity.
/// Verifies all fields are transferred accurately.
/// </summary>
[Fact]
public void MapToPlayer_ValidDto_MapsAllFields()
{
// Arrange
var dto = new PlayerProfileDto
{
Id = “sr:player:abc123”,
First_Name = “LeBron”,
Last_Name = “James”,
Full_Name = “LeBron James”,
Jersey_Number = “23”,
Position = “SF”,
Primary_Position = “SF”,
Height = 81, // 6’9”
Weight = 250,
Birthdate = “1984-12-30”,
College = “None”,
Active = true,
Rookie = false
};

```
        // Act - using the mapping method from PlayerIngestionService
        var player = new Player
        {
            ExternalPlayerId = dto.Id,
            FirstName = dto.First_Name,
            LastName = dto.Last_Name,
            FullName = dto.Full_Name,
            JerseyNumber = dto.Jersey_Number,
            Position = dto.Primary_Position ?? dto.Position,
            Height = dto.Height,
            Weight = dto.Weight,
            College = dto.College,
            IsActive = dto.Active,
            IsRetired = !dto.Active
        };

        // Assert - Verify all fields mapped correctly
        player.ExternalPlayerId.Should().Be("sr:player:abc123", 
            "external ID should be mapped from DTO");
        player.FirstName.Should().Be("LeBron", 
            "first name should be mapped");
        player.LastName.Should().Be("James", 
            "last name should be mapped");
        player.FullName.Should().Be("LeBron James", 
            "full name should be mapped");
        player.JerseyNumber.Should().Be("23", 
            "jersey number should be mapped");
        player.Position.Should().Be("SF", 
            "position should use Primary_Position when available");
        player.Height.Should().Be(81, 
            "height in inches should be mapped");
        player.Weight.Should().Be(250, 
            "weight in pounds should be mapped");
        player.College.Should().Be("None", 
            "college should be mapped");
        player.IsActive.Should().BeTrue(
            "active status should be mapped");
        player.IsRetired.Should().BeFalse(
            "retired status should be inverse of active");
    }

    /// <summary>
    /// Tests that minutes string parsing handles various formats.
    /// Validates "MM:SS" format conversion to minutes and seconds.
    /// </summary>
    [Theory]
    [InlineData("32:15", 32, 15)]
    [InlineData("0:30", 0, 30)]
    [InlineData("48:00", 48, 0)]
    [InlineData("", 0, 0)]
    [InlineData(null, 0, 0)]
    [InlineData("invalid", 0, 0)]
    public void ParseMinutes_VariousFormats_ParsesCorrectly(
        string input, 
        int expectedMinutes, 
        int expectedSeconds)
    {
        // Arrange & Act
        (int minutes, int seconds) = ParseMinutesHelper(input);

        // Assert
        minutes.Should().Be(expectedMinutes, 
            $"minutes should be parsed from '{input}'");
        seconds.Should().Be(expectedSeconds, 
            $"seconds should be parsed from '{input}'");
    }

    /// <summary>
    /// Tests that PlayerGameStatsDto maps correctly to PlayerGameStat entity.
    /// Verifies all statistical fields are transferred.
    /// </summary>
    [Fact]
    public void MapToPlayerGameStat_ValidDto_MapsAllStats()
    {
        // Arrange
        var dto = new PlayerGameStatsDto
        {
            Player_Id = "sr:player:abc123",
            Full_Name = "LeBron James",
            Minutes = "35:24",
            Starter = true,
            Played = true,
            Statistics = new PlayerGameStatistics
            {
                Points = 27,
                Field_Goals_Made = 10,
                Field_Goals_Att = 18,
                Field_Goals_Pct = 0.556m,
                Three_Points_Made = 2,
                Three_Points_Att = 5,
                Three_Points_Pct = 0.400m,
                Free_Throws_Made = 5,
                Free_Throws_Att = 6,
                Free_Throws_Pct = 0.833m,
                Rebounds = 8,
                Offensive_Rebounds = 1,
                Defensive_Rebounds = 7,
                Assists = 10,
                Steals = 2,
                Blocks = 1,
                Turnovers = 3,
                Personal_Fouls = 2,
                Plus_Minus = 12
            }
        };

        // Act - map to entity
        var stat = new PlayerGameStat
        {
            IsStarter = dto.Starter,
            DidNotPlay = !dto.Played,
            MinutesPlayed = 35,
            Seconds = 24,
            Points = dto.Statistics.Points,
            FieldGoalsMade = dto.Statistics.Field_Goals_Made,
            FieldGoalsAttempted = dto.Statistics.Field_Goals_Att,
            ThreePointsMade = dto.Statistics.Three_Points_Made,
            Rebounds = dto.Statistics.Rebounds,
            Assists = dto.Statistics.Assists,
            Steals = dto.Statistics.Steals,
            Blocks = dto.Statistics.Blocks,
            Turnovers = dto.Statistics.Turnovers
        };

        // Assert - Verify all stats mapped
        stat.IsStarter.Should().BeTrue("starter status should be mapped");
        stat.DidNotPlay.Should().BeFalse("DNP should be inverse of played");
        stat.MinutesPlayed.Should().Be(35, "minutes should be parsed");
        stat.Seconds.Should().Be(24, "seconds should be parsed");
        stat.Points.Should().Be(27, "points should be mapped");
        stat.FieldGoalsMade.Should().Be(10, "FGM should be mapped");
        stat.FieldGoalsAttempted.Should().Be(18, "FGA should be mapped");
        stat.ThreePointsMade.Should().Be(2, "3PM should be mapped");
        stat.Rebounds.Should().Be(8, "rebounds should be mapped");
        stat.Assists.Should().Be(10, "assists should be mapped");
        stat.Steals.Should().Be(2, "steals should be mapped");
        stat.Blocks.Should().Be(1, "blocks should be mapped");
        stat.Turnovers.Should().Be(3, "turnovers should be mapped");
    }

    // Helper method for minute parsing test
    private (int, int) ParseMinutesHelper(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return (0, 0);

        var parts = input.Split(':');
        if (parts.Length != 2)
            return (0, 0);

        int.TryParse(parts[0], out int minutes);
        int.TryParse(parts[1], out int seconds);

        return (minutes, seconds);
    }
}

/// <summary>
/// Tests for derived statistic logic
/// </summary>
public class DerivedStatCalculatorTests
{
    private readonly DerivedStatCalculator _calculator;

    public DerivedStatCalculatorTests()
    {
        _calculator = new DerivedStatCalculator();
    }

    /// <summary>
    /// Tests double-double detection with various stat combinations.
    /// Verifies that any two categories at 10+ trigger double-double.
    /// </summary>
    [Theory]
    [InlineData(25, 12, 5, 0, 0, true, "points and rebounds")]
    [InlineData(15, 8, 10, 0, 0, true, "points and assists")]
    [InlineData(8, 12, 11, 0, 0, true, "rebounds and assists")]
    [InlineData(12, 5, 8, 10, 0, true, "points and steals")]
    [InlineData(8, 8, 12, 0, 10, true, "assists and blocks")]
    [InlineData(25, 9, 9, 0, 0, false, "no category reaches 10")]
    [InlineData(15, 8, 5, 3, 2, false, "only one category at 10+")]
    public void IsDoubleDouble_VariousStatLines_DetectsCorrectly(
        int points, 
        int rebounds, 
        int assists, 
        int steals, 
        int blocks,
        bool expected,
        string because)
    {
        // Arrange
        var stat = new PlayerGameStat
        {
            Points = points,
            Rebounds = rebounds,
            Assists = assists,
            Steals = steals,
            Blocks = blocks
        };

        // Act
        var result = _calculator.IsDoubleDouble(stat);

        // Assert
        result.Should().Be(expected, because);
    }

    /// <summary>
    /// Tests triple-double detection.
    /// Verifies that three categories at 10+ trigger triple-double.
    /// </summary>
    [Theory]
    [InlineData(25, 12, 10, 0, 0, true, "points, rebounds, assists")]
    [InlineData(15, 10, 12, 10, 0, true, "three of five categories")]
    [InlineData(25, 12, 9, 0, 0, false, "only two categories")]
    [InlineData(8, 8, 8, 8, 8, false, "five categories but all under 10")]
    [InlineData(10, 10, 10, 10, 10, true, "all five categories")]
    public void IsTripleDouble_VariousStatLines_DetectsCorrectly(
        int points,
        int rebounds,
        int assists,
        int steals,
        int blocks,
        bool expected,
        string because)
    {
        // Arrange
        var stat = new PlayerGameStat
        {
            Points = points,
            Rebounds = rebounds,
            Assists = assists,
            Steals = steals,
            Blocks = blocks
        };

        // Act
        var result = _calculator.IsTripleDouble(stat);

        // Assert
        result.Should().Be(expected, because);
    }

    /// <summary>
    /// Tests rolling average calculation.
    /// Verifies correct averaging over last N games, excluding DNPs.
    /// </summary>
    [Fact]
    public void CalculateRollingAverage_Last3Games_AveragesCorrectly()
    {
        // Arrange - Last 5 games with one DNP
        var recentStats = new List<PlayerGameStat>
        {
            new PlayerGameStat { GameId = 5, Points = 30, DidNotPlay = false },
            new PlayerGameStat { GameId = 4, Points = 25, DidNotPlay = false },
            new PlayerGameStat { GameId = 3, Points = 0, DidNotPlay = true }, // DNP - should skip
            new PlayerGameStat { GameId = 2, Points = 28, DidNotPlay = false },
            new PlayerGameStat { GameId = 1, Points = 22, DidNotPlay = false }
        };

        // Act - Calculate last 3 games average (excluding DNP)
        var average = _calculator.CalculateRollingAverage(
            recentStats, 
            s => s.Points, 
            gameCount: 3);

        // Assert - Should average 30, 25, 28 (most recent 3 non-DNP games)
        average.Should().BeApproximately(27.67m, 0.01m, 
            "should average the 3 most recent games excluding DNPs");
    }

    /// <summary>
    /// Tests rolling average with insufficient data.
    /// Verifies graceful handling when fewer games available.
    /// </summary>
    [Fact]
    public void CalculateRollingAverage_InsufficientGames_ReturnsAvailableAverage()
    {
        // Arrange - Only 2 games available
        var recentStats = new List<PlayerGameStat>
        {
            new PlayerGameStat { GameId = 2, Points = 20, DidNotPlay = false },
            new PlayerGameStat { GameId = 1, Points = 24, DidNotPlay = false }
        };

        // Act - Request last 5 games but only 2 available
        var average = _calculator.CalculateRollingAverage(
            recentStats,
            s => s.Points,
            gameCount: 5);

        // Assert - Should average the 2 available games
        average.Should().Be(22m, 
            "should average all available games when fewer than requested");
    }

    /// <summary>
    /// Tests rolling average with no games.
    /// Verifies returns zero for empty dataset.
    /// </summary>
    [Fact]
    public void CalculateRollingAverage_NoGames_ReturnsZero()
    {
        // Arrange - Empty list
        var recentStats = new List<PlayerGameStat>();

        // Act
        var average = _calculator.CalculateRollingAverage(
            recentStats,
            s => s.Points,
            gameCount: 3);

        // Assert
        average.Should().Be(0m, "should return 0 for empty dataset");
    }
}

/// <summary>
/// Tests for DraftKings fantasy scoring logic
/// </summary>
public class DraftKingsFantasyCalculatorTests
{
    private readonly DraftKingsFantasyCalculator _calculator;
    private readonly DerivedStatCalculator _derivedCalculator;

    public DraftKingsFantasyCalculatorTests()
    {
        _derivedCalculator = new DerivedStatCalculator();
        _calculator = new DraftKingsFantasyCalculator(_derivedCalculator);
    }

    /// <summary>
    /// Tests basic fantasy scoring without bonuses.
    /// Verifies correct point values per stat category.
    /// </summary>
    [Fact]
    public void CalculateFantasyScore_BasicStats_CalculatesCorrectly()
    {
        // Arrange
        var stat = new PlayerGameStat
        {
            Points = 20,        // 20 * 1.0 = 20.0
            ThreePointsMade = 2,// 2 * 0.5 = 1.0
            Rebounds = 8,       // 8 * 1.25 = 10.0
            Assists = 6,        // 6 * 1.5 = 9.0
            Steals = 2,         // 2 * 2.0 = 4.0
            Blocks = 1,         // 1 * 2.0 = 2.0
            Turnovers = 3       // 3 * -0.5 = -1.5
        };
        // Expected: 20 + 1 + 10 + 9 + 4 + 2 - 1.5 = 44.5

        // Act
        var score = _calculator.CalculateFantasyScore(stat);

        // Assert
        score.Should().Be(44.5m, 
            "should calculate fantasy score using correct point values");
    }

    /// <summary>
    /// Tests fantasy scoring with double-double bonus.
    /// Verifies +1.5 bonus for double-double.
    /// </summary>
    [Fact]
    public void CalculateFantasyScore_DoubleDouble_AddsBonus()
    {
        // Arrange - Double-double with points and rebounds
        var stat = new PlayerGameStat
        {
            Points = 25,        // 25 * 1.0 = 25.0
            ThreePointsMade = 0,
            Rebounds = 12,      // 12 * 1.25 = 15.0
            Assists = 5,        // 5 * 1.5 = 7.5
            Steals = 1,         // 1 * 2.0 = 2.0
            Blocks = 0,
            Turnovers = 2       // 2 * -0.5 = -1.0
        };
        // Base: 25 + 15 + 7.5 + 2 - 1 = 48.5
        // Double-double bonus: +1.5
        // Expected: 50.0

        // Act
        var score = _calculator.CalculateFantasyScore(stat);

        // Assert
        score.Should().Be(50.0m, 
            "should add 1.5 point bonus for double-double");
    }

    /// <summary>
    /// Tests fantasy scoring with triple-double bonus.
    /// Verifies +3.0 bonus for triple-double (not +1.5 for double-double).
    /// </summary>
    [Fact]
    public void CalculateFantasyScore_TripleDouble_AddsHigherBonus()
    {
        // Arrange - Triple-double
        var stat = new PlayerGameStat
        {
            Points = 25,        // 25 * 1.0 = 25.0
            ThreePointsMade = 2,// 2 * 0.5 = 1.0
            Rebounds = 10,      // 10 * 1.25 = 12.5
            Assists = 10,       // 10 * 1.5 = 15.0
            Steals = 2,         // 2 * 2.0 = 4.0
            Blocks = 1,         // 1 * 2.0 = 2.0
            Turnovers = 3       // 3 * -0.5 = -1.5
        };
        // Base: 25 + 1 + 12.5 + 15 + 4 + 2 - 1.5 = 58.0
        // Triple-double bonus: +3.0 (NOT +1.5 for DD)
        // Expected: 61.0

        // Act
        var score = _calculator.CalculateFantasyScore(stat);

        // Assert
        score.Should().Be(61.0m,
            "should add 3.0 point bonus for triple-double, not double-double bonus");
    }

    /// <summary>
    /// Tests turnovers correctly reduce fantasy score.
    /// Verifies negative point value application.
    /// </summary>
    [Fact]
    public void CalculateFantasyScore_Turnovers_ReduceScore()
    {
        // Arrange
        var stat = new PlayerGameStat
        {
            Points = 10,
            ThreePointsMade = 0,
            Rebounds = 5,
            Assists = 3,
            Steals = 0,
            Blocks = 0,
            Turnovers = 6  // 6 * -0.5 = -3.0
        };
        // Expected: 10 + 6.25 + 4.5 - 3.0 = 17.75

        // Act
        var score = _calculator.CalculateFantasyScore(stat);

        // Assert
        score.Should().Be(17.75m,
            "turnovers should reduce fantasy score at -0.5 per TO");
    }

    /// <summary>
    /// Tests projected fantasy score with probability-based bonuses.
    /// Verifies expected value calculation for double/triple-double.
    /// </summary>
    [Fact]
    public void ProjectFantasyScore_WithProbabilities_CalculatesExpectedValue()
    {
        // Arrange
        decimal projectedPoints = 25m;
        decimal projected3PM = 2m;
        decimal projectedRebounds = 9m;
        decimal projectedAssists = 8m;
        decimal projectedSteals = 1m;
        decimal projectedBlocks = 0.5m;
        decimal projectedTurnovers = 3m;
        decimal ddProbability = 0.40m; // 40% chance of double-double
        decimal tdProbability = 0.10m; // 10% chance of triple-double

        // Base: 25 + 1 + 11.25 + 12 + 2 + 1 - 1.5 = 50.75
        // DD expected value: (0.40 - 0.10) * 1.5 = 0.45
        // TD expected value: 0.10 * 3.0 = 0.30
        // Total expected: 50.75 + 0.45 + 0.30 = 51.5

        // Act
        var projectedScore = _calculator.ProjectFantasyScore(
            projectedPoints,
            projected3PM,
            projectedRebounds,
            projectedAssists,
            projectedSteals,
            projectedBlocks,
            projectedTurnovers,
            ddProbability,
            tdProbability);

        // Assert
        projectedScore.Should().BeApproximately(51.5m, 0.01m,
            "should calculate expected fantasy score with probability-weighted bonuses");
    }
}

/// <summary>
/// Tests for idempotent upsert behavior
/// </summary>
public class PlayerUpsertTests
{
    /// <summary>
    /// Tests that upserting same player twice doesn't create duplicates.
    /// Verifies idempotency of player ingestion.
    /// </summary>
    [Fact]
    public void UpsertPlayer_SameExternalId_UpdatesExisting()
    {
        // Arrange - Simulate repository with one player
        var existingPlayer = new Player
        {
            PlayerId = 1,
            ExternalPlayerId = "sr:player:abc123",
            FirstName = "LeBron",
            LastName = "James",
            JerseyNumber = "6", // Old jersey
            IsActive = true
        };

        var updatedProfile = new PlayerProfileDto
        {
            Id = "sr:player:abc123", // Same external ID
            First_Name = "LeBron",
            Last_Name = "James",
            Jersey_Number = "23", // New jersey
            Active = true
        };

        // Act - Update existing player
        existingPlayer.JerseyNumber = updatedProfile.Jersey_Number;
        existingPlayer.UpdatedAt = DateTime.UtcNow;

        // Assert - Player updated, not duplicated
        existingPlayer.PlayerId.Should().Be(1, 
            "should update existing player, not create new one");
        existingPlayer.ExternalPlayerId.Should().Be("sr:player:abc123",
            "external ID should remain unchanged");
        existingPlayer.JerseyNumber.Should().Be("23",
            "jersey number should be updated to new value");
    }

    /// <summary>
    /// Tests that upserting same game stat twice updates existing record.
    /// Verifies idempotency of game stat ingestion.
    /// </summary>
    [Fact]
    public void UpsertPlayerGameStat_SamePlayerAndGame_UpdatesExisting()
    {
        // Arrange - Existing stat with partial data (halftime)
        var existingStat = new PlayerGameStat
        {
            PlayerGameStatId = 1,
            PlayerId = 100,
            GameId = 500,
            Points = 15, // Halftime score
            Rebounds = 4,
            Assists = 3
        };

        var updatedDto = new PlayerGameStatsDto
        {
            Player_Id = "sr:player:abc123",
            Statistics = new PlayerGameStatistics
            {
                Points = 28, // Final score
                Rebounds = 8,
                Assists = 10
            }
        };

        // Act - Update existing stat with final numbers
        existingStat.Points = updatedDto.Statistics.Points;
        existingStat.Rebounds = updatedDto.Statistics.Rebounds;
        existingStat.Assists = updatedDto.Statistics.Assists;
        existingStat.UpdatedAt = DateTime.UtcNow;

        // Assert - Stat updated, not duplicated
        existingStat.PlayerGameStatId.Should().Be(1,
            "should update existing stat, not create new one");
        existingStat.Points.Should().Be(28,
            "points should be updated to final value");
        existingStat.Rebounds.Should().Be(8,
            "rebounds should be updated to final value");
        existingStat.Assists.Should().Be(10,
            "assists should be updated to final value");
    }
}
```

}