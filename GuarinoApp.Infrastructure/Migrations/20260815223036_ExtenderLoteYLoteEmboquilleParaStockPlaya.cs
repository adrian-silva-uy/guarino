using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GuarinoApp.Infrastructure.Migrations
    {
    /// <inheritdoc />
    public partial class ExtenderLoteYLoteEmboquilleParaStockPlaya : Migration
        {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
            {
            migrationBuilder.AddColumn<int>(
                name: "CantidadBinsDespachados",
                table: "LotesEmboquille",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CantidadBinsVolcados",
                table: "LotesEmboquille",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Estado",
                table: "LotesEmboquille",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "IdBascula",
                table: "LotesEmboquille",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IdDestinoInterno",
                table: "LotesEmboquille",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IdEmpaque",
                table: "LotesEmboquille",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TipoLoteId",
                table: "Lotes",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LotesEmboquille_IdBascula",
                table: "LotesEmboquille",
                column: "IdBascula");

            migrationBuilder.CreateIndex(
                name: "IX_LotesEmboquille_IdDestinoInterno",
                table: "LotesEmboquille",
                column: "IdDestinoInterno");

            migrationBuilder.CreateIndex(
                name: "IX_LotesEmboquille_IdEmpaque",
                table: "LotesEmboquille",
                column: "IdEmpaque");

            migrationBuilder.CreateIndex(
                name: "IX_Lotes_TipoLoteId",
                table: "Lotes",
                column: "TipoLoteId");

            migrationBuilder.AddForeignKey(
                name: "FK_Lotes_TiposLote_TipoLoteId",
                table: "Lotes",
                column: "TipoLoteId",
                principalTable: "TiposLote",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LotesEmboquille_Basculas_IdBascula",
                table: "LotesEmboquille",
                column: "IdBascula",
                principalTable: "Basculas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LotesEmboquille_DestinosInternos_IdDestinoInterno",
                table: "LotesEmboquille",
                column: "IdDestinoInterno",
                principalTable: "DestinosInternos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LotesEmboquille_Empaques_IdEmpaque",
                table: "LotesEmboquille",
                column: "IdEmpaque",
                principalTable: "Empaques",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
            {
            migrationBuilder.DropForeignKey(
                name: "FK_Lotes_TiposLote_TipoLoteId",
                table: "Lotes");

            migrationBuilder.DropForeignKey(
                name: "FK_LotesEmboquille_Basculas_IdBascula",
                table: "LotesEmboquille");

            migrationBuilder.DropForeignKey(
                name: "FK_LotesEmboquille_DestinosInternos_IdDestinoInterno",
                table: "LotesEmboquille");

            migrationBuilder.DropForeignKey(
                name: "FK_LotesEmboquille_Empaques_IdEmpaque",
                table: "LotesEmboquille");

            migrationBuilder.DropTable(
                name: "RITMOCALIBRADORA");

            migrationBuilder.DropIndex(
                name: "IX_LotesEmboquille_IdBascula",
                table: "LotesEmboquille");

            migrationBuilder.DropIndex(
                name: "IX_LotesEmboquille_IdDestinoInterno",
                table: "LotesEmboquille");

            migrationBuilder.DropIndex(
                name: "IX_LotesEmboquille_IdEmpaque",
                table: "LotesEmboquille");

            migrationBuilder.DropIndex(
                name: "IX_Lotes_TipoLoteId",
                table: "Lotes");

            migrationBuilder.DropColumn(
                name: "CantidadBinsDespachados",
                table: "LotesEmboquille");

            migrationBuilder.DropColumn(
                name: "CantidadBinsVolcados",
                table: "LotesEmboquille");

            migrationBuilder.DropColumn(
                name: "Estado",
                table: "LotesEmboquille");

            migrationBuilder.DropColumn(
                name: "IdBascula",
                table: "LotesEmboquille");

            migrationBuilder.DropColumn(
                name: "IdDestinoInterno",
                table: "LotesEmboquille");

            migrationBuilder.DropColumn(
                name: "IdEmpaque",
                table: "LotesEmboquille");

            migrationBuilder.DropColumn(
                name: "TipoLoteId",
                table: "Lotes");
            }
        }
    }
