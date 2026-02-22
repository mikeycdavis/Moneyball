// =====================================================
// NBA PLAYER LAYER - C# DTOs AND CLASSES
// =====================================================

using System;
using System.Collections.Generic;
using System.Linq;

namespace SportsBetting.Core.DTOs.ExternalAPIs
{
// =====================================================
// 1. SPORTRADAR API DTOs
// =====================================================

```
/// <summary>
/// Player profile from SportRadar API
/// </summary>
public class PlayerProfileDto
{
    public string Id { get; set; } // SportRadar player ID
    public string First_Name { get; set; }
    public string Last_Name { get; set; }
    public string Full_Name { get; set; }
    public string Jersey_Number { get; set; }
    public string Position { get; set; }
    public string Primary_Position { get; set; }
    public int? Height { get; set; } // Inches
    public int? Weight { get; set; } // Pounds
    public string Birth_Place { get; set; }
    public string Birthdate { get; set; } // ISO date string
    public string College { get; set; }
    public bool Active { get; set; }
    public bool Rookie { get; set; }
    
    // Draft info
    public int? Draft_Year { get; set; }
    public int? Draft_Round { get; set; }
    public int? Draft_Pick { get; set; }
}

/// <summary>
/// Season statistics aggregation from SportRadar
/// </summary>
public class SeasonStatsDto
{
    public string Player_Id { get; set; }
    public string Season { get; set; } // "2023-24"
    public string Team_Id { get; set; }
    
    // Games
    public int Games_Played { get; set; }
    public int Games_Started { get; set; }
    
    // Averages
    public SeasonAverages Average { get; set; }
    public SeasonTotals Total { get; set; }
}

public class SeasonAverages
{
    public decimal Minutes { get; set; }
    public decimal Points { get; set; }
    public decimal Field_Goals_Made { get; set; }
    public decimal Field_Goals_Att { get; set; }
    public decimal Field_Goals_Pct { get; set; }
    public decimal Three_Points_Made { get; set; }
    public decimal Three_Points_Att { get; set; }
    public decimal Three_Points_Pct { get; set; }
    public decimal Free_Throws_Made { get; set; }
    public decimal Free_Throws_Att { get; set; }
    public decimal Free_Throws_Pct { get; set; }
    public decimal Rebounds { get; set; }
    public decimal Offensive_Rebounds { get; set; }
    public decimal Defensive_Rebounds { get; set; }
    public decimal Assists { get; set; }
    public decimal Steals { get; set; }
    public decimal Blocks { get; set; }
    public decimal Turnovers { get; set; }
    public decimal Personal_Fouls { get; set; }
}

public class SeasonTotals
{
    public int Minutes { get; set; }
    public int Points { get; set; }
    public int Field_Goals_Made { get; set; }
    public int Field_Goals_Att { get; set; }
    public int Three_Points_Made { get; set; }
    public int Three_Points_Att { get; set; }
    public int Free_Throws_Made { get; set; }
    public int Free_Throws_Att { get; set; }
    public int Rebounds { get; set; }
    public int Offensive_Rebounds { get; set; }
    public int Defensive_Rebounds { get; set; }
    public int Assists { get; set; }
    public int Steals { get; set; }
    public int Blocks { get; set; }
    public int Turnovers { get; set; }
    public int Personal_Fouls { get; set; }
}

/// <summary>
/// Game-by-game boxscore from SportRadar
/// </summary>
public class GameBoxscoreDto
{
    public string Game_Id { get; set; }
    public string Game_Date { get; set; }
    public string Status { get; set; }
    public HomeTeamBoxscore Home { get; set; }
    public AwayTeamBoxscore Away { get; set; }
}

public class HomeTeamBoxscore
{
    public string Team_Id { get; set; }
    public List<PlayerGameStatsDto> Players { get; set; }
}

public class AwayTeamBoxscore
{
    public string Team_Id { get; set; }
    public List<PlayerGameStatsDto> Players { get; set; }
}

/// <summary>
/// Individual player stats for a single game from SportRadar
/// </summary>
public class PlayerGameStatsDto
{
    public string Player_Id { get; set; }
    public string Full_Name { get; set; }
    public string Jersey_Number { get; set; }
    public string Position { get; set; }
    public string Primary_Position { get; set; }
    
    // Playing time
    public string Minutes { get; set; } // Format: "32:15"
    public bool Starter { get; set; }
    public bool Played { get; set; }
    public bool Active { get; set; }
    
    // DNP handling
    public string Not_Playing_Reason { get; set; }
    public string Not_Playing_Description { get; set; }
    
    // Statistics
    public PlayerGameStatistics Statistics { get; set; }
}

public class PlayerGameStatistics
{
    public int Points { get; set; }
    public int Field_Goals_Made { get; set; }
    public int Field_Goals_Att { get; set; }
    public decimal? Field_Goals_Pct { get; set; }
    public int Three_Points_Made { get; set; }
    public int Three_Points_Att { get; set; }
    public decimal? Three_Points_Pct { get; set; }
    public int Free_Throws_Made { get; set; }
    public int Free_Throws_Att { get; set; }
    public decimal? Free_Throws_Pct { get; set; }
    public int Rebounds { get; set; }
    public int Offensive_Rebounds { get; set; }
    public int Defensive_Rebounds { get; set; }
    public int Assists { get; set; }
    public int Steals { get; set; }
    public int Blocks { get; set; }
    public int Turnovers { get; set; }
    public int Personal_Fouls { get; set; }
    public int? Plus_Minus { get; set; }
}
```

}

