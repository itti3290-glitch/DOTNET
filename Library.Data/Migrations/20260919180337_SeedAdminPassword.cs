using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Library.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedAdminPassword : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Members",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "v1.210000.IRzm9BJmjQ3lBW+ICEoajQ==.a5+PbzmF1az6vrnPDwyIjjW93HlY+881dxC+xz7oQaQ=");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Members",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "!SEED-PLACEHOLDER!");
        }
    }
}
