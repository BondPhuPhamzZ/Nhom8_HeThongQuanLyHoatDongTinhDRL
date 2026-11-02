using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentActivityManagement.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class Sprint5MonitorRelay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RelayedAt",
                table: "ThreadEscalations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RelayedById",
                table: "ThreadEscalations",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ThreadEscalations_RelayedById",
                table: "ThreadEscalations",
                column: "RelayedById");

            migrationBuilder.AddForeignKey(
                name: "FK_ThreadEscalations_Users_RelayedById",
                table: "ThreadEscalations",
                column: "RelayedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ThreadEscalations_Users_RelayedById",
                table: "ThreadEscalations");

            migrationBuilder.DropIndex(
                name: "IX_ThreadEscalations_RelayedById",
                table: "ThreadEscalations");

            migrationBuilder.DropColumn(
                name: "RelayedAt",
                table: "ThreadEscalations");

            migrationBuilder.DropColumn(
                name: "RelayedById",
                table: "ThreadEscalations");
        }
    }
}
