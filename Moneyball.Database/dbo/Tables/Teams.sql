CREATE TABLE [dbo].[Teams] (
    [TeamId]       INT            IDENTITY (1, 1) NOT NULL,
    [SportId]      INT            NOT NULL,
    [ExternalId]   NVARCHAR (50)  NULL,
    [Name]         NVARCHAR (100) NOT NULL,
    [Abbreviation] NVARCHAR (10)  NULL,
    [City]         NVARCHAR (100) NULL,
    [Conference]   NVARCHAR (100) NULL,
    [Division]     NVARCHAR (100) NULL,
    PRIMARY KEY CLUSTERED ([TeamId] ASC),
    FOREIGN KEY ([SportId]) REFERENCES [dbo].[Sports] ([SportId])
);


GO
CREATE NONCLUSTERED INDEX [IX_Teams_Name]
    ON [dbo].[Teams]([Name] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_Teams_SportId_ExternalId]
    ON [dbo].[Teams]([SportId] ASC, [ExternalId] ASC);

