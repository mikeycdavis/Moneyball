CREATE TABLE [dbo].[Games] (
    [GameId]         INT           IDENTITY (1, 1) NOT NULL,
    [SportId]        INT           NOT NULL,
    [ExternalGameId] NVARCHAR (50) NULL,
    [HomeTeamId]     INT           NOT NULL,
    [AwayTeamId]     INT           NOT NULL,
    [GameDate]       DATETIME2 (7) NOT NULL,
    [HomeScore]      INT           NULL,
    [AwayScore]      INT           NULL,
    [Status]         INT           DEFAULT ((0)) NOT NULL,
    [IsComplete]     BIT           DEFAULT ((0)) NULL,
    [Season]         NVARCHAR (50) NULL,
    [Week]           INT           NULL,
    [CreatedAt]      DATETIME2 (7) DEFAULT (getutcdate()) NULL,
    [UpdatedAt]      DATETIME2 (7) NULL,
    PRIMARY KEY CLUSTERED ([GameId] ASC),
    FOREIGN KEY ([AwayTeamId]) REFERENCES [dbo].[Teams] ([TeamId]),
    FOREIGN KEY ([HomeTeamId]) REFERENCES [dbo].[Teams] ([TeamId]),
    FOREIGN KEY ([SportId]) REFERENCES [dbo].[Sports] ([SportId])
);


GO
CREATE NONCLUSTERED INDEX [IX_Games_Status_GameDate]
    ON [dbo].[Games]([Status] ASC, [GameDate] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_Games_ExternalGameId]
    ON [dbo].[Games]([ExternalGameId] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_Games_SportId_GameDate]
    ON [dbo].[Games]([SportId] ASC, [GameDate] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_Games_GameDate]
    ON [dbo].[Games]([GameDate] ASC);

