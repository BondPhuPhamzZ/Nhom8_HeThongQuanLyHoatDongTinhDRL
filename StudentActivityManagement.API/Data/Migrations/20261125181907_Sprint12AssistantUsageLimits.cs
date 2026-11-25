using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentActivityManagement.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class Sprint12AssistantUsageLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AssistantDailyUsages_StudentId_UsageDate",
                table: "AssistantDailyUsages");

            migrationBuilder.AddColumn<string>(
                name: "Feature",
                table: "AssistantDailyUsages",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "Assistant");

            migrationBuilder.AddColumn<int>(
                name: "TokenCount",
                table: "AssistantDailyUsages",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_AssistantDailyUsages_StudentId_UsageDate_Feature",
                table: "AssistantDailyUsages",
                columns: new[] { "StudentId", "UsageDate", "Feature" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssistantDailyUsages_UsageDate",
                table: "AssistantDailyUsages",
                column: "UsageDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AssistantDailyUsages_StudentId_UsageDate_Feature",
                table: "AssistantDailyUsages");

            migrationBuilder.DropIndex(
                name: "IX_AssistantDailyUsages_UsageDate",
                table: "AssistantDailyUsages");

            migrationBuilder.DropColumn(
                name: "Feature",
                table: "AssistantDailyUsages");

            migrationBuilder.DropColumn(
                name: "TokenCount",
                table: "AssistantDailyUsages");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantDailyUsages_StudentId_UsageDate",
                table: "AssistantDailyUsages",
                columns: new[] { "StudentId", "UsageDate" },
                unique: true);
        }
    }
}