namespace SportsBetting.Core.Entities
{
// =====================================================
// 2. ENTITY CLASSES
// =====================================================

```
/// <summary>
/// Player entity matching database schema
/// </summary>
public class Player
{
    public int PlayerId { get; set; }
    public string ExternalPlayerId { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string FullName { get; set; }
    public string JerseyNumber { get; set; }
    public string Position { get; set; }
    public int? Height { get; set; }
    public int? Weight { get; set; }
    public DateTime? BirthDate { get; set; }
    public string College { get; set; }
    public int? CurrentTeamId { get; set; }
    public bool IsActive { get; set; }
    public bool IsRetired { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // Navigation properties
    public Team CurrentTeam { get; set; }
    public ICollection<PlayerSeasonStat> SeasonStats { get; set; }
    public ICollection<PlayerGameStat> GameStats { get; set; }
}

/// <summary>
/// Player season statistics entity
/// </summary>
public class PlayerSeasonStat
{
    public int PlayerSeasonStatId { get; set; }
    public int PlayerId { get; set; }
    public string Season { get; set; }
    public int TeamId { get; set; }
    public int GamesPlayed { get; set; }
    public int GamesStarted { get; set; }
    
    // All stats as per schema
    public decimal Points { get; set; }
    public decimal FieldGoalsMade { get; set; }
    public decimal FieldGoalsAttempted { get; set; }
    public decimal? FieldGoalPercentage { get; set; }
    public decimal ThreePointsMade { get; set; }
    public decimal ThreePointsAttempted { get; set; }
    public decimal? ThreePointPercentage { get; set; }
    public decimal FreeThrowsMade { get; set; }
    public decimal FreeThrowsAttempted { get; set; }
    public decimal? FreeThrowPercentage { get; set; }
    public decimal Rebounds { get; set; }
    public decimal OffensiveRebounds { get; set; }
    public decimal DefensiveRebounds { get; set; }
    public decimal Assists { get; set; }
    public decimal Steals { get; set; }
    public decimal Blocks { get; set; }
    public decimal Turnovers { get; set; }
    public decimal PersonalFouls { get; set; }
    public decimal MinutesPlayed { get; set; }
    
    // Computed
    public decimal PointsReboundsAssists { get; set; }
    public int? DoubleDoubles { get; set; }
    public int? TripleDoubles { get; set; }
    public decimal? DraftKingsFantasyScore { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // Navigation
    public Player Player { get; set; }
    public Team Team { get; set; }
}

/// <summary>
/// Player game statistics entity
/// </summary>
public class PlayerGameStat
{
    public int PlayerGameStatId { get; set; }
    public int PlayerId { get; set; }
    public int GameId { get; set; }
    public int TeamId { get; set; }
    public bool IsHomeGame { get; set; }
    public bool IsStarter { get; set; }
    public bool DidNotPlay { get; set; }
    public string DNPReason { get; set; }
    
    public int MinutesPlayed { get; set; }
    public int Seconds { get; set; }
    
    // All game stats as per schema
    public int Points { get; set; }
    public int FieldGoalsMade { get; set; }
    public int FieldGoalsAttempted { get; set; }
    public decimal? FieldGoalPercentage { get; set; }
    public int ThreePointsMade { get; set; }
    public int ThreePointsAttempted { get; set; }
    public decimal? ThreePointPercentage { get; set; }
    public int FreeThrowsMade { get; set; }
    public int FreeThrowsAttempted { get; set; }
    public decimal? FreeThrowPercentage { get; set; }
    public int Rebounds { get; set; }
    public int OffensiveRebounds { get; set; }
    public int DefensiveRebounds { get; set; }
    public int Assists { get; set; }
    public int Steals { get; set; }
    public int Blocks { get; set; }
    public int Turnovers { get; set; }
    public int PersonalFouls { get; set; }
    public int? PlusMinus { get; set; }
    
    // Computed columns
    public int PointsReboundsAssists { get; set; }
    public int IsDoubleDouble { get; set; }
    public int IsTripleDouble { get; set; }
    public decimal DraftKingsFantasyScore { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // Navigation
    public Player Player { get; set; }
    public Game Game { get; set; }
    public Team Team { get; set; }
}

/// <summary>
/// ML model metadata entity
/// </summary>
public class MLPlayerModel
{
    public int ModelId { get; set; }
    public string ModelName { get; set; }
    public string StatCategory { get; set; }
    public string ModelType { get; set; }
    public int Version { get; set; }
    public string ModelFilePath { get; set; }
    public DateTime TrainingStartDate { get; set; }
    public DateTime TrainingEndDate { get; set; }
    public int TrainingRecordCount { get; set; }
    public decimal? MAE { get; set; }
    public decimal? RMSE { get; set; }
    public decimal? R2Score { get; set; }
    public int? CVFolds { get; set; }
    public decimal? CVMeanMAE { get; set; }
    public decimal? CVStdMAE { get; set; }
    public string Hyperparameters { get; set; }
    public string FeatureImportance { get; set; }
    public bool IsActive { get; set; }
    public bool IsBestModel { get; set; }
    public DateTime TrainedAt { get; set; }
    public string TrainedBy { get; set; }
}
```

}

