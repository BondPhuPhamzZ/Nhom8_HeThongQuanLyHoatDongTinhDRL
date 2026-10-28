using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentActivityManagement.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class Sprint3CheckInAndEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CheckInCode",
                table: "ActivitySessions",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CheckInCodeGeneratedAt",
                table: "ActivitySessions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ActivityCheckInConfigs",
                columns: table => new
                {
                    ActivityId = table.Column<int>(type: "INTEGER", nullable: false),
                    Method = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    OpenBeforeMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    CloseAfterMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedById = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityCheckInConfigs", x => x.ActivityId);
                    table.ForeignKey(
                        name: "FK_ActivityCheckInConfigs_Activities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "Activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ActivityCheckInConfigs_Users_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ActivityCheckIns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RegistrationId = table.Column<int>(type: "INTEGER", nullable: false),
                    SessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    CheckedInAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Method = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    RecordedById = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityCheckIns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActivityCheckIns_ActivityRegistrations_RegistrationId",
                        column: x => x.RegistrationId,
                        principalTable: "ActivityRegistrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ActivityCheckIns_ActivitySessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "ActivitySessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActivityCheckIns_Users_RecordedById",
                        column: x => x.RecordedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ActivityEvidences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RegistrationId = table.Column<int>(type: "INTEGER", nullable: false),
                    SessionId = table.Column<int>(type: "INTEGER", nullable: true),
                    EvidenceType = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    ConfirmationCode = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Note = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    OriginalFileName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    StoredFileName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    SizeBytes = table.Column<long>(type: "INTEGER", nullable: true),
                    ReviewStatus = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    ReviewNote = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ReviewedById = table.Column<int>(type: "INTEGER", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityEvidences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActivityEvidences_ActivityRegistrations_RegistrationId",
                        column: x => x.RegistrationId,
                        principalTable: "ActivityRegistrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ActivityEvidences_ActivitySessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "ActivitySessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActivityEvidences_Users_ReviewedById",
                        column: x => x.ReviewedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ActivityReminderOffsets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ActivityId = table.Column<int>(type: "INTEGER", nullable: false),
                    OffsetMinutes = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityReminderOffsets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActivityReminderOffsets_Activities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "Activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityCheckInConfigs_UpdatedById",
                table: "ActivityCheckInConfigs",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityCheckIns_RecordedById",
                table: "ActivityCheckIns",
                column: "RecordedById");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityCheckIns_RegistrationId_SessionId",
                table: "ActivityCheckIns",
                columns: new[] { "RegistrationId", "SessionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ActivityCheckIns_SessionId",
                table: "ActivityCheckIns",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityEvidences_RegistrationId",
                table: "ActivityEvidences",
                column: "RegistrationId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityEvidences_ReviewedById",
                table: "ActivityEvidences",
                column: "ReviewedById");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityEvidences_ReviewStatus",
                table: "ActivityEvidences",
                column: "ReviewStatus");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityEvidences_SessionId",
                table: "ActivityEvidences",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityReminderOffsets_ActivityId_OffsetMinutes",
                table: "ActivityReminderOffsets",
                columns: new[] { "ActivityId", "OffsetMinutes" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityCheckInConfigs");

            migrationBuilder.DropTable(
                name: "ActivityCheckIns");

            migrationBuilder.DropTable(
                name: "ActivityEvidences");

            migrationBuilder.DropTable(
                name: "ActivityReminderOffsets");

            migrationBuilder.DropColumn(
                name: "CheckInCode",
                table: "ActivitySessions");

            migrationBuilder.DropColumn(
                name: "CheckInCodeGeneratedAt",
                table: "ActivitySessions");
        }
    }
}
