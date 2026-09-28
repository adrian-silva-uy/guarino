using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GuarinoApp.Infrastructure.Migrations
    {
    /// <inheritdoc />
    public partial class AgregarCamposCompletosPallet : Migration
        {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
            {
            migrationBuilder.DropForeignKey(
                name: "FK_Pallets_Volcados_VolcadoId",
                table: "Pallets");

            migrationBuilder.AlterColumn<int>(
                name: "VolcadoId",
                table: "Pallets",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "Cantidad",
                table: "Pallets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Cuadros",
                table: "Pallets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Embalaje",
                table: "Pallets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Especie",
                table: "Pallets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EstadoTexto",
                table: "Pallets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FecCosecha",
                table: "Pallets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Fecha",
                table: "Pallets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FormaEmbalado",
                table: "Pallets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Frio",
                table: "Pallets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Hora",
                table: "Pallets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Lote",
                table: "Pallets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "O_Obs3",
                table: "Pallets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Orden",
                table: "Pallets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PalletNumero",
                table: "Pallets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PesoNeto",
                table: "Pallets",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Produccion",
                table: "Pallets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Productor",
                table: "Pallets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SPA",
                table: "Pallets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SemanaProd",
                table: "Pallets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TipoPallet",
                table: "Pallets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Variedad",
                table: "Pallets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Pallets_Volcados_VolcadoId",
                table: "Pallets",
                column: "VolcadoId",
                principalTable: "Volcados",
                principalColumn: "Id");
            }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
            {
            migrationBuilder.DropForeignKey(
                name: "FK_Pallets_Volcados_VolcadoId",
                table: "Pallets");

            migrationBuilder.DropColumn(
                name: "Cantidad",
                table: "Pallets");

            migrationBuilder.DropColumn(
                name: "Cuadros",
                table: "Pallets");

            migrationBuilder.DropColumn(
                name: "Embalaje",
                table: "Pallets");

            migrationBuilder.DropColumn(
                name: "Especie",
                table: "Pallets");

            migrationBuilder.DropColumn(
                name: "EstadoTexto",
                table: "Pallets");

            migrationBuilder.DropColumn(
                name: "FecCosecha",
                table: "Pallets");

            migrationBuilder.DropColumn(
                name: "Fecha",
                table: "Pallets");

            migrationBuilder.DropColumn(
                name: "FormaEmbalado",
                table: "Pallets");

            migrationBuilder.DropColumn(
                name: "Frio",
                table: "Pallets");

            migrationBuilder.DropColumn(
                name: "Hora",
                table: "Pallets");

            migrationBuilder.DropColumn(
                name: "Lote",
                table: "Pallets");

            migrationBuilder.DropColumn(
                name: "O_Obs3",
                table: "Pallets");

            migrationBuilder.DropColumn(
                name: "Orden",
                table: "Pallets");

            migrationBuilder.DropColumn(
                name: "PalletNumero",
                table: "Pallets");

            migrationBuilder.DropColumn(
                name: "PesoNeto",
                table: "Pallets");

            migrationBuilder.DropColumn(
                name: "Produccion",
                table: "Pallets");

            migrationBuilder.DropColumn(
                name: "Productor",
                table: "Pallets");

            migrationBuilder.DropColumn(
                name: "SPA",
                table: "Pallets");

            migrationBuilder.DropColumn(
                name: "SemanaProd",
                table: "Pallets");

            migrationBuilder.DropColumn(
                name: "TipoPallet",
                table: "Pallets");

            migrationBuilder.DropColumn(
                name: "Variedad",
                table: "Pallets");

            migrationBuilder.AlterColumn<int>(
                name: "VolcadoId",
                table: "Pallets",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Pallets_Volcados_VolcadoId",
                table: "Pallets",
                column: "VolcadoId",
                principalTable: "Volcados",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
            }
        }
    }