namespace SportsBetting.Core.Interfaces
{
// =====================================================
// 3. REPOSITORY INTERFACES
// =====================================================

```
/// <summary>
/// Repository for player data access
/// </summary>
public interface IPlayerRepository : IRepository<Player>
{
    Task<Player> GetByExternalIdAsync(string externalPlayerId);
    Task<IEnumerable<Player>> GetActivePlayersAsync();
    Task<IEnumerable<Player>> GetPlayersByTeamAsync(int teamId);
    Task<Player> GetWithStatsAsync(int playerId, string season);
}

/// <summary>
/// Repository for player game statistics
/// </summary>
public interface IPlayerGameStatsRepository : IRepository<PlayerGameStat>
{
    Task<IEnumerable<PlayerGameStat>> GetPlayerGameStatsAsync(int playerId, int lastNGames);
    Task<IEnumerable<PlayerGameStat>> GetGameBoxscoreAsync(int gameId);
    Task<PlayerGameStat> GetPlayerGameStatAsync(int playerId, int gameId);
    Task<IEnumerable<PlayerGameStat>> GetRecentStatsForMLAsync(int playerId, int gameCount);
}

/// <summary>
/// Repository for ML models
/// </summary>
public interface IMLPlayerModelRepository : IRepository<MLPlayerModel>
{
    Task<MLPlayerModel> GetBestModelForStatAsync(string statCategory);
    Task<IEnumerable<MLPlayerModel>> GetActiveModelsAsync();
    Task SetBestModelAsync(int modelId, string statCategory);
}
```

}

namespace SportsBetting.Infrastructure.Services
{
// =====================================================
// 4. PLAYER INGESTION SERVICE
// =====================================================

```
/// <summary>
/// Service for ingesting player data from SportRadar API
/// </summary>
public class PlayerIngestionService
{
    private readonly IMoneyballRepository _repository;
    private readonly ISportsDataService _sportsDataService;
    private readonly ILogger<PlayerIngestionService> _logger;
    private readonly DerivedStatCalculator _derivedStatCalculator;
    
    public PlayerIngestionService(
        IMoneyballRepository repository,
        ISportsDataService sportsDataService,
        ILogger<PlayerIngestionService> logger,
        DerivedStatCalculator derivedStatCalculator)
    {
        _repository = repository;
        _sportsDataService = sportsDataService;
        _logger = logger;
        _derivedStatCalculator = derivedStatCalculator;
    }
    
    /// <summary>
    /// Ingests all active NBA players from SportRadar
    /// </summary>
    public async Task IngestNBAPlayersAsync()
    {
        _logger.LogInformation("Starting NBA player ingestion");
        
        try
        {
            // Fetch all players from SportRadar API
            var playerProfiles = await _sportsDataService.GetNBAPlayersAsync();
            
            var playersUpserted = 0;
            
            foreach (var profile in playerProfiles)
            {
                // Check if player exists
                var existingPlayer = await _repository.Players
                    .FirstOrDefaultAsync(p => p.ExternalPlayerId == profile.Id);
                
                if (existingPlayer == null)
                {
                    // Create new player
                    var newPlayer = MapToPlayer(profile);
                    await _repository.Players.AddAsync(newPlayer);
                    playersUpserted++;
                    _logger.LogDebug("Created new player: {Name}", profile.Full_Name);
                }
                else
                {
                    // Update existing player
                    UpdatePlayerFromProfile(existingPlayer, profile);
                    await _repository.Players.UpdateAsync(existingPlayer);
                    playersUpserted++;
                    _logger.LogDebug("Updated player: {Name}", profile.Full_Name);
                }
            }
            
            await _repository.SaveChangesAsync();
            
            _logger.LogInformation(
                "NBA player ingestion complete. Upserted: {Count} players",
                playersUpserted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during NBA player ingestion");
            throw;
        }
    }
    
    /// <summary>
    /// Ingests game-by-game statistics for a date range
    /// </summary>
    public async Task IngestPlayerGameStatsAsync(DateTime startDate, DateTime endDate)
    {
        _logger.LogInformation(
            "Starting player game stats ingestion from {Start} to {End}",
            startDate, endDate);
        
        try
        {
            // Get all completed games in date range
            var games = await _repository.Games.GetGamesByDateRangeAsync(
                startDate, endDate, sportId: null);
            
            var statsIngested = 0;
            
            foreach (var game in games.Where(g => g.IsComplete))
            {
                // Fetch boxscore from SportRadar
                var boxscore = await _sportsDataService.GetGameBoxscoreAsync(game.ExternalGameId);
                
                if (boxscore == null) continue;
                
                // Process home team players
                foreach (var playerDto in boxscore.Home.Players)
                {
                    await UpsertPlayerGameStatAsync(playerDto, game, isHome: true);
                    statsIngested++;
                }
                
                // Process away team players
                foreach (var playerDto in boxscore.Away.Players)
                {
                    await UpsertPlayerGameStatAsync(playerDto, game, isHome: false);
                    statsIngested++;
                }
                
                await _repository.SaveChangesAsync();
            }
            
            _logger.LogInformation(
                "Player game stats ingestion complete. Stats ingested: {Count}",
                statsIngested);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during player game stats ingestion");
            throw;
        }
    }
    
    private async Task UpsertPlayerGameStatAsync(
        PlayerGameStatsDto dto, 
        Game game, 
        bool isHome)
    {
        // Find player
        var player = await _repository.Players
            .FirstOrDefaultAsync(p => p.ExternalPlayerId == dto.Player_Id);
        
        if (player == null)
        {
            _logger.LogWarning("Player not found: {PlayerId}", dto.Player_Id);
            return;
        }
        
        // Check if stat already exists
        var existing = await _repository.PlayerGameStats
            .FirstOrDefaultAsync(pgs => 
                pgs.PlayerId == player.PlayerId && pgs.GameId == game.GameId);
        
        if (existing == null)
        {
            // Create new
            var newStat = MapToPlayerGameStat(dto, player.PlayerId, game, isHome);
            await _repository.PlayerGameStats.AddAsync(newStat);
        }
        else
        {
            // Update existing
            UpdatePlayerGameStat(existing, dto);
            await _repository.PlayerGameStats.UpdateAsync(existing);
        }
    }
    
    private Player MapToPlayer(PlayerProfileDto dto)
    {
        return new Player
        {
            ExternalPlayerId = dto.Id,
            FirstName = dto.First_Name,
            LastName = dto.Last_Name,
            FullName = dto.Full_Name,
            JerseyNumber = dto.Jersey_Number,
            Position = dto.Primary_Position ?? dto.Position,
            Height = dto.Height,
            Weight = dto.Weight,
            BirthDate = ParseDate(dto.Birthdate),
            College = dto.College,
            IsActive = dto.Active,
            IsRetired = !dto.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
    
    private PlayerGameStat MapToPlayerGameStat(
        PlayerGameStatsDto dto, 
        int playerId, 
        Game game, 
        bool isHome)
    {
        var (minutes, seconds) = ParseMinutes(dto.Minutes);
        
        var stat = new PlayerGameStat
        {
            PlayerId = playerId,
            GameId = game.GameId,
            TeamId = isHome ? game.HomeTeamId : game.AwayTeamId,
            IsHomeGame = isHome,
            IsStarter = dto.Starter,
            DidNotPlay = !dto.Played,
            DNPReason = dto.Not_Playing_Reason,
            MinutesPlayed = minutes,
            Seconds = seconds,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        
        if (dto.Statistics != null)
        {
            stat.Points = dto.Statistics.Points;
            stat.FieldGoalsMade = dto.Statistics.Field_Goals_Made;
            stat.FieldGoalsAttempted = dto.Statistics.Field_Goals_Att;
            stat.FieldGoalPercentage = dto.Statistics.Field_Goals_Pct;
            stat.ThreePointsMade = dto.Statistics.Three_Points_Made;
            stat.ThreePointsAttempted = dto.Statistics.Three_Points_Att;
            stat.ThreePointPercentage = dto.Statistics.Three_Points_Pct;
            stat.FreeThrowsMade = dto.Statistics.Free_Throws_Made;
            stat.FreeThrowsAttempted = dto.Statistics.Free_Throws_Att;
            stat.FreeThrowPercentage = dto.Statistics.Free_Throws_Pct;
            stat.Rebounds = dto.Statistics.Rebounds;
            stat.OffensiveRebounds = dto.Statistics.Offensive_Rebounds;
            stat.DefensiveRebounds = dto.Statistics.Defensive_Rebounds;
            stat.Assists = dto.Statistics.Assists;
            stat.Steals = dto.Statistics.Steals;
            stat.Blocks = dto.Statistics.Blocks;
            stat.Turnovers = dto.Statistics.Turnovers;
            stat.PersonalFouls = dto.Statistics.Personal_Fouls;
            stat.PlusMinus = dto.Statistics.Plus_Minus;
        }
        
        return stat;
    }
    
    private (int minutes, int seconds) ParseMinutes(string minutesString)
    {
        if (string.IsNullOrWhiteSpace(minutesString))
            return (0, 0);
        
        var parts = minutesString.Split(':');
        if (parts.Length != 2)
            return (0, 0);
        
        int.TryParse(parts[0], out int minutes);
        int.TryParse(parts[1], out int seconds);
        
        return (minutes, seconds);
    }
    
    private DateTime? ParseDate(string dateString)
    {
        if (string.IsNullOrWhiteSpace(dateString))
            return null;
        
        if (DateTime.TryParse(dateString, out DateTime result))
            return result;
        
        return null;
    }
    
    private void UpdatePlayerFromProfile(Player player, PlayerProfileDto dto)
    {
        player.JerseyNumber = dto.Jersey_Number;
        player.Position = dto.Primary_Position ?? dto.Position;
        player.IsActive = dto.Active;
        player.IsRetired = !dto.Active;
        player.UpdatedAt = DateTime.UtcNow;
    }
    
    private void UpdatePlayerGameStat(PlayerGameStat stat, PlayerGameStatsDto dto)
    {
        // Update logic similar to mapping
        stat.UpdatedAt = DateTime.UtcNow;
        // ... update all fields
    }
}

// =====================================================
// 5. DERIVED STAT CALCULATOR
// =====================================================

/// <summary>
/// Calculates derived statistics (double-doubles, triple-doubles, etc.)
/// </summary>
public class DerivedStatCalculator
{
    /// <summary>
    /// Determines if a stat line is a double-double
    /// </summary>
    public bool IsDoubleDouble(PlayerGameStat stat)
    {
        var categories = new[] 
        {
            stat.Points,
            stat.Rebounds,
            stat.Assists,
            stat.Steals,
            stat.Blocks
        };
        
        return categories.Count(c => c >= 10) >= 2;
    }
    
    /// <summary>
    /// Determines if a stat line is a triple-double
    /// </summary>
    public bool IsTripleDouble(PlayerGameStat stat)
    {
        var categories = new[] 
        {
            stat.Points,
            stat.Rebounds,
            stat.Assists,
            stat.Steals,
            stat.Blocks
        };
        
        return categories.Count(c => c >= 10) >= 3;
    }
    
    /// <summary>
    /// Calculates rolling average for last N games
    /// </summary>
    public decimal CalculateRollingAverage(
        IEnumerable<PlayerGameStat> recentStats, 
        Func<PlayerGameStat, int> statSelector,
        int gameCount)
    {
        var stats = recentStats
            .Where(s => !s.DidNotPlay)
            .OrderByDescending(s => s.GameId)
            .Take(gameCount)
            .ToList();
        
        if (!stats.Any())
            return 0;
        
        return (decimal)stats.Average(s => statSelector(s));
    }
}

// =====================================================
// 6. DRAFTKINGS FANTASY CALCULATOR
// =====================================================

/// <summary>
/// Calculates DraftKings fantasy scores
/// Scoring: Points (1pt), 3PM (0.5pt), Reb (1.25pt), Ast (1.5pt), 
/// Stl (2pt), Blk (2pt), TO (-0.5pt), Double-double (+1.5pt), Triple-double (+3pt)
/// </summary>
public class DraftKingsFantasyCalculator
{
    private const decimal POINTS_VALUE = 1.0m;
    private const decimal THREE_PM_VALUE = 0.5m;
    private const decimal REBOUND_VALUE = 1.25m;
    private const decimal ASSIST_VALUE = 1.5m;
    private const decimal STEAL_VALUE = 2.0m;
    private const decimal BLOCK_VALUE = 2.0m;
    private const decimal TURNOVER_VALUE = -0.5m;
    private const decimal DOUBLE_DOUBLE_BONUS = 1.5m;
    private const decimal TRIPLE_DOUBLE_BONUS = 3.0m;
    
    private readonly DerivedStatCalculator _derivedStatCalculator;
    
    public DraftKingsFantasyCalculator(DerivedStatCalculator derivedStatCalculator)
    {
        _derivedStatCalculator = derivedStatCalculator;
    }
    
    /// <summary>
    /// Calculates DraftKings fantasy score for a game
    /// </summary>
    public decimal CalculateFantasyScore(PlayerGameStat stat)
    {
        var score = 
            (stat.Points * POINTS_VALUE) +
            (stat.ThreePointsMade * THREE_PM_VALUE) +
            (stat.Rebounds * REBOUND_VALUE) +
            (stat.Assists * ASSIST_VALUE) +
            (stat.Steals * STEAL_VALUE) +
            (stat.Blocks * BLOCK_VALUE) +
            (stat.Turnovers * TURNOVER_VALUE);
        
        // Add bonuses
        if (_derivedStatCalculator.IsTripleDouble(stat))
        {
            score += TRIPLE_DOUBLE_BONUS;
        }
        else if (_derivedStatCalculator.IsDoubleDouble(stat))
        {
            score += DOUBLE_DOUBLE_BONUS;
        }
        
        return score;
    }
    
    /// <summary>
    /// Projects fantasy score based on predicted stats
    /// </summary>
    public decimal ProjectFantasyScore(
        decimal projectedPoints,
        decimal projected3PM,
        decimal projectedRebounds,
        decimal projectedAssists,
        decimal projectedSteals,
        decimal projectedBlocks,
        decimal projectedTurnovers,
        decimal doubleDoubleProbability,
        decimal tripleDoubleProbability)
    {
        var baseScore = 
            (projectedPoints * POINTS_VALUE) +
            (projected3PM * THREE_PM_VALUE) +
            (projectedRebounds * REBOUND_VALUE) +
            (projectedAssists * ASSIST_VALUE) +
            (projectedSteals * STEAL_VALUE) +
            (projectedBlocks * BLOCK_VALUE) +
            (projectedTurnovers * TURNOVER_VALUE);
        
        // Add expected value of bonuses
        var bonusExpectation = 
            (tripleDoubleProbability * TRIPLE_DOUBLE_BONUS) +
            ((doubleDoubleProbability - tripleDoubleProbability) * DOUBLE_DOUBLE_BONUS);
        
        return baseScore + bonusExpectation;
    }
}
```

}