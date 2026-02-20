CREATE TABLE [dbo].[Predictions] (
    [PredictionId]                INT             IDENTITY (1, 1) NOT NULL,
    [ModelId]                     INT             NOT NULL,
    [GameId]                      INT             NOT NULL,
    [PredictedHomeWinProbability] DECIMAL (5, 4)  NOT NULL,
    [PredictedAwayWinProbability] DECIMAL (5, 4)  NOT NULL,
    [Edge]                        DECIMAL (10, 4) NULL,
    [Confidence]                  DECIMAL (5, 4)  NULL,
    [PredictedHomeScore]          DECIMAL (5, 2)  NULL,
    [PredictedAwayScore]          DECIMAL (5, 2)  NULL,
    [PredictedTotal]              DECIMAL (5, 2)  NULL,
    [FeatureValues]               NVARCHAR (MAX)  NULL,
    [CreatedAt]                   DATETIME2 (7)   DEFAULT (getutcdate()) NULL,
    PRIMARY KEY CLUSTERED ([PredictionId] ASC),
    FOREIGN KEY ([GameId]) REFERENCES [dbo].[Games] ([GameId]),
    FOREIGN KEY ([ModelId]) REFERENCES [dbo].[Models] ([ModelId])
);


GO
CREATE NONCLUSTERED INDEX [IX_Predictions_ModelId_CreatedAt]
    ON [dbo].[Predictions]([ModelId] ASC, [CreatedAt] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_Predictions_CreatedAt]
    ON [dbo].[Predictions]([CreatedAt] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_Predictions_GameId_ModelId]
    ON [dbo].[Predictions]([GameId] ASC, [ModelId] ASC);

