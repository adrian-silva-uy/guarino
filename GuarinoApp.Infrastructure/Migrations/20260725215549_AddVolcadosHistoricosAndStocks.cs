using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GuarinoApp.Infrastructure.Migrations
    {
    /// <inheritdoc />
    public partial class AddVolcadosHistoricosAndStocks : Migration
        {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
            {
            migrationBuilder.CreateTable(
                name: "StocksVariedad",
                columns: table => new
                    {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Mes = table.Column<int>(type: "int", nullable: false),
                    Año = table.Column<int>(type: "int", nullable: false),
                    Variedad = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Cantidad = table.Column<int>(type: "int", nullable: false),
                    Color = table.Column<string>(type: "nvarchar(max)", nullable: true)
                    },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StocksVariedad", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VolcadosHistoricos",
                columns: table => new
                    {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Mes = table.Column<int>(type: "int", nullable: false),
                    Año = table.Column<int>(type: "int", nullable: false),
                    Variedad = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    KgsVolcados = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Bins = table.Column<int>(type: "int", nullable: false),
                    Productor = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Zafra = table.Column<string>(type: "nvarchar(max)", nullable: true)
                    },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VolcadosHistoricos", x => x.Id);
                });
            }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
            {
            migrationBuilder.DropTable(
                name: "StocksVariedad");

            migrationBuilder.DropTable(
                name: "VolcadosHistoricos");
            }
        }
    }
