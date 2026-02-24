CREATE TABLE [dbo].[BettingRecommendations] (
    [RecommendationId]           INT             IDENTITY (1, 1) NOT NULL,
    [PredictionId]               INT             NOT NULL,
    [RecommendedBetType]         INT             NOT NULL,
    [RecommendedTeamId]          INT             NULL,
    [Edge]                       DECIMAL (10, 4) NOT NULL,
    [KellyFraction]              DECIMAL (5, 4)  NULL,
    [RecommendedStakePercentage] DECIMAL (5, 4)  NOT NULL,
    [MinBankroll]                DECIMAL (10, 2) NULL,
    [CreatedAt]                  DATETIME2 (7)   NOT NULL,
    CONSTRAINT [PK_BettingRecommendations] PRIMARY KEY CLUSTERED ([RecommendationId] ASC),
    CONSTRAINT [FK_BettingRecommendations_Predictions_PredictionId] FOREIGN KEY ([PredictionId]) REFERENCES [dbo].[Predictions] ([PredictionId]) ON DELETE CASCADE,
    CONSTRAINT [FK_BettingRecommendations_Teams_RecommendedTeamId] FOREIGN KEY ([RecommendedTeamId]) REFERENCES [dbo].[Teams] ([TeamId])
);




GO
CREATE NONCLUSTERED INDEX [IX_BettingRecommendations_Edge_CreatedAt]
    ON [dbo].[BettingRecommendations]([Edge] ASC, [CreatedAt] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_BettingRecommendations_RecommendedTeamId]
    ON [dbo].[BettingRecommendations]([RecommendedTeamId] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_BettingRecommendations_PredictionId]
    ON [dbo].[BettingRecommendations]([PredictionId] ASC);

