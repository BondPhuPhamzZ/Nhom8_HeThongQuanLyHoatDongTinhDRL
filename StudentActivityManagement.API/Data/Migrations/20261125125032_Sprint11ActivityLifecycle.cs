using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentActivityManagement.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class Sprint11ActivityLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CorrectionWindowMinutes",
                table: "ActivityCheckInConfigs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1440);

            migrationBuilder.AddColumn<DateTime>(
                name: "PointsFinalizedAt",
                table: "Activities",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PointsFinalizedById",
                table: "Activities",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Activities"
                SET "PointsFinalizedAt" = COALESCE("UpdatedAt", "EndTime")
                WHERE "Status" = 'Completed';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Activities_PointsFinalizedById",
                table: "Activities",
                column: "PointsFinalizedById");

            migrationBuilder.AddForeignKey(
                name: "FK_Activities_Users_PointsFinalizedById",
                table: "Activities",
                column: "PointsFinalizedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Activities_Users_PointsFinalizedById",
                table: "Activities");

            migrationBuilder.DropIndex(
                name: "IX_Activities_PointsFinalizedById",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "CorrectionWindowMinutes",
                table: "ActivityCheckInConfigs");

            migrationBuilder.DropColumn(
                name: "PointsFinalizedAt",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "PointsFinalizedById",
                table: "Activities");
        }
    }
}
