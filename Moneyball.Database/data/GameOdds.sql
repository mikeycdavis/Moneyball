-- Market/Odds Data
CREATE TABLE [data].GameOdds (
    OddsId INT PRIMARY KEY IDENTITY(1,1),
    GameId INT FOREIGN KEY REFERENCES [data].Games(GameId),
    BookmakerName NVARCHAR(100),
    HomeMoneyline DECIMAL(10,2),
    AwayMoneyline DECIMAL(10,2),
    HomeSpread DECIMAL(5,2),
    AwaySpread DECIMAL(5,2),
    OverUnder DECIMAL(5,2),
    RecordedAt DATETIME2 DEFAULT GETUTCDATE(),
    INDEX IX_GameOdds_GameId (GameId)
);