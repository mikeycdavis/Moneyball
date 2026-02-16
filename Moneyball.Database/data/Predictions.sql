CREATE TABLE [data].Predictions (
    PredictionId INT PRIMARY KEY IDENTITY(1,1),
    ModelId INT FOREIGN KEY REFERENCES ml.Models(ModelId),
    GameId INT FOREIGN KEY REFERENCES [data].Games(GameId),
    PredictedHomeWinProbability DECIMAL(5,4) NOT NULL,
    PredictedAwayWinProbability DECIMAL(5,4) NOT NULL,
    Edge DECIMAL(10,4), -- Expected value vs market odds
    Confidence DECIMAL(5,4), -- Model confidence score
    CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
    INDEX IX_Predictions_GameId (GameId),
    INDEX IX_Predictions_ModelId (ModelId)
);