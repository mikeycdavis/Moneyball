– =====================================================
– NBA PLAYER LAYER - DATABASE SCHEMA
– =====================================================
– Purpose: Extend existing sports betting system with player-level data
– Supports: Player props, game-by-game stats, ML features
– =====================================================

– =====================================================
– 1. PLAYERS TABLE
– =====================================================
– Stores player profile and career information
CREATE TABLE Players (
PlayerId INT IDENTITY(1,1) PRIMARY KEY,

```
-- External identifiers
ExternalPlayerId NVARCHAR(100) NOT NULL UNIQUE, -- SportRadar player ID

-- Profile data
FirstName NVARCHAR(100) NOT NULL,
LastName NVARCHAR(100) NOT NULL,
FullName AS (FirstName + ' ' + LastName) PERSISTED, -- Computed column for queries
JerseyNumber NVARCHAR(10) NULL,
Position NVARCHAR(20) NULL, -- PG, SG, SF, PF, C
Height INT NULL, -- Height in inches
Weight INT NULL, -- Weight in pounds
BirthDate DATE NULL,
College NVARCHAR(200) NULL,

-- Current team (nullable for retired/free agents)
CurrentTeamId INT NULL,

-- Status tracking
IsActive BIT NOT NULL DEFAULT 1,
IsRetired BIT NOT NULL DEFAULT 0,

-- Audit fields
CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

-- Foreign keys
CONSTRAINT FK_Players_Teams FOREIGN KEY (CurrentTeamId) 
    REFERENCES Teams(TeamId),

-- Indexes for common queries
INDEX IX_Players_ExternalId NONCLUSTERED (ExternalPlayerId),
INDEX IX_Players_Team NONCLUSTERED (CurrentTeamId) WHERE CurrentTeamId IS NOT NULL,
INDEX IX_Players_Active NONCLUSTERED (IsActive) INCLUDE (FullName, Position),
INDEX IX_Players_Name NONCLUSTERED (LastName, FirstName)
```

);

– =====================================================
– 2. PLAYER SEASON STATS TABLE
– =====================================================
– Stores aggregated season statistics for each player
CREATE TABLE PlayerSeasonStats (
PlayerSeasonStatId INT IDENTITY(1,1) PRIMARY KEY,

```
PlayerId INT NOT NULL,
Season NVARCHAR(20) NOT NULL, -- e.g., "2023-24"
TeamId INT NOT NULL, -- Team player played for this season

-- Games played
GamesPlayed INT NOT NULL DEFAULT 0,
GamesStarted INT NOT NULL DEFAULT 0,

-- Shooting stats
Points DECIMAL(6,2) NOT NULL DEFAULT 0,
FieldGoalsMade DECIMAL(6,2) NOT NULL DEFAULT 0,
FieldGoalsAttempted DECIMAL(6,2) NOT NULL DEFAULT 0,
FieldGoalPercentage DECIMAL(5,3) NULL,
ThreePointsMade DECIMAL(6,2) NOT NULL DEFAULT 0,
ThreePointsAttempted DECIMAL(6,2) NOT NULL DEFAULT 0,
ThreePointPercentage DECIMAL(5,3) NULL,
FreeThrowsMade DECIMAL(6,2) NOT NULL DEFAULT 0,
FreeThrowsAttempted DECIMAL(6,2) NOT NULL DEFAULT 0,
FreeThrowPercentage DECIMAL(5,3) NULL,

-- Rebounding
Rebounds DECIMAL(6,2) NOT NULL DEFAULT 0,
OffensiveRebounds DECIMAL(6,2) NOT NULL DEFAULT 0,
DefensiveRebounds DECIMAL(6,2) NOT NULL DEFAULT 0,

-- Playmaking and defense
Assists DECIMAL(6,2) NOT NULL DEFAULT 0,
Steals DECIMAL(6,2) NOT NULL DEFAULT 0,
Blocks DECIMAL(6,2) NOT NULL DEFAULT 0,
Turnovers DECIMAL(6,2) NOT NULL DEFAULT 0,
PersonalFouls DECIMAL(6,2) NOT NULL DEFAULT 0,

-- Minutes
MinutesPlayed DECIMAL(7,2) NOT NULL DEFAULT 0,

-- Derived stats
PointsReboundsAssists AS (Points + Rebounds + Assists) PERSISTED, -- PRA
DoubleDoubles INT NULL, -- Count of double-doubles
TripleDoubles INT NULL, -- Count of triple-doubles

-- DraftKings fantasy scoring (per game average)
DraftKingsFantasyScore DECIMAL(6,2) NULL,

-- Audit
CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

-- Constraints
CONSTRAINT FK_PlayerSeasonStats_Player FOREIGN KEY (PlayerId) 
    REFERENCES Players(PlayerId),
CONSTRAINT FK_PlayerSeasonStats_Team FOREIGN KEY (TeamId) 
    REFERENCES Teams(TeamId),
CONSTRAINT UQ_PlayerSeasonStats UNIQUE (PlayerId, Season, TeamId),

-- Indexes optimized for prop queries
INDEX IX_PlayerSeasonStats_Player_Season NONCLUSTERED (PlayerId, Season) 
    INCLUDE (Points, Rebounds, Assists, ThreePointsMade),
INDEX IX_PlayerSeasonStats_Season NONCLUSTERED (Season) 
    INCLUDE (PlayerId, Points, MinutesPlayed)
```

);

– =====================================================
– 3. PLAYER GAME STATS TABLE
– =====================================================
– Stores game-by-game statistics for each player
CREATE TABLE PlayerGameStats (
PlayerGameStatId INT IDENTITY(1,1) PRIMARY KEY,

```
PlayerId INT NOT NULL,
GameId INT NOT NULL,
TeamId INT NOT NULL, -- Team player played for in this game

-- Game context
IsHomeGame BIT NOT NULL,
IsStarter BIT NOT NULL DEFAULT 0,
DidNotPlay BIT NOT NULL DEFAULT 0,
DNPReason NVARCHAR(100) NULL, -- e.g., "Injury", "Rest", "Coach's Decision"

-- Minutes played
MinutesPlayed INT NOT NULL DEFAULT 0, -- Total minutes (can exceed 48 with OT)
Seconds INT NOT NULL DEFAULT 0, -- Additional seconds

-- Shooting stats
Points INT NOT NULL DEFAULT 0,
FieldGoalsMade INT NOT NULL DEFAULT 0,
FieldGoalsAttempted INT NOT NULL DEFAULT 0,
FieldGoalPercentage DECIMAL(5,3) NULL,
ThreePointsMade INT NOT NULL DEFAULT 0,
ThreePointsAttempted INT NOT NULL DEFAULT 0,
ThreePointPercentage DECIMAL(5,3) NULL,
FreeThrowsMade INT NOT NULL DEFAULT 0,
FreeThrowsAttempted INT NOT NULL DEFAULT 0,
FreeThrowPercentage DECIMAL(5,3) NULL,

-- Rebounding
Rebounds INT NOT NULL DEFAULT 0,
OffensiveRebounds INT NOT NULL DEFAULT 0,
DefensiveRebounds INT NOT NULL DEFAULT 0,

-- Playmaking and defense
Assists INT NOT NULL DEFAULT 0,
Steals INT NOT NULL DEFAULT 0,
Blocks INT NOT NULL DEFAULT 0,
Turnovers INT NOT NULL DEFAULT 0,
PersonalFouls INT NOT NULL DEFAULT 0,

-- Plus/minus
PlusMinus INT NULL,

-- Derived stats (computed columns)
PointsReboundsAssists AS (Points + Rebounds + Assists) PERSISTED, -- PRA
IsDoubleDouble AS (
    CASE WHEN (
        (CASE WHEN Points >= 10 THEN 1 ELSE 0 END) +
        (CASE WHEN Rebounds >= 10 THEN 1 ELSE 0 END) +
        (CASE WHEN Assists >= 10 THEN 1 ELSE 0 END) +
        (CASE WHEN Steals >= 10 THEN 1 ELSE 0 END) +
        (CASE WHEN Blocks >= 10 THEN 1 ELSE 0 END)
    ) >= 2 THEN 1 ELSE 0 END
) PERSISTED,
IsTripleDouble AS (
    CASE WHEN (
        (CASE WHEN Points >= 10 THEN 1 ELSE 0 END) +
        (CASE WHEN Rebounds >= 10 THEN 1 ELSE 0 END) +
        (CASE WHEN Assists >= 10 THEN 1 ELSE 0 END) +
        (CASE WHEN Steals >= 10 THEN 1 ELSE 0 END) +
        (CASE WHEN Blocks >= 10 THEN 1 ELSE 0 END)
    ) >= 3 THEN 1 ELSE 0 END
) PERSISTED,

-- DraftKings fantasy scoring
-- Points (1pt), 3PM (0.5pt), Reb (1.25pt), Ast (1.5pt), Stl (2pt), Blk (2pt), TO (-0.5pt)
-- Double-double (+1.5pt), Triple-double (+3pt)
DraftKingsFantasyScore AS (
    (Points * 1.0) +
    (ThreePointsMade * 0.5) +
    (Rebounds * 1.25) +
    (Assists * 1.5) +
    (Steals * 2.0) +
    (Blocks * 2.0) +
    (Turnovers * -0.5) +
    (CASE WHEN (
        (CASE WHEN Points >= 10 THEN 1 ELSE 0 END) +
        (CASE WHEN Rebounds >= 10 THEN 1 ELSE 0 END) +
        (CASE WHEN Assists >= 10 THEN 1 ELSE 0 END) +
        (CASE WHEN Steals >= 10 THEN 1 ELSE 0 END) +
        (CASE WHEN Blocks >= 10 THEN 1 ELSE 0 END)
    ) >= 3 THEN 3.0 -- Triple-double
     WHEN (
        (CASE WHEN Points >= 10 THEN 1 ELSE 0 END) +
        (CASE WHEN Rebounds >= 10 THEN 1 ELSE 0 END) +
        (CASE WHEN Assists >= 10 THEN 1 ELSE 0 END) +
        (CASE WHEN Steals >= 10 THEN 1 ELSE 0 END) +
        (CASE WHEN Blocks >= 10 THEN 1 ELSE 0 END)
    ) >= 2 THEN 1.5 -- Double-double
    ELSE 0 END)
) PERSISTED,

-- Audit
CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

-- Constraints
CONSTRAINT FK_PlayerGameStats_Player FOREIGN KEY (PlayerId) 
    REFERENCES Players(PlayerId),
CONSTRAINT FK_PlayerGameStats_Game FOREIGN KEY (GameId) 
    REFERENCES Games(GameId),
CONSTRAINT FK_PlayerGameStats_Team FOREIGN KEY (TeamId) 
    REFERENCES Teams(TeamId),
CONSTRAINT UQ_PlayerGameStats UNIQUE (PlayerId, GameId),

-- Critical indexes for prop queries and ML feature generation
INDEX IX_PlayerGameStats_Player_Game NONCLUSTERED (PlayerId, GameId DESC) 
    INCLUDE (Points, Rebounds, Assists, ThreePointsMade, MinutesPlayed),
INDEX IX_PlayerGameStats_Game NONCLUSTERED (GameId) 
    INCLUDE (PlayerId, Points, Rebounds, Assists),
INDEX IX_PlayerGameStats_Player_Recent NONCLUSTERED (PlayerId, GameId DESC) 
    INCLUDE (Points, Rebounds, Assists, ThreePointsMade, Steals, Blocks, Turnovers, MinutesPlayed, IsHomeGame, DraftKingsFantasyScore)
    WHERE DidNotPlay = 0, -- Exclude DNPs from rolling averages
INDEX IX_PlayerGameStats_Team NONCLUSTERED (TeamId, GameId DESC)
```

);

