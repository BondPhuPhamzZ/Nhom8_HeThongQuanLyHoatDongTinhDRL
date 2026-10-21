using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentActivityManagement.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityStructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OrganizerName",
                table: "Activities",
                type: "TEXT",
                maxLength: 150,
                nullable: false,
                defaultValue: "Phòng Quản lý HSSV");

            migrationBuilder.CreateTable(
                name: "ActivityAudienceRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ActivityId = table.Column<int>(type: "INTEGER", nullable: false),
                    Campus = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    AcademicYear = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    ClassId = table.Column<int>(type: "INTEGER", nullable: true),
                    ParticipationType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityAudienceRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActivityAudienceRules_Activities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "Activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ActivityAudienceRules_Classes_ClassId",
                        column: x => x.ClassId,
                        principalTable: "Classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ActivitySessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ActivityId = table.Column<int>(type: "INTEGER", nullable: false),
                    SessionName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Campus = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Location = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    StartTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivitySessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActivitySessions_Activities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "Activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Preserve Sprint 1 activities by converting their single schedule into session #1.
            migrationBuilder.Sql(
                """
                INSERT INTO ActivitySessions
                    (ActivityId, SessionName, Campus, Location, StartTime, EndTime, SortOrder)
                SELECT Id, 'Buổi chính', Campus, Location, StartTime, EndTime, 1
                FROM Activities;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ActivityAudienceRules_ActivityId",
                table: "ActivityAudienceRules",
                column: "ActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityAudienceRules_ClassId",
                table: "ActivityAudienceRules",
                column: "ClassId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivitySessions_ActivityId_SortOrder",
                table: "ActivitySessions",
                columns: new[] { "ActivityId", "SortOrder" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityAudienceRules");

            migrationBuilder.DropTable(
                name: "ActivitySessions");

            migrationBuilder.DropColumn(
                name: "OrganizerName",
                table: "Activities");
        }
    }
}
