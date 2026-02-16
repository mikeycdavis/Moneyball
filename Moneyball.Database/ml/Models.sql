-- ML Model Management
CREATE TABLE ml.Models (
    ModelId INT PRIMARY KEY IDENTITY(1,1),
    Name NVARCHAR(100) NOT NULL,
    Version NVARCHAR(50) NOT NULL,
    SportId INT FOREIGN KEY REFERENCES [data].Sports(SportId),
    ModelType NVARCHAR(50), -- 'Python', 'ML.NET', etc.
    FilePath NVARCHAR(500), -- or endpoint URL for Python service
    IsActive BIT DEFAULT 1,
    CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
    Metadata NVARCHAR(MAX), -- JSON for hyperparameters, features, etc.
    CONSTRAINT UQ_Model_Name_Version UNIQUE (Name, Version)
);