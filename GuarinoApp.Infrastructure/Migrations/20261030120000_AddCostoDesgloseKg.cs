using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GuarinoApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCostoDesgloseKg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CostosDesgloseKg",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Zafra = table.Column<int>(type: "int", nullable: false),
                    CostoFrutaPlanta = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CostoPackingManoObra = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CostoCartonEnvases = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CostoEnergiaFrio = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CostoFletePuerto = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CostoOtros = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    KgExportablesProyectados = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FechaCarga = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CargadoPor = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Observaciones = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostosDesgloseKg", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CostosDesgloseKg_Zafra",
                table: "CostosDesgloseKg",
                column: "Zafra");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CostosDesgloseKg");
        }
    }
}
