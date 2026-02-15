CREATE TABLE NbaPrediction (
    Id INT IDENTITY PRIMARY KEY,
    GameId INT NOT NULL,
    ModelVersion NVARCHAR(50) NOT NULL,
    HomeOrAway NVARCHAR(4) NOT NULL,
    WinProbability FLOAT NOT NULL,
    Edge FLOAT NOT NULL,
    Confidence NVARCHAR(20) NOT NULL,
    CreatedAt DATETIME2 NOT NULL,

    CONSTRAINT FK_NbaPrediction_Game FOREIGN KEY (GameId)
        REFERENCES NbaGame(Id)
);
