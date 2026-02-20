using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Moneyball.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.CreateTable(
                name: "Sports",
                schema: "dbo",
                columns: table => new
                {
                    SportId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sports", x => x.SportId);
                });

            migrationBuilder.CreateTable(
                name: "Models",
                schema: "dbo",
                columns: table => new
                {
                    ModelId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SportId = table.Column<int>(type: "int", nullable: false),
                    ModelType = table.Column<int>(type: "int", nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Models", x => x.ModelId);
                    table.ForeignKey(
                        name: "FK_Models_Sports_SportId",
                        column: x => x.SportId,
                        principalSchema: "dbo",
                        principalTable: "Sports",
                        principalColumn: "SportId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Teams",
                schema: "dbo",
                columns: table => new
                {
                    TeamId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SportId = table.Column<int>(type: "int", nullable: false),
                    ExternalId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Abbreviation = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Conference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Division = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teams", x => x.TeamId);
                    table.ForeignKey(
                        name: "FK_Teams_Sports_SportId",
                        column: x => x.SportId,
                        principalSchema: "dbo",
                        principalTable: "Sports",
                        principalColumn: "SportId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModelPerformances",
                schema: "dbo",
                columns: table => new
                {
                    PerformanceId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ModelId = table.Column<int>(type: "int", nullable: false),
                    EvaluationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Accuracy = table.Column<decimal>(type: "decimal(5,4)", nullable: true),
                    ROI = table.Column<decimal>(type: "decimal(10,4)", nullable: true),
                    SampleSize = table.Column<int>(type: "int", nullable: true),
                    Metrics = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelPerformances", x => x.PerformanceId);
                    table.ForeignKey(
                        name: "FK_ModelPerformances_Models_ModelId",
                        column: x => x.ModelId,
                        principalSchema: "dbo",
                        principalTable: "Models",
                        principalColumn: "ModelId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Games",
                schema: "dbo",
                columns: table => new
                {
                    GameId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SportId = table.Column<int>(type: "int", nullable: false),
                    ExternalGameId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    HomeTeamId = table.Column<int>(type: "int", nullable: false),
                    AwayTeamId = table.Column<int>(type: "int", nullable: false),
                    GameDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HomeScore = table.Column<int>(type: "int", nullable: true),
                    AwayScore = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    IsComplete = table.Column<bool>(type: "bit", nullable: false),
                    Season = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Week = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Games", x => x.GameId);
                    table.ForeignKey(
                        name: "FK_Games_Sports_SportId",
                        column: x => x.SportId,
                        principalSchema: "dbo",
                        principalTable: "Sports",
                        principalColumn: "SportId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Games_Teams_AwayTeamId",
                        column: x => x.AwayTeamId,
                        principalSchema: "dbo",
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Games_Teams_HomeTeamId",
                        column: x => x.HomeTeamId,
                        principalSchema: "dbo",
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GameOdds",
                schema: "dbo",
                columns: table => new
                {
                    OddsId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    BookmakerName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    HomeMoneyline = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    AwayMoneyline = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    HomeSpread = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    AwaySpread = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    HomeSpreadOdds = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    AwaySpreadOdds = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    OverUnder = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    OverOdds = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    UnderOdds = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameOdds", x => x.OddsId);
                    table.ForeignKey(
                        name: "FK_GameOdds_Games_GameId",
                        column: x => x.GameId,
                        principalSchema: "dbo",
                        principalTable: "Games",
                        principalColumn: "GameId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Predictions",
                schema: "dbo",
                columns: table => new
                {
                    PredictionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ModelId = table.Column<int>(type: "int", nullable: false),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    PredictedHomeWinProbability = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    PredictedAwayWinProbability = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    Edge = table.Column<decimal>(type: "decimal(10,4)", nullable: true),
                    Confidence = table.Column<decimal>(type: "decimal(5,4)", nullable: true),
                    PredictedHomeScore = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    PredictedAwayScore = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    PredictedTotal = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    FeatureValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Predictions", x => x.PredictionId);
                    table.ForeignKey(
                        name: "FK_Predictions_Games_GameId",
                        column: x => x.GameId,
                        principalSchema: "dbo",
                        principalTable: "Games",
                        principalColumn: "GameId");
                    table.ForeignKey(
                        name: "FK_Predictions_Models_ModelId",
                        column: x => x.ModelId,
                        principalSchema: "dbo",
                        principalTable: "Models",
                        principalColumn: "ModelId");
                });

            migrationBuilder.CreateTable(
                name: "TeamStatistics",
                schema: "dbo",
                columns: table => new
                {
                    TeamStatisticId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    TeamId = table.Column<int>(type: "int", nullable: false),
                    IsHomeTeam = table.Column<bool>(type: "bit", nullable: false),
                    Points = table.Column<int>(type: "int", nullable: true),
                    FieldGoalsMade = table.Column<int>(type: "int", nullable: true),
                    FieldGoalsAttempted = table.Column<int>(type: "int", nullable: true),
                    FieldGoalPercentage = table.Column<decimal>(type: "decimal(5,4)", nullable: true),
                    ThreePointsMade = table.Column<int>(type: "int", nullable: true),
                    ThreePointsAttempted = table.Column<int>(type: "int", nullable: true),
                    ThreePointPercentage = table.Column<decimal>(type: "decimal(5,4)", nullable: true),
                    FreeThrowsMade = table.Column<int>(type: "int", nullable: true),
                    FreeThrowsAttempted = table.Column<int>(type: "int", nullable: true),
                    FreeThrowPercentage = table.Column<decimal>(type: "decimal(5,4)", nullable: true),
                    Rebounds = table.Column<int>(type: "int", nullable: true),
                    OffensiveRebounds = table.Column<int>(type: "int", nullable: true),
                    DefensiveRebounds = table.Column<int>(type: "int", nullable: true),
                    Assists = table.Column<int>(type: "int", nullable: true),
                    Steals = table.Column<int>(type: "int", nullable: true),
                    Blocks = table.Column<int>(type: "int", nullable: true),
                    Turnovers = table.Column<int>(type: "int", nullable: true),
                    PersonalFouls = table.Column<int>(type: "int", nullable: true),
                    PassingYards = table.Column<int>(type: "int", nullable: true),
                    RushingYards = table.Column<int>(type: "int", nullable: true),
                    TotalYards = table.Column<int>(type: "int", nullable: true),
                    Touchdowns = table.Column<int>(type: "int", nullable: true),
                    Interceptions = table.Column<int>(type: "int", nullable: true),
                    Fumbles = table.Column<int>(type: "int", nullable: true),
                    Sacks = table.Column<int>(type: "int", nullable: true),
                    TimeOfPossession = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    AdditionalStats = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamStatistics", x => x.TeamStatisticId);
                    table.ForeignKey(
                        name: "FK_TeamStatistics_Games_GameId",
                        column: x => x.GameId,
                        principalSchema: "dbo",
                        principalTable: "Games",
                        principalColumn: "GameId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TeamStatistics_Teams_TeamId",
                        column: x => x.TeamId,
                        principalSchema: "dbo",
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BettingRecommendations",
                schema: "dbo",
                columns: table => new
                {
                    RecommendationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PredictionId = table.Column<int>(type: "int", nullable: false),
                    RecommendedBetType = table.Column<int>(type: "int", nullable: false),
                    RecommendedTeamId = table.Column<int>(type: "int", nullable: true),
                    Edge = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
                    KellyFraction = table.Column<decimal>(type: "decimal(5,4)", nullable: true),
                    RecommendedStakePercentage = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    MinBankroll = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BettingRecommendations", x => x.RecommendationId);
                    table.ForeignKey(
                        name: "FK_BettingRecommendations_Predictions_PredictionId",
                        column: x => x.PredictionId,
                        principalSchema: "dbo",
                        principalTable: "Predictions",
                        principalColumn: "PredictionId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BettingRecommendations_Teams_RecommendedTeamId",
                        column: x => x.RecommendedTeamId,
                        principalSchema: "dbo",
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "dbo",
                table: "Sports",
                columns: new[] { "SportId", "IsActive", "Name" },
                values: new object[,]
                {
                    { 1, true, "NBA" },
                    { 2, true, "NFL" },
                    { 3, true, "NHL" },
                    { 4, true, "MLB" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_BettingRecommendations_Edge_CreatedAt",
                schema: "dbo",
                table: "BettingRecommendations",
                columns: new[] { "Edge", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BettingRecommendations_PredictionId",
                schema: "dbo",
                table: "BettingRecommendations",
                column: "PredictionId");

            migrationBuilder.CreateIndex(
                name: "IX_BettingRecommendations_RecommendedTeamId",
                schema: "dbo",
                table: "BettingRecommendations",
                column: "RecommendedTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_GameOdds_BookmakerName",
                schema: "dbo",
                table: "GameOdds",
                column: "BookmakerName");

            migrationBuilder.CreateIndex(
                name: "IX_GameOdds_GameId_RecordedAt",
                schema: "dbo",
                table: "GameOdds",
                columns: new[] { "GameId", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Games_AwayTeamId",
                schema: "dbo",
                table: "Games",
                column: "AwayTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Games_ExternalGameId",
                schema: "dbo",
                table: "Games",
                column: "ExternalGameId");

            migrationBuilder.CreateIndex(
                name: "IX_Games_GameDate",
                schema: "dbo",
                table: "Games",
                column: "GameDate");

            migrationBuilder.CreateIndex(
                name: "IX_Games_HomeTeamId",
                schema: "dbo",
                table: "Games",
                column: "HomeTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Games_SportId_GameDate",
                schema: "dbo",
                table: "Games",
                columns: new[] { "SportId", "GameDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Games_Status_GameDate",
                schema: "dbo",
                table: "Games",
                columns: new[] { "Status", "GameDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelPerformances_ModelId",
                schema: "dbo",
                table: "ModelPerformances",
                column: "ModelId");

            migrationBuilder.CreateIndex(
                name: "IX_Models_Name_Version",
                schema: "dbo",
                table: "Models",
                columns: new[] { "Name", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Models_SportId_IsActive",
                schema: "dbo",
                table: "Models",
                columns: new[] { "SportId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Predictions_CreatedAt",
                schema: "dbo",
                table: "Predictions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Predictions_GameId_ModelId",
                schema: "dbo",
                table: "Predictions",
                columns: new[] { "GameId", "ModelId" });

            migrationBuilder.CreateIndex(
                name: "IX_Predictions_ModelId_CreatedAt",
                schema: "dbo",
                table: "Predictions",
                columns: new[] { "ModelId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Sports_Name",
                schema: "dbo",
                table: "Sports",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Teams_Name",
                schema: "dbo",
                table: "Teams",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_SportId_ExternalId",
                schema: "dbo",
                table: "Teams",
                columns: new[] { "SportId", "ExternalId" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamStatistics_GameId_TeamId",
                schema: "dbo",
                table: "TeamStatistics",
                columns: new[] { "GameId", "TeamId" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamStatistics_TeamId",
                schema: "dbo",
                table: "TeamStatistics",
                column: "TeamId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BettingRecommendations",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "GameOdds",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ModelPerformances",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TeamStatistics",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Predictions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Games",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Models",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Teams",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Sports",
                schema: "dbo");
        }
    }
}
