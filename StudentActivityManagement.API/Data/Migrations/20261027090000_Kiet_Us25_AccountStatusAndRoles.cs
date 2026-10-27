using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentActivityManagement.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class Kiet_Us25_AccountStatusAndRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Mặc định true để mọi tài khoản hiện có vẫn đăng nhập được sau khi nâng cấp.
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            // US-25 tách Admin thành HSSV và SystemAdmin; tài khoản Admin cũ trở thành SystemAdmin.
            migrationBuilder.Sql("UPDATE \"Users\" SET \"Role\" = 'SystemAdmin' WHERE \"Role\" = 'Admin';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE \"Users\" SET \"Role\" = 'Admin' WHERE \"Role\" IN ('SystemAdmin', 'HSSV');");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Users");
        }
    }
}
