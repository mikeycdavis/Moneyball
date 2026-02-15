CREATE TABLE NbaFeatureSet (
    GameId INT PRIMARY KEY,
    EloDiff FLOAT NOT NULL,
    RestDiff FLOAT NOT NULL,
    BackToBackDiff FLOAT NOT NULL,
    InjuryMinutesLostDiff FLOAT NOT NULL,
    HomeAdvantage FLOAT NOT NULL,

    CONSTRAINT FK_NbaFeatureSet_Game FOREIGN KEY (GameId)
        REFERENCES NbaGame(Id)
);
