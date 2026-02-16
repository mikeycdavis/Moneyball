CREATE TABLE [data].Games (
    GameId INT PRIMARY KEY IDENTITY(1,1),
    SportId INT FOREIGN KEY REFERENCES [data].Sports(SportId),
    ExternalGameId NVARCHAR(50),
    HomeTeamId INT FOREIGN KEY REFERENCES [data].Teams(TeamId),
    AwayTeamId INT FOREIGN KEY REFERENCES [data].Teams(TeamId),
    GameDate DATETIME2 NOT NULL,
    HomeScore INT NULL,
    AwayScore INT NULL,
    IsComplete BIT DEFAULT 0,
    CreatedAt DATETIME2 DEFAULT GETUTCDATE()
);