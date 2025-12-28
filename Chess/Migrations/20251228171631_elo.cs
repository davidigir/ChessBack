using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chess.Migrations
{
    /// <inheritdoc />
    public partial class elo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Elo",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Elo",
                table: "Users");
        }
    }
}
