using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentActivityManagement.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class Sprint11CheckInLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ActivityCheckIns_RegistrationId_SessionId",
                table: "ActivityCheckIns");

            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "ActivityCheckIns",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VoidReason",
                table: "ActivityCheckIns",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VoidedAt",
                table: "ActivityCheckIns",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VoidedById",
                table: "ActivityCheckIns",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ActivityCheckIns_RegistrationId_SessionId",
                table: "ActivityCheckIns",
                columns: new[] { "RegistrationId", "SessionId" },
                unique: true,
                filter: "\"VoidedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityCheckIns_VoidedById",
                table: "ActivityCheckIns",
                column: "VoidedById");

            migrationBuilder.AddForeignKey(
                name: "FK_ActivityCheckIns_Users_VoidedById",
                table: "ActivityCheckIns",
                column: "VoidedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ActivityCheckIns_Users_VoidedById",
                table: "ActivityCheckIns");

            migrationBuilder.DropIndex(
                name: "IX_ActivityCheckIns_RegistrationId_SessionId",
                table: "ActivityCheckIns");

            migrationBuilder.DropIndex(
                name: "IX_ActivityCheckIns_VoidedById",
                table: "ActivityCheckIns");

            migrationBuilder.DropColumn(
                name: "Note",
                table: "ActivityCheckIns");

            migrationBuilder.DropColumn(
                name: "VoidReason",
                table: "ActivityCheckIns");

            migrationBuilder.DropColumn(
                name: "VoidedAt",
                table: "ActivityCheckIns");

            migrationBuilder.DropColumn(
                name: "VoidedById",
                table: "ActivityCheckIns");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityCheckIns_RegistrationId_SessionId",
                table: "ActivityCheckIns",
                columns: new[] { "RegistrationId", "SessionId" },
                unique: true);
        }
    }
}
