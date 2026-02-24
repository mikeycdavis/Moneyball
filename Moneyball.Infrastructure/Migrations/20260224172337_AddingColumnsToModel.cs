using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Moneyball.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddingColumnsToModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ModelType",
                schema: "dbo",
                table: "Models");

            migrationBuilder.AddColumn<DateTime>(
                name: "TrainedAt",
                schema: "dbo",
                table: "Models",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "TrainedBy",
                schema: "dbo",
                table: "Models",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Type",
                schema: "dbo",
                table: "Models",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                schema: "dbo",
                table: "Models",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "dbo",
                table: "Games",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TrainedAt",
                schema: "dbo",
                table: "Models");

            migrationBuilder.DropColumn(
                name: "TrainedBy",
                schema: "dbo",
                table: "Models");

            migrationBuilder.DropColumn(
                name: "Type",
                schema: "dbo",
                table: "Models");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                schema: "dbo",
                table: "Models");

            migrationBuilder.AddColumn<int>(
                name: "ModelType",
                schema: "dbo",
                table: "Models",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                schema: "dbo",
                table: "Games",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");
        }
    }
}
