CREATE TABLE [dbo].[Sports] (
    [SportId]  INT           IDENTITY (1, 1) NOT NULL,
    [Name]     NVARCHAR (50) NOT NULL,
    [IsActive] BIT           NOT NULL,
    CONSTRAINT [PK_Sports] PRIMARY KEY CLUSTERED ([SportId] ASC)
);


GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_Sports_Name]
    ON [dbo].[Sports]([Name] ASC);

