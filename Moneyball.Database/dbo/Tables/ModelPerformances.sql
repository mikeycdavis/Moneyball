CREATE TABLE [dbo].[ModelPerformances] (
    [PerformanceId]  INT             IDENTITY (1, 1) NOT NULL,
    [ModelId]        INT             NOT NULL,
    [EvaluationDate] DATETIME2 (7)   DEFAULT (getutcdate()) NULL,
    [Accuracy]       DECIMAL (5, 4)  NULL,
    [ROI]            DECIMAL (10, 4) NULL,
    [SampleSize]     INT             NULL,
    [Metrics]        NVARCHAR (MAX)  NULL,
    PRIMARY KEY CLUSTERED ([PerformanceId] ASC),
    FOREIGN KEY ([ModelId]) REFERENCES [dbo].[Models] ([ModelId])
);

