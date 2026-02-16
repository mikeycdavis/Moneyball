CREATE TABLE BettingRecommendations (
    RecommendationId INT PRIMARY KEY IDENTITY(1,1),
    PredictionId INT FOREIGN KEY REFERENCES [data].Predictions(PredictionId),
    RecommendedBetType NVARCHAR(50), -- 'Moneyline', 'Spread', etc.
    RecommendedTeamId INT FOREIGN KEY REFERENCES [data].Teams(TeamId),
    Edge DECIMAL(10,4),
    KellyFraction DECIMAL(5,4), -- Kelly Criterion fraction
    RecommendedStakePercentage DECIMAL(5,4),
    MinBankroll DECIMAL(10,2),
    CreatedAt DATETIME2 DEFAULT GETUTCDATE()
);