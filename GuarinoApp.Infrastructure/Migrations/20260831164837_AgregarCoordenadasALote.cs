using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GuarinoApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarCoordenadasALote : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Latitude",
                table: "Lotes",
                type: "decimal(10,6)",
                precision: 10,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Longitude",
                table: "Lotes",
                type: "decimal(10,6)",
                precision: 10,
                scale: 6,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Lotes");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Lotes");
        }
    }
}
