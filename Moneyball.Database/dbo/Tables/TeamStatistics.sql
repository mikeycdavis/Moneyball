CREATE TABLE [dbo].[TeamStatistics] (
    [TeamStatisticId]      INT            IDENTITY (1, 1) NOT NULL,
    [GameId]               INT            NOT NULL,
    [TeamId]               INT            NOT NULL,
    [IsHomeTeam]           BIT            NOT NULL,
    [Points]               INT            NULL,
    [FieldGoalsMade]       INT            NULL,
    [FieldGoalsAttempted]  INT            NULL,
    [FieldGoalPercentage]  DECIMAL (5, 4) NULL,
    [ThreePointsMade]      INT            NULL,
    [ThreePointsAttempted] INT            NULL,
    [ThreePointPercentage] DECIMAL (5, 4) NULL,
    [FreeThrowsMade]       INT            NULL,
    [FreeThrowsAttempted]  INT            NULL,
    [FreeThrowPercentage]  DECIMAL (5, 4) NULL,
    [Rebounds]             INT            NULL,
    [OffensiveRebounds]    INT            NULL,
    [DefensiveRebounds]    INT            NULL,
    [Assists]              INT            NULL,
    [Steals]               INT            NULL,
    [Blocks]               INT            NULL,
    [Turnovers]            INT            NULL,
    [PersonalFouls]        INT            NULL,
    [PassingYards]         INT            NULL,
    [RushingYards]         INT            NULL,
    [TotalYards]           INT            NULL,
    [Touchdowns]           INT            NULL,
    [Interceptions]        INT            NULL,
    [Fumbles]              INT            NULL,
    [Sacks]                INT            NULL,
    [TimeOfPossession]     DECIMAL (5, 2) NULL,
    [AdditionalStats]      NVARCHAR (MAX) NULL,
    [CreatedAt]            DATETIME2 (7)  DEFAULT (getutcdate()) NULL,
    PRIMARY KEY CLUSTERED ([TeamStatisticId] ASC),
    FOREIGN KEY ([GameId]) REFERENCES [dbo].[Games] ([GameId]),
    FOREIGN KEY ([TeamId]) REFERENCES [dbo].[Teams] ([TeamId])
);


GO
CREATE NONCLUSTERED INDEX [IX_TeamStatistics_GameId_TeamId]
    ON [dbo].[TeamStatistics]([GameId] ASC, [TeamId] ASC);

