CREATE TABLE [data].Teams (
    TeamId INT PRIMARY KEY IDENTITY(1,1),
    SportId INT FOREIGN KEY REFERENCES [data].Sports(SportId),
    ExternalTeamId NVARCHAR(50), -- API team ID
    Name NVARCHAR(100) NOT NULL,
    Abbreviation NVARCHAR(10)
);