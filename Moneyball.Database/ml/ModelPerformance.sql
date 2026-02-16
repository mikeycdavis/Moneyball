CREATE TABLE ml.ModelPerformance (
    PerformanceId INT PRIMARY KEY IDENTITY(1,1),
    ModelId INT FOREIGN KEY REFERENCES ml.Models(ModelId),
    EvaluationDate DATETIME2 DEFAULT GETUTCDATE(),
    Accuracy DECIMAL(5,4),
    ROI DECIMAL(10,4),
    SampleSize INT,
    Metrics NVARCHAR(MAX) -- JSON for precision, recall, F1, etc.
);