using Microsoft.EntityFrameworkCore.Migrations;

namespace GuarinoApp.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class AddLatitudeLongitudeToLotes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Agregar columnas Latitude y Longitude si no existen
        migrationBuilder.AddColumn<decimal>(
            name: "Latitude",
            table: "Lotes",
            type: "numeric(10,6)",
            precision: 10,
            scale: 6,
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "Longitude",
            table: "Lotes",
            type: "numeric(10,6)",
            precision: 10,
            scale: 6,
            nullable: true);

        // Actualizar con coordenadas por defecto (Salto, Uruguay)
        migrationBuilder.Sql(@"
            UPDATE Lotes 
            SET Latitude = -32.3814, Longitude = -56.1559
            WHERE Latitude IS NULL OR Longitude IS NULL;
        ");
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
