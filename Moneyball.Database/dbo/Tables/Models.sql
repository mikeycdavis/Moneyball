CREATE TABLE [dbo].[Models] (
    [ModelId]     INT             IDENTITY (1, 1) NOT NULL,
    [Name]        NVARCHAR (100)  NOT NULL,
    [Version]     NVARCHAR (50)   NOT NULL,
    [SportId]     INT             NOT NULL,
    [FilePath]    NVARCHAR (500)  NULL,
    [IsActive]    BIT             NOT NULL,
    [CreatedAt]   DATETIME2 (7)   NOT NULL,
    [Metadata]    NVARCHAR (MAX)  NULL,
    [Description] NVARCHAR (1000) NULL,
    [TrainedAt]   DATETIME2 (7)   DEFAULT ('0001-01-01T00:00:00.0000000') NOT NULL,
    [TrainedBy]   NVARCHAR (MAX)  DEFAULT (N'') NOT NULL,
    [Type]        NVARCHAR (MAX)  DEFAULT (N'') NOT NULL,
    [UpdatedAt]   DATETIME2 (7)   DEFAULT ('0001-01-01T00:00:00.0000000') NOT NULL,
    CONSTRAINT [PK_Models] PRIMARY KEY CLUSTERED ([ModelId] ASC),
    CONSTRAINT [FK_Models_Sports_SportId] FOREIGN KEY ([SportId]) REFERENCES [dbo].[Sports] ([SportId]) ON DELETE CASCADE
);




GO
CREATE NONCLUSTERED INDEX [IX_Models_SportId_IsActive]
    ON [dbo].[Models]([SportId] ASC, [IsActive] ASC);


GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_Models_Name_Version]
    ON [dbo].[Models]([Name] ASC, [Version] ASC);

