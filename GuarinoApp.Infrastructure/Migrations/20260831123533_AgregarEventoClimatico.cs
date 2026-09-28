using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GuarinoApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarEventoClimatico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Lotes_Empaques_EmpaqueId",
                table: "Lotes");

            migrationBuilder.DropForeignKey(
                name: "FK_Lotes_Productores_ProductorId",
                table: "Lotes");

            migrationBuilder.CreateTable(
                name: "EventosClimaticos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LoteId = table.Column<int>(type: "int", nullable: false),
                    ProductorId = table.Column<int>(type: "int", nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TipoEvento = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SeveridadEstimada = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PorcentajePerdidaEstimada = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    KilosPerdidosEstimados = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Fuente = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Observaciones = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventosClimaticos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventosClimaticos_Lotes_LoteId",
                        column: x => x.LoteId,
                        principalTable: "Lotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EventosClimaticos_Productores_ProductorId",
                        column: x => x.ProductorId,
                        principalTable: "Productores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EventosClimaticos_Fecha",
                table: "EventosClimaticos",
                column: "Fecha");

            migrationBuilder.CreateIndex(
                name: "IX_EventosClimaticos_LoteId",
                table: "EventosClimaticos",
                column: "LoteId");

            migrationBuilder.CreateIndex(
                name: "IX_EventosClimaticos_ProductorId",
                table: "EventosClimaticos",
                column: "ProductorId");

            migrationBuilder.CreateIndex(
                name: "IX_EventosClimaticos_TipoEvento",
                table: "EventosClimaticos",
                column: "TipoEvento");

            migrationBuilder.AddForeignKey(
                name: "FK_Lotes_Empaques_EmpaqueId",
                table: "Lotes",
                column: "EmpaqueId",
                principalTable: "Empaques",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Lotes_Productores_ProductorId",
                table: "Lotes",
                column: "ProductorId",
                principalTable: "Productores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Lotes_Empaques_EmpaqueId",
                table: "Lotes");

            migrationBuilder.DropForeignKey(
                name: "FK_Lotes_Productores_ProductorId",
                table: "Lotes");

            migrationBuilder.DropTable(
                name: "EventosClimaticos");

            migrationBuilder.AddForeignKey(
                name: "FK_Lotes_Empaques_EmpaqueId",
                table: "Lotes",
                column: "EmpaqueId",
                principalTable: "Empaques",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Lotes_Productores_ProductorId",
                table: "Lotes",
                column: "ProductorId",
                principalTable: "Productores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
