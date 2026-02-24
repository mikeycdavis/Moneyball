CREATE TABLE [dbo].[GameOdds] (
    [OddsId]         INT             IDENTITY (1, 1) NOT NULL,
    [GameId]         INT             NOT NULL,
    [BookmakerName]  NVARCHAR (100)  NOT NULL,
    [HomeMoneyline]  DECIMAL (10, 2) NULL,
    [AwayMoneyline]  DECIMAL (10, 2) NULL,
    [HomeSpread]     DECIMAL (5, 2)  NULL,
    [AwaySpread]     DECIMAL (5, 2)  NULL,
    [HomeSpreadOdds] DECIMAL (10, 2) NULL,
    [AwaySpreadOdds] DECIMAL (10, 2) NULL,
    [OverUnder]      DECIMAL (5, 2)  NULL,
    [OverOdds]       DECIMAL (10, 2) NULL,
    [UnderOdds]      DECIMAL (10, 2) NULL,
    [RecordedAt]     DATETIME2 (7)   NOT NULL,
    CONSTRAINT [PK_GameOdds] PRIMARY KEY CLUSTERED ([OddsId] ASC),
    CONSTRAINT [FK_GameOdds_Games_GameId] FOREIGN KEY ([GameId]) REFERENCES [dbo].[Games] ([GameId]) ON DELETE CASCADE
);




GO
CREATE NONCLUSTERED INDEX [IX_GameOdds_BookmakerName]
    ON [dbo].[GameOdds]([BookmakerName] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_GameOdds_GameId_RecordedAt]
    ON [dbo].[GameOdds]([GameId] ASC, [RecordedAt] ASC);

