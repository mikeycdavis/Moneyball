CREATE TABLE [dbo].[Games] (
    [GameId]         INT            IDENTITY (1, 1) NOT NULL,
    [SportId]        INT            NOT NULL,
    [ExternalGameId] NVARCHAR (50)  NULL,
    [HomeTeamId]     INT            NOT NULL,
    [AwayTeamId]     INT            NOT NULL,
    [GameDate]       DATETIME2 (7)  NOT NULL,
    [HomeScore]      INT            NULL,
    [AwayScore]      INT            NULL,
    [Status]         NVARCHAR (450) NOT NULL,
    [IsComplete]     BIT            NOT NULL,
    [Season]         NVARCHAR (50)  NULL,
    [Week]           INT            NULL,
    [CreatedAt]      DATETIME2 (7)  NOT NULL,
    [UpdatedAt]      DATETIME2 (7)  NULL,
    CONSTRAINT [PK_Games] PRIMARY KEY CLUSTERED ([GameId] ASC),
    CONSTRAINT [FK_Games_Sports_SportId] FOREIGN KEY ([SportId]) REFERENCES [dbo].[Sports] ([SportId]) ON DELETE CASCADE,
    CONSTRAINT [FK_Games_Teams_AwayTeamId] FOREIGN KEY ([AwayTeamId]) REFERENCES [dbo].[Teams] ([TeamId]),
    CONSTRAINT [FK_Games_Teams_HomeTeamId] FOREIGN KEY ([HomeTeamId]) REFERENCES [dbo].[Teams] ([TeamId])
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


GO
CREATE NONCLUSTERED INDEX [IX_Games_HomeTeamId]
    ON [dbo].[Games]([HomeTeamId] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_Games_AwayTeamId]
    ON [dbo].[Games]([AwayTeamId] ASC);

