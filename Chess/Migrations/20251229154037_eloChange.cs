using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chess.Migrations
{
    /// <inheritdoc />
    public partial class eloChange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BlackEloAfter",
                table: "Games",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BlackEloBefore",
                table: "Games",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EloChange",
                table: "Games",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalMovements",
                table: "Games",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WhiteEloAfter",
                table: "Games",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WhiteEloBefore",
                table: "Games",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BlackEloAfter",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "BlackEloBefore",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "EloChange",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "TotalMovements",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "WhiteEloAfter",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "WhiteEloBefore",
                table: "Games");
        }
    }
}
