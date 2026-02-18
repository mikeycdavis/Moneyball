CREATE TABLE [dbo].[Models] (
    [ModelId]     INT             IDENTITY (1, 1) NOT NULL,
    [Name]        NVARCHAR (100)  NOT NULL,
    [Version]     NVARCHAR (50)   NOT NULL,
    [SportId]     INT             NOT NULL,
    [ModelType]   INT             NOT NULL,
    [FilePath]    NVARCHAR (500)  NULL,
    [IsActive]    BIT             DEFAULT ((1)) NULL,
    [CreatedAt]   DATETIME2 (7)   DEFAULT (getutcdate()) NULL,
    [Metadata]    NVARCHAR (MAX)  NULL,
    [Description] NVARCHAR (1000) NULL,
    PRIMARY KEY CLUSTERED ([ModelId] ASC),
    FOREIGN KEY ([SportId]) REFERENCES [dbo].[Sports] ([SportId]),
    CONSTRAINT [UQ_Model_Name_Version] UNIQUE NONCLUSTERED ([Name] ASC, [Version] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_Models_SportId_IsActive]
    ON [dbo].[Models]([SportId] ASC, [IsActive] ASC);

