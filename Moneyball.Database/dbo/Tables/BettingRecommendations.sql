CREATE TABLE [dbo].[BettingRecommendations] (
    [RecommendationId]           INT             IDENTITY (1, 1) NOT NULL,
    [PredictionId]               INT             NOT NULL,
    [RecommendedBetType]         INT             NOT NULL,
    [RecommendedTeamId]          INT             NULL,
    [Edge]                       DECIMAL (10, 4) NOT NULL,
    [KellyFraction]              DECIMAL (5, 4)  NULL,
    [RecommendedStakePercentage] DECIMAL (5, 4)  NOT NULL,
    [MinBankroll]                DECIMAL (10, 2) NULL,
    [CreatedAt]                  DATETIME2 (7)   DEFAULT (getutcdate()) NULL,
    PRIMARY KEY CLUSTERED ([RecommendationId] ASC),
    FOREIGN KEY ([PredictionId]) REFERENCES [dbo].[Predictions] ([PredictionId]),
    FOREIGN KEY ([RecommendedTeamId]) REFERENCES [dbo].[Teams] ([TeamId])
);


GO
CREATE NONCLUSTERED INDEX [IX_BettingRecommendations_Edge_CreatedAt]
    ON [dbo].[BettingRecommendations]([Edge] ASC, [CreatedAt] ASC);

