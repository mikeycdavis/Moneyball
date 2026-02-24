-- Sports Betting Database Setup
-- Run this script to create the database and initial schema

USE master;
GO

-- Create database if it doesn't exist
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'Moneyball')
BEGIN
    CREATE DATABASE Moneyball;
END
GO

USE Moneyball;
GO

-- Sports table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Sports]') AND type in (N'U'))
BEGIN
    CREATE TABLE Sports (
        SportId INT PRIMARY KEY IDENTITY(1,1),
        Name NVARCHAR(50) NOT NULL UNIQUE,
        IsActive BIT DEFAULT 1
    );
END
GO

-- Teams table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Teams]') AND type in (N'U'))
BEGIN
    CREATE TABLE Teams (
        TeamId INT PRIMARY KEY IDENTITY(1,1),
        SportId INT NOT NULL FOREIGN KEY REFERENCES Sports(SportId),
        ExternalId NVARCHAR(50),
        Name NVARCHAR(100) NOT NULL,
        Abbreviation NVARCHAR(10),
        City NVARCHAR(100),
        Conference NVARCHAR(100),
        Division NVARCHAR(100)
    );

    CREATE INDEX IX_Teams_SportId_ExternalId ON Teams(SportId, ExternalId);
    CREATE INDEX IX_Teams_Name ON Teams(Name);
END
GO

-- Games table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Games]') AND type in (N'U'))
BEGIN
    CREATE TABLE Games (
        GameId INT PRIMARY KEY IDENTITY(1,1),
        SportId INT NOT NULL FOREIGN KEY REFERENCES Sports(SportId),
        ExternalGameId NVARCHAR(50),
        HomeTeamId INT NOT NULL FOREIGN KEY REFERENCES Teams(TeamId),
        AwayTeamId INT NOT NULL FOREIGN KEY REFERENCES Teams(TeamId),
        GameDate DATETIME2 NOT NULL,
        HomeScore INT NULL,
        AwayScore INT NULL,
        Status INT NOT NULL DEFAULT 0, -- 0=Scheduled, 1=InProgress, 2=Final, 3=Postponed, 4=Cancelled
        IsComplete BIT DEFAULT 0,
        Season NVARCHAR(50),
        Week INT,
        CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
        UpdatedAt DATETIME2
    );

    CREATE INDEX IX_Games_GameDate ON Games(GameDate);
    CREATE INDEX IX_Games_SportId_GameDate ON Games(SportId, GameDate);
    CREATE INDEX IX_Games_ExternalGameId ON Games(ExternalGameId);
    CREATE INDEX IX_Games_Status_GameDate ON Games(Status, GameDate);
END
GO

-- GameOdds table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[GameOdds]') AND type in (N'U'))
BEGIN
    CREATE TABLE GameOdds (
        OddsId INT PRIMARY KEY IDENTITY(1,1),
        GameId INT NOT NULL FOREIGN KEY REFERENCES Games(GameId),
        BookmakerName NVARCHAR(100) NOT NULL,
        HomeMoneyline DECIMAL(10,2),
        AwayMoneyline DECIMAL(10,2),
        HomeSpread DECIMAL(5,2),
        AwaySpread DECIMAL(5,2),
        HomeSpreadOdds DECIMAL(10,2),
        AwaySpreadOdds DECIMAL(10,2),
        OverUnder DECIMAL(5,2),
        OverOdds DECIMAL(10,2),
        UnderOdds DECIMAL(10,2),
        RecordedAt DATETIME2 DEFAULT GETUTCDATE()
    );

    CREATE INDEX IX_GameOdds_GameId_RecordedAt ON GameOdds(GameId, RecordedAt);
    CREATE INDEX IX_GameOdds_BookmakerName ON GameOdds(BookmakerName);
END
GO

-- TeamStatistics table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[TeamStatistics]') AND type in (N'U'))
BEGIN
    CREATE TABLE TeamStatistics (
        TeamStatisticId INT PRIMARY KEY IDENTITY(1,1),
        GameId INT NOT NULL FOREIGN KEY REFERENCES Games(GameId),
        TeamId INT NOT NULL FOREIGN KEY REFERENCES Teams(TeamId),
        IsHomeTeam BIT NOT NULL,
        Points INT,
        
        -- Basketball stats
        FieldGoalsMade INT,
        FieldGoalsAttempted INT,
        FieldGoalPercentage DECIMAL(5,4),
        ThreePointsMade INT,
        ThreePointsAttempted INT,
        ThreePointPercentage DECIMAL(5,4),
        FreeThrowsMade INT,
        FreeThrowsAttempted INT,
        FreeThrowPercentage DECIMAL(5,4),
        Rebounds INT,
        OffensiveRebounds INT,
        DefensiveRebounds INT,
        Assists INT,
        Steals INT,
        Blocks INT,
        Turnovers INT,
        PersonalFouls INT,
        
        -- Football stats
        PassingYards INT,
        RushingYards INT,
        TotalYards INT,
        Touchdowns INT,
        Interceptions INT,
        Fumbles INT,
        Sacks INT,
        TimeOfPossession DECIMAL(5,2),
        
        AdditionalStats NVARCHAR(MAX),
        CreatedAt DATETIME2 DEFAULT GETUTCDATE()
    );

    CREATE INDEX IX_TeamStatistics_GameId_TeamId ON TeamStatistics(GameId, TeamId);
END
GO

-- Models table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Models]') AND type in (N'U'))
BEGIN
    CREATE TABLE Models (
        ModelId INT PRIMARY KEY IDENTITY(1,1),
        Name NVARCHAR(100) NOT NULL,
        Version NVARCHAR(50) NOT NULL,
        SportId INT NOT NULL FOREIGN KEY REFERENCES Sports(SportId),
        Type INT NOT NULL, -- 0=Python, 1=MLNet, 2=External
        FilePath NVARCHAR(500),
        IsActive BIT DEFAULT 1,
        CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
        UpdatedAt DATETIME2 DEFAULT GETUTCDATE(),
        TrainedAt DATETIME2 DEFAULT GETUTCDATE(),
        TrainedBy NVARCHAR(50) NOT NULL,
        Metadata NVARCHAR(MAX),
        Description NVARCHAR(1000),
        CONSTRAINT UQ_Model_Name_Version UNIQUE (Name, Version)
    );

    CREATE INDEX IX_Models_SportId_IsActive ON Models(SportId, IsActive);
END
GO

-- ModelPerformance table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ModelPerformances]') AND type in (N'U'))
BEGIN
    CREATE TABLE ModelPerformances (
        PerformanceId INT PRIMARY KEY IDENTITY(1,1),
        ModelId INT NOT NULL FOREIGN KEY REFERENCES Models(ModelId),
        EvaluationDate DATETIME2 DEFAULT GETUTCDATE(),
        Accuracy DECIMAL(5,4),
        ROI DECIMAL(10,4),
        SampleSize INT,
        Metrics NVARCHAR(MAX)
    );
END
GO

-- Predictions table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Predictions]') AND type in (N'U'))
BEGIN
    CREATE TABLE Predictions (
        PredictionId INT PRIMARY KEY IDENTITY(1,1),
        ModelId INT NOT NULL FOREIGN KEY REFERENCES Models(ModelId),
        GameId INT NOT NULL FOREIGN KEY REFERENCES Games(GameId),
        PredictedHomeWinProbability DECIMAL(5,4) NOT NULL,
        PredictedAwayWinProbability DECIMAL(5,4) NOT NULL,
        Edge DECIMAL(10,4),
        Confidence DECIMAL(5,4),
        PredictedHomeScore DECIMAL(5,2),
        PredictedAwayScore DECIMAL(5,2),
        PredictedTotal DECIMAL(5,2),
        FeatureValues NVARCHAR(MAX),
        CreatedAt DATETIME2 DEFAULT GETUTCDATE()
    );

    CREATE INDEX IX_Predictions_GameId_ModelId ON Predictions(GameId, ModelId);
    CREATE INDEX IX_Predictions_CreatedAt ON Predictions(CreatedAt);
    CREATE INDEX IX_Predictions_ModelId_CreatedAt ON Predictions(ModelId, CreatedAt);
END
GO

-- BettingRecommendations table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[BettingRecommendations]') AND type in (N'U'))
BEGIN
    CREATE TABLE BettingRecommendations (
        RecommendationId INT PRIMARY KEY IDENTITY(1,1),
        PredictionId INT NOT NULL FOREIGN KEY REFERENCES Predictions(PredictionId),
        RecommendedBetType INT NOT NULL, -- 0=Moneyline, 1=Spread, 2=OverUnder, 3=PlayerProps
        RecommendedTeamId INT FOREIGN KEY REFERENCES Teams(TeamId),
        Edge DECIMAL(10,4) NOT NULL,
        KellyFraction DECIMAL(5,4),
        RecommendedStakePercentage DECIMAL(5,4) NOT NULL,
        MinBankroll DECIMAL(10,2),
        CreatedAt DATETIME2 DEFAULT GETUTCDATE()
    );

    CREATE INDEX IX_BettingRecommendations_Edge_CreatedAt ON BettingRecommendations(Edge, CreatedAt);
END
GO

-- Insert initial sports
IF NOT EXISTS (SELECT 1 FROM Sports WHERE Name = 'NBA')
BEGIN
    INSERT INTO Sports (Name, IsActive) VALUES ('NBA', 1);
END

IF NOT EXISTS (SELECT 1 FROM Sports WHERE Name = 'NFL')
BEGIN
    INSERT INTO Sports (Name, IsActive) VALUES ('NFL', 1);
END

IF NOT EXISTS (SELECT 1 FROM Sports WHERE Name = 'NHL')
BEGIN
    INSERT INTO Sports (Name, IsActive) VALUES ('NHL', 1);
END

IF NOT EXISTS (SELECT 1 FROM Sports WHERE Name = 'MLB')
BEGIN
    INSERT INTO Sports (Name, IsActive) VALUES ('MLB', 1);
END
GO

PRINT 'Database setup complete!';
GO