CREATE TABLE NbaGame (
    Id INT IDENTITY PRIMARY KEY,
    GameDate DATE NOT NULL,
    HomeTeamId INT NOT NULL,
    AwayTeamId INT NOT NULL,
    HomeScore INT NULL,
    AwayScore INT NULL,

    CONSTRAINT FK_NbaGame_HomeTeam FOREIGN KEY (HomeTeamId)
        REFERENCES NbaTeam(Id),

    CONSTRAINT FK_NbaGame_AwayTeam FOREIGN KEY (AwayTeamId)
        REFERENCES NbaTeam(Id)
);
