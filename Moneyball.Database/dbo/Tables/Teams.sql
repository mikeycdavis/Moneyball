CREATE TABLE [dbo].[Teams] (
    [TeamId]       INT            IDENTITY (1, 1) NOT NULL,
    [SportId]      INT            NOT NULL,
    [ExternalId]   NVARCHAR (50)  NULL,
    [Name]         NVARCHAR (100) NOT NULL,
    [Abbreviation] NVARCHAR (10)  NULL,
    [City]         NVARCHAR (100) NULL,
    [Conference]   NVARCHAR (100) NULL,
    [Division]     NVARCHAR (100) NULL,
    CONSTRAINT [PK_Teams] PRIMARY KEY CLUSTERED ([TeamId] ASC),
    CONSTRAINT [FK_Teams_Sports_SportId] FOREIGN KEY ([SportId]) REFERENCES [dbo].[Sports] ([SportId]) ON DELETE CASCADE
);




GO
CREATE NONCLUSTERED INDEX [IX_Teams_Name]
    ON [dbo].[Teams]([Name] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_Teams_SportId_ExternalId]
    ON [dbo].[Teams]([SportId] ASC, [ExternalId] ASC);

