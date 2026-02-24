using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Moneyball.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddingColumnsToModelPerformance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SampleSize",
                schema: "dbo",
                table: "ModelPerformances");

            migrationBuilder.RenameColumn(
                name: "Metrics",
                schema: "dbo",
                table: "ModelPerformances",
                newName: "FeatureImportanceJson");

            migrationBuilder.AlterColumn<decimal>(
                name: "Accuracy",
                schema: "dbo",
                table: "ModelPerformances",
                type: "decimal(5,4)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(5,4)",
                oldNullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AUC",
                schema: "dbo",
                table: "ModelPerformances",
                type: "decimal(5,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "F1Score",
                schema: "dbo",
                table: "ModelPerformances",
                type: "decimal(5,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LogLoss",
                schema: "dbo",
                table: "ModelPerformances",
                type: "decimal(8,6)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Precision",
                schema: "dbo",
                table: "ModelPerformances",
                type: "decimal(5,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Recall",
                schema: "dbo",
                table: "ModelPerformances",
                type: "decimal(5,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "TrainingSamples",
                schema: "dbo",
                table: "ModelPerformances",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ValidationSamples",
                schema: "dbo",
                table: "ModelPerformances",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AUC",
                schema: "dbo",
                table: "ModelPerformances");

            migrationBuilder.DropColumn(
                name: "F1Score",
                schema: "dbo",
                table: "ModelPerformances");

            migrationBuilder.DropColumn(
                name: "LogLoss",
                schema: "dbo",
                table: "ModelPerformances");

            migrationBuilder.DropColumn(
                name: "Precision",
                schema: "dbo",
                table: "ModelPerformances");

            migrationBuilder.DropColumn(
                name: "Recall",
                schema: "dbo",
                table: "ModelPerformances");

            migrationBuilder.DropColumn(
                name: "TrainingSamples",
                schema: "dbo",
                table: "ModelPerformances");

            migrationBuilder.DropColumn(
                name: "ValidationSamples",
                schema: "dbo",
                table: "ModelPerformances");

            migrationBuilder.RenameColumn(
                name: "FeatureImportanceJson",
                schema: "dbo",
                table: "ModelPerformances",
                newName: "Metrics");

            migrationBuilder.AlterColumn<decimal>(
                name: "Accuracy",
                schema: "dbo",
                table: "ModelPerformances",
                type: "decimal(5,4)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(5,4)");

            migrationBuilder.AddColumn<int>(
                name: "SampleSize",
                schema: "dbo",
                table: "ModelPerformances",
                type: "int",
                nullable: true);
        }
    }
}
