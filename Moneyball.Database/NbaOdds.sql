CREATE TABLE NbaOdds (
    Id INT IDENTITY(1,1) PRIMARY KEY,

    GameId INT NOT NULL,
    Sportsbook NVARCHAR(100) NOT NULL,

    HomeMoneyline INT NOT NULL,   -- -150, +130
    AwayMoneyline INT NOT NULL,

    HomeImpliedProb DECIMAL(6,4) NULL,
    AwayImpliedProb DECIMAL(6,4) NULL,

    CapturedAt DATETIME2(0) NOT NULL 
        CONSTRAINT DF_NbaOdds_CapturedAt DEFAULT SYSDATETIME(),

    CONSTRAINT FK_NbaOdds_Game
        FOREIGN KEY (GameId) REFERENCES NbaGame(Id)
);