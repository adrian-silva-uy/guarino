using Microsoft.EntityFrameworkCore.Migrations;

namespace GuarinoApp.Infrastructure.Data.Migrations;

public partial class AddClimateDataSeeding : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Agregar valores de Latitude y Longitude a los lotes existentes
        // Usando coordenadas de Salto, Paysandú, Uruguay y Norte de Brasil
        migrationBuilder.UpdateData(
            table: "Lotes",
            keyColumn: "Id",
            keyValue: 1,
            columns: new[] { "Latitude", "Longitude" },
            values: new object[] { -32.3814m, -56.1559m }); // Salto, Uruguay

        migrationBuilder.Sql(@"
            UPDATE Lotes 
            SET Latitude = CASE 
                WHEN Id % 5 = 0 THEN CAST(-32.3814 + (RAND() * 0.5) AS DECIMAL(10,6))
                WHEN Id % 5 = 1 THEN CAST(-32.2500 + (RAND() * 0.5) AS DECIMAL(10,6))
                WHEN Id % 5 = 2 THEN CAST(-32.1500 + (RAND() * 0.5) AS DECIMAL(10,6))
                WHEN Id % 5 = 3 THEN CAST(-32.0500 + (RAND() * 0.5) AS DECIMAL(10,6))
                ELSE CAST(-32.4500 + (RAND() * 0.5) AS DECIMAL(10,6))
            END,
            Longitude = CASE 
                WHEN Id % 5 = 0 THEN CAST(-56.1559 + (RAND() * 0.5) AS DECIMAL(10,6))
                WHEN Id % 5 = 1 THEN CAST(-56.2000 + (RAND() * 0.5) AS DECIMAL(10,6))
                WHEN Id % 5 = 2 THEN CAST(-56.0500 + (RAND() * 0.5) AS DECIMAL(10,6))
                WHEN Id % 5 = 3 THEN CAST(-56.3000 + (RAND() * 0.5) AS DECIMAL(10,6))
                ELSE CAST(-56.1000 + (RAND() * 0.5) AS DECIMAL(10,6))
            END
            WHERE Latitude IS NULL OR Longitude IS NULL;
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            UPDATE Lotes 
            SET Latitude = NULL, Longitude = NULL;
        ");
    }
}
