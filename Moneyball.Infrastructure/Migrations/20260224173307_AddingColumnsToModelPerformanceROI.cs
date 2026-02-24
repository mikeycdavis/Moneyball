using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Moneyball.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddingColumnsToModelPerformanceROI : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "ROI",
                schema: "dbo",
                table: "ModelPerformances",
                type: "decimal(10,4)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,4)",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "ROI",
                schema: "dbo",
                table: "ModelPerformances",
                type: "decimal(10,4)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,4)");
        }
    }
}