– =====================================================
– 4. ML MODEL METADATA TABLE
– =====================================================
– Tracks trained models per stat category
CREATE TABLE MLPlayerModels (
ModelId INT IDENTITY(1,1) PRIMARY KEY,

```
ModelName NVARCHAR(200) NOT NULL, -- e.g., "Points_XGBoost_v1"
StatCategory NVARCHAR(50) NOT NULL, -- Points, Rebounds, Assists, 3PM, PRA, etc.
ModelType NVARCHAR(50) NOT NULL, -- LinearRegression, RandomForest, XGBoost, LightGBM
Version INT NOT NULL DEFAULT 1,

-- Model file location
ModelFilePath NVARCHAR(500) NOT NULL,

-- Training metadata
TrainingStartDate DATE NOT NULL,
TrainingEndDate DATE NOT NULL,
TrainingRecordCount INT NOT NULL,

-- Performance metrics
MAE DECIMAL(10,4) NULL, -- Mean Absolute Error
RMSE DECIMAL(10,4) NULL, -- Root Mean Squared Error
R2Score DECIMAL(10,4) NULL, -- R-squared

-- Cross-validation results
CVFolds INT NULL,
CVMeanMAE DECIMAL(10,4) NULL,
CVStdMAE DECIMAL(10,4) NULL,

-- Hyperparameters (JSON)
Hyperparameters NVARCHAR(MAX) NULL,

-- Feature importance (JSON)
FeatureImportance NVARCHAR(MAX) NULL,

-- Status
IsActive BIT NOT NULL DEFAULT 1, -- Active model for this stat category
IsBestModel BIT NOT NULL DEFAULT 0, -- Best performer for this stat

-- Audit
TrainedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
TrainedBy NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER,

-- Constraints
CONSTRAINT UQ_MLPlayerModels_Name UNIQUE (ModelName),
INDEX IX_MLPlayerModels_StatCategory NONCLUSTERED (StatCategory, IsActive, IsBestModel) 
    INCLUDE (ModelId, ModelType, MAE, ModelFilePath),
INDEX IX_MLPlayerModels_Best NONCLUSTERED (StatCategory, IsBestModel) 
    WHERE IsBestModel = 1
```

);

– =====================================================
– 5. ML_PLAYER_FEATURES VIEW
– =====================================================
– Feature engineering view for ML training and inference
– Generates rolling averages, splits, and contextual features
CREATE VIEW ML_PlayerFeatures AS
WITH GameContext AS (
– Get game details
SELECT
g.GameId,
g.GameDate,
g.HomeTeamId,
g.AwayTeamId,
g.HomeScore + g.AwayScore AS TotalScore – Pace proxy
FROM Games g
WHERE g.IsComplete = 1
),
RecentGames AS (
– Rolling averages: last 3, 5, 10 games
SELECT
pgs.PlayerId,
pgs.GameId,

```
    -- Last 3 games averages
    AVG(pgs.Points) OVER (
        PARTITION BY pgs.PlayerId 
        ORDER BY pgs.GameId 
        ROWS BETWEEN 3 PRECEDING AND 1 PRECEDING
    ) AS Points_L3,
    AVG(pgs.Rebounds) OVER (
        PARTITION BY pgs.PlayerId 
        ORDER BY pgs.GameId 
        ROWS BETWEEN 3 PRECEDING AND 1 PRECEDING
    ) AS Rebounds_L3,
    AVG(pgs.Assists) OVER (
        PARTITION BY pgs.PlayerId 
        ORDER BY pgs.GameId 
        ROWS BETWEEN 3 PRECEDING AND 1 PRECEDING
    ) AS Assists_L3,
    AVG(pgs.ThreePointsMade) OVER (
        PARTITION BY pgs.PlayerId 
        ORDER BY pgs.GameId 
        ROWS BETWEEN 3 PRECEDING AND 1 PRECEDING
    ) AS ThreePointsMade_L3,
    AVG(pgs.MinutesPlayed) OVER (
        PARTITION BY pgs.PlayerId 
        ORDER BY pgs.GameId 
        ROWS BETWEEN 3 PRECEDING AND 1 PRECEDING
    ) AS Minutes_L3,
    
    -- Last 5 games averages
    AVG(pgs.Points) OVER (
        PARTITION BY pgs.PlayerId 
        ORDER BY pgs.GameId 
        ROWS BETWEEN 5 PRECEDING AND 1 PRECEDING
    ) AS Points_L5,
    AVG(pgs.Rebounds) OVER (
        PARTITION BY pgs.PlayerId 
        ORDER BY pgs.GameId 
        ROWS BETWEEN 5 PRECEDING AND 1 PRECEDING
    ) AS Rebounds_L5,
    AVG(pgs.Assists) OVER (
        PARTITION BY pgs.PlayerId 
        ORDER BY pgs.GameId 
        ROWS BETWEEN 5 PRECEDING AND 1 PRECEDING
    ) AS Assists_L5,
    
    -- Last 10 games averages
    AVG(pgs.Points) OVER (
        PARTITION BY pgs.PlayerId 
        ORDER BY pgs.GameId 
        ROWS BETWEEN 10 PRECEDING AND 1 PRECEDING
    ) AS Points_L10,
    AVG(pgs.Rebounds) OVER (
        PARTITION BY pgs.PlayerId 
        ORDER BY pgs.GameId 
        ROWS BETWEEN 10 PRECEDING AND 1 PRECEDING
    ) AS Rebounds_L10,
    AVG(pgs.Assists) OVER (
        PARTITION BY pgs.PlayerId 
        ORDER BY pgs.GameId 
        ROWS BETWEEN 10 PRECEDING AND 1 PRECEDING
    ) AS Assists_L10
    
FROM PlayerGameStats pgs
WHERE pgs.DidNotPlay = 0
```

),
HomeSplits AS (
– Home/away performance splits
SELECT
pgs.PlayerId,
pgs.GameId,
AVG(CASE WHEN pgs.IsHomeGame = 1 THEN pgs.Points ELSE NULL END) OVER (
PARTITION BY pgs.PlayerId
) AS Points_HomeAvg,
AVG(CASE WHEN pgs.IsHomeGame = 0 THEN pgs.Points ELSE NULL END) OVER (
PARTITION BY pgs.PlayerId
) AS Points_AwayAvg
FROM PlayerGameStats pgs
WHERE pgs.DidNotPlay = 0
),
RestDays AS (
– Calculate rest days between games
SELECT
pgs.PlayerId,
pgs.GameId,
DATEDIFF(DAY,
LAG(gc.GameDate) OVER (PARTITION BY pgs.PlayerId ORDER BY pgs.GameId),
gc.GameDate
) AS DaysSinceLastGame,
CASE
WHEN DATEDIFF(DAY,
LAG(gc.GameDate) OVER (PARTITION BY pgs.PlayerId ORDER BY pgs.GameId),
gc.GameDate
) <= 1 THEN 1
ELSE 0
END AS IsBackToBack
FROM PlayerGameStats pgs
INNER JOIN GameContext gc ON pgs.GameId = gc.GameId
)
SELECT
– Identifiers
pgs.PlayerGameStatId,
pgs.PlayerId,
p.FullName AS PlayerName,
p.Position,
pgs.GameId,
pgs.TeamId,
gc.GameDate,

```
-- Target variables (what we're predicting)
pgs.Points AS Target_Points,
pgs.Rebounds AS Target_Rebounds,
pgs.Assists AS Target_Assists,
pgs.ThreePointsMade AS Target_3PM,
pgs.PointsReboundsAssists AS Target_PRA,
pgs.DraftKingsFantasyScore AS Target_FantasyScore,
pgs.IsDoubleDouble AS Target_DoubleDouble,
pgs.IsTripleDouble AS Target_TripleDouble,

-- Rolling averages (features)
rg.Points_L3,
rg.Rebounds_L3,
rg.Assists_L3,
rg.ThreePointsMade_L3,
rg.Minutes_L3,
rg.Points_L5,
rg.Rebounds_L5,
rg.Assists_L5,
rg.Points_L10,
rg.Rebounds_L10,
rg.Assists_L10,

-- Season averages (features)
pss.Points AS Season_Avg_Points,
pss.Rebounds AS Season_Avg_Rebounds,
pss.Assists AS Season_Avg_Assists,
pss.MinutesPlayed AS Season_Avg_Minutes,

-- Home/away splits (features)
hs.Points_HomeAvg,
hs.Points_AwayAvg,
pgs.IsHomeGame,

-- Rest and schedule (features)
rd.DaysSinceLastGame,
rd.IsBackToBack,

-- Minutes trend (feature)
rg.Minutes_L3,

-- Pace proxy (feature)
gc.TotalScore AS OpponentPace,

-- Opponent (feature - to join with defensive stats)
CASE WHEN pgs.IsHomeGame = 1 
    THEN gc.AwayTeamId 
    ELSE gc.HomeTeamId 
END AS OpponentTeamId
```

FROM PlayerGameStats pgs
INNER JOIN Players p ON pgs.PlayerId = p.PlayerId
INNER JOIN GameContext gc ON pgs.GameId = gc.GameId
LEFT JOIN RecentGames rg ON pgs.PlayerId = rg.PlayerId AND pgs.GameId = rg.GameId
LEFT JOIN HomeSplits hs ON pgs.PlayerId = hs.PlayerId AND pgs.GameId = hs.GameId
LEFT JOIN RestDays rd ON pgs.PlayerId = rd.PlayerId AND pgs.GameId = rd.GameId
LEFT JOIN PlayerSeasonStats pss ON pgs.PlayerId = pss.PlayerId
AND pss.Season = (SELECT TOP 1 Season FROM PlayerSeasonStats WHERE PlayerId = pgs.PlayerId ORDER BY Season DESC)
WHERE pgs.DidNotPlay = 0; – Exclude DNPs from training data

GO

– =====================================================
– 6. HELPER STORED PROCEDURES
– =====================================================

– Get latest features for a player for inference
CREATE PROCEDURE sp_GetPlayerFeaturesForInference
@PlayerId INT,
@NextGameDate DATE,
@OpponentTeamId INT,
@IsHomeGame BIT
AS
BEGIN
SET NOCOUNT ON;

```
-- This SP would generate features for upcoming game
-- Similar to ML_PlayerFeatures view but for future game

SELECT 
    @PlayerId AS PlayerId,
    @NextGameDate AS GameDate,
    @OpponentTeamId AS OpponentTeamId,
    @IsHomeGame AS IsHomeGame,
    
    -- Rolling averages from last N games
    (SELECT AVG(Points) FROM (
        SELECT TOP 3 Points 
        FROM PlayerGameStats 
        WHERE PlayerId = @PlayerId AND DidNotPlay = 0 
        ORDER BY GameId DESC
    ) t) AS Points_L3,
    
    -- Add other features...
    1 AS Placeholder; -- Replace with full feature calculation
```

END;
GO

– =====================================================
– 7. SAMPLE DATA QUERIES
– =====================================================

– Get player prop averages for betting lines
– SELECT
–     p.FullName,
–     AVG(pgs.Points) AS Avg_Points,
–     AVG(pgs.Rebounds) AS Avg_Rebounds,
–     AVG(pgs.Assists) AS Avg_Assists,
–     AVG(pgs.ThreePointsMade) AS Avg_3PM
– FROM PlayerGameStats pgs
– INNER JOIN Players p ON pgs.PlayerId = p.PlayerId
– WHERE pgs.DidNotPlay = 0
–     AND pgs.GameId IN (SELECT TOP 10 GameId FROM Games ORDER BY GameDate DESC)
– GROUP BY p.FullName;

– Get best model for each stat category
– SELECT
–     StatCategory,
–     ModelName,
–     ModelType,
–     MAE,
–     RMSE,
–     R2Score
– FROM MLPlayerModels
– WHERE IsBestModel = 1
– ORDER BY StatCategory;