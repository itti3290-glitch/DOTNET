using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Library.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedDemoMemberPassword : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Members",
                keyColumn: "Id",
                keyValue: 2,
                column: "PasswordHash",
                value: "v1.210000.gk+khtvbDLcxJWq9rolcZw==.bl/tr0AX88InBEEjMRfj4r13yydmhusBwAuS99t3+oU=");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Members",
                keyColumn: "Id",
                keyValue: 2,
                column: "PasswordHash",
                value: "!SEED-PLACEHOLDER!");
        }
    }
}
