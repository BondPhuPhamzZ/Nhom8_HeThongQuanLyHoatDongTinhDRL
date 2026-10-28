using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentActivityManagement.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityRequirementsAndRestrictDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ActivityRegistrations_Activities_ActivityId",
                table: "ActivityRegistrations");

            migrationBuilder.AddColumn<string>(
                name: "Requirements",
                table: "Activities",
                type: "TEXT",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddForeignKey(
                name: "FK_ActivityRegistrations_Activities_ActivityId",
                table: "ActivityRegistrations",
                column: "ActivityId",
                principalTable: "Activities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ActivityRegistrations_Activities_ActivityId",
                table: "ActivityRegistrations");

            migrationBuilder.DropColumn(
                name: "Requirements",
                table: "Activities");

            migrationBuilder.AddForeignKey(
                name: "FK_ActivityRegistrations_Activities_ActivityId",
                table: "ActivityRegistrations",
                column: "ActivityId",
                principalTable: "Activities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
