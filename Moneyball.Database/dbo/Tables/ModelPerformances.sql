CREATE TABLE [dbo].[ModelPerformances] (
    [PerformanceId]         INT             IDENTITY (1, 1) NOT NULL,
    [ModelId]               INT             NOT NULL,
    [EvaluationDate]        DATETIME2 (7)   NOT NULL,
    [Accuracy]              DECIMAL (5, 4)  DEFAULT ((0.0)) NOT NULL,
    [ROI]                   DECIMAL (10, 4) NULL,
    [FeatureImportanceJson] NVARCHAR (MAX)  NULL,
    [AUC]                   DECIMAL (5, 4)  DEFAULT ((0.0)) NOT NULL,
    [F1Score]               DECIMAL (5, 4)  DEFAULT ((0.0)) NOT NULL,
    [LogLoss]               DECIMAL (8, 6)  DEFAULT ((0.0)) NOT NULL,
    [Precision]             DECIMAL (5, 4)  DEFAULT ((0.0)) NOT NULL,
    [Recall]                DECIMAL (5, 4)  DEFAULT ((0.0)) NOT NULL,
    [TrainingSamples]       INT             DEFAULT ((0)) NOT NULL,
    [ValidationSamples]     INT             DEFAULT ((0)) NOT NULL,
    CONSTRAINT [PK_ModelPerformances] PRIMARY KEY CLUSTERED ([PerformanceId] ASC),
    CONSTRAINT [FK_ModelPerformances_Models_ModelId] FOREIGN KEY ([ModelId]) REFERENCES [dbo].[Models] ([ModelId]) ON DELETE CASCADE
);


GO
CREATE NONCLUSTERED INDEX [IX_ModelPerformances_ModelId]
    ON [dbo].[ModelPerformances]([ModelId] ASC);

