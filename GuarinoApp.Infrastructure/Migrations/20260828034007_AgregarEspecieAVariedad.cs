using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GuarinoApp.Infrastructure.Migrations
    {
    /// <inheritdoc />
    public partial class AgregarEspecieAVariedad : Migration
        {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
            {
            migrationBuilder.DropForeignKey(
                name: "FK_Lotes_Variedades_VariedadId",
                table: "Lotes");

            migrationBuilder.DropColumn(
                name: "Especie",
                table: "Variedades");

            migrationBuilder.AddColumn<int>(
                name: "EspecieId",
                table: "Variedades",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RazonSocial",
                table: "Productores",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "BasculaId",
                table: "Lotes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CantidadBins",
                table: "Lotes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CantidadBinsDesp",
                table: "Lotes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CantidadBinsEmb",
                table: "Lotes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DestinoInternoId",
                table: "Lotes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmpaqueId",
                table: "Lotes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaIngreso",
                table: "Lotes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdRemito",
                table: "Lotes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "KilosNetos",
                table: "Lotes",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "ProductorId",
                table: "Lotes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "LugarDestino",
                columns: table => new
                    {
                    IdLugarDestino = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Direccion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Localidad = table.Column<string>(type: "nvarchar(max)", nullable: true)
                    },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LugarDestino", x => x.IdLugarDestino);
                });

            migrationBuilder.CreateTable(
                name: "Lugares",
                columns: table => new
                    {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Direccion = table.Column<string>(type: "nvarchar(max)", nullable: true)
                    },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lugares", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LugarOrigen",
                columns: table => new
                    {
                    IdLugarOrigen = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Direccion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Localidad = table.Column<string>(type: "nvarchar(max)", nullable: true)
                    },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LugarOrigen", x => x.IdLugarOrigen);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Variedades_EspecieId",
                table: "Variedades",
                column: "EspecieId");

            migrationBuilder.CreateIndex(
                name: "IX_Lotes_BasculaId",
                table: "Lotes",
                column: "BasculaId");

            migrationBuilder.CreateIndex(
                name: "IX_Lotes_DestinoInternoId",
                table: "Lotes",
                column: "DestinoInternoId");

            migrationBuilder.CreateIndex(
                name: "IX_Lotes_EmpaqueId",
                table: "Lotes",
                column: "EmpaqueId");

            migrationBuilder.CreateIndex(
                name: "IX_Lotes_ProductorId",
                table: "Lotes",
                column: "ProductorId");

            migrationBuilder.AddForeignKey(
                name: "FK_Lotes_Basculas_BasculaId",
                table: "Lotes",
                column: "BasculaId",
                principalTable: "Basculas",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Lotes_DestinosInternos_DestinoInternoId",
                table: "Lotes",
                column: "DestinoInternoId",
                principalTable: "DestinosInternos",
                principalColumn: "Id");

            // Corregido: Cascade -> Restrict. Junto con la de Productores de abajo, generaba
            // dos rutas de cascada hacia Lotes (directa y vía Quinta->Productor), que SQL Server
            // rechaza ("may cause cycles or multiple cascade paths"). Ver ApplicationDbContext.cs.
            migrationBuilder.AddForeignKey(
                name: "FK_Lotes_Empaques_EmpaqueId",
                table: "Lotes",
                column: "EmpaqueId",
                principalTable: "Empaques",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Corregido: Cascade -> Restrict (ver comentario arriba).
            migrationBuilder.AddForeignKey(
                name: "FK_Lotes_Productores_ProductorId",
                table: "Lotes",
                column: "ProductorId",
                principalTable: "Productores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Lotes_Variedades_VariedadId",
                table: "Lotes",
                column: "VariedadId",
                principalTable: "Variedades",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Variedades_Especies_EspecieId",
                table: "Variedades",
                column: "EspecieId",
                principalTable: "Especies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
            {
            migrationBuilder.DropForeignKey(
                name: "FK_Lotes_Basculas_BasculaId",
                table: "Lotes");

            migrationBuilder.DropForeignKey(
                name: "FK_Lotes_DestinosInternos_DestinoInternoId",
                table: "Lotes");

            migrationBuilder.DropForeignKey(
                name: "FK_Lotes_Empaques_EmpaqueId",
                table: "Lotes");

            migrationBuilder.DropForeignKey(
                name: "FK_Lotes_Productores_ProductorId",
                table: "Lotes");

            migrationBuilder.DropForeignKey(
                name: "FK_Lotes_Variedades_VariedadId",
                table: "Lotes");

            migrationBuilder.DropForeignKey(
                name: "FK_Variedades_Especies_EspecieId",
                table: "Variedades");

            migrationBuilder.DropTable(
                name: "LugarDestino");

            migrationBuilder.DropTable(
                name: "Lugares");

            migrationBuilder.DropTable(
                name: "LugarOrigen");

            migrationBuilder.DropIndex(
                name: "IX_Variedades_EspecieId",
                table: "Variedades");

            migrationBuilder.DropIndex(
                name: "IX_Lotes_BasculaId",
                table: "Lotes");

            migrationBuilder.DropIndex(
                name: "IX_Lotes_DestinoInternoId",
                table: "Lotes");

            migrationBuilder.DropIndex(
                name: "IX_Lotes_EmpaqueId",
                table: "Lotes");

            migrationBuilder.DropIndex(
                name: "IX_Lotes_ProductorId",
                table: "Lotes");

            migrationBuilder.DropColumn(
                name: "EspecieId",
                table: "Variedades");

            migrationBuilder.DropColumn(
                name: "RazonSocial",
                table: "Productores");

            migrationBuilder.DropColumn(
                name: "BasculaId",
                table: "Lotes");

            migrationBuilder.DropColumn(
                name: "CantidadBins",
                table: "Lotes");

            migrationBuilder.DropColumn(
                name: "CantidadBinsDesp",
                table: "Lotes");

            migrationBuilder.DropColumn(
                name: "CantidadBinsEmb",
                table: "Lotes");

            migrationBuilder.DropColumn(
                name: "DestinoInternoId",
                table: "Lotes");

            migrationBuilder.DropColumn(
                name: "EmpaqueId",
                table: "Lotes");

            migrationBuilder.DropColumn(
                name: "FechaIngreso",
                table: "Lotes");

            migrationBuilder.DropColumn(
                name: "IdRemito",
                table: "Lotes");

            migrationBuilder.DropColumn(
                name: "KilosNetos",
                table: "Lotes");

            migrationBuilder.DropColumn(
                name: "ProductorId",
                table: "Lotes");

            migrationBuilder.AddColumn<string>(
                name: "Especie",
                table: "Variedades",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Lotes_Variedades_VariedadId",
                table: "Lotes",
                column: "VariedadId",
                principalTable: "Variedades",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
            }
        }
    }
