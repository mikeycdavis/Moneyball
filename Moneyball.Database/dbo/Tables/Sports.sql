CREATE TABLE [dbo].[Sports] (
    [SportId]  INT           IDENTITY (1, 1) NOT NULL,
    [Name]     NVARCHAR (50) NOT NULL,
    [IsActive] BIT           DEFAULT ((1)) NULL,
    PRIMARY KEY CLUSTERED ([SportId] ASC),
    UNIQUE NONCLUSTERED ([Name] ASC)
);

