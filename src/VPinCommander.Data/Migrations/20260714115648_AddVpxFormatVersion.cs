using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VPinCommander.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVpxFormatVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "VpxFormatVersion",
                table: "Tables",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VpxFormatVersion",
                table: "Tables");
        }
    }
}
