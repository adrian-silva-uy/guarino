using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GuarinoApp.Infrastructure.Migrations
{
    /// <summary>
    /// Corrección de violaciones de forma normal detectadas en auditoría:
    /// 1) 7 maestras (Especies, Empaques, Basculas, TiposLote, DestinosInternos,
    ///    Emboquilles, Responsables) tenían una columna IdXxx muerta junto a la
    ///    PK identity Id: siempre en 0 y usada por 2 queries que nunca matcheaban.
    /// 2) Volcados.IdBascula: columna legacy sin FK ni uso (la bascula se
    ///    referencia por Canal).
    /// 3) LotesEmboquille: 4 columnas para 2 relaciones (IdLote/IdEmboquille
    ///    muertas + LoteId/EmboquilleId, que son las FK reales de EF).
    /// 4) Pallets.EstadoTexto: duplicaba al enum Estado (dos fuentes de verdad);
    ///    los filtros "EstadoTexto &lt;&gt; 'B'" perdían filas con NULL en SQL.
    /// Los backfills garantizan que no se pierde información al eliminar.
    /// </summary>
    public partial class EliminarColumnasDesnormalizadas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfill ANTES de dropear EstadoTexto: el texto legado es la fuente
            // histórica del estado ('F' = En Frío, 'B' = Bajado), así el enum
            // queda con el valor correcto y no se pierde la información.
            migrationBuilder.Sql(@"
UPDATE Pallets SET Estado = 2 WHERE EstadoTexto = 'F';
UPDATE Pallets SET Estado = 5 WHERE EstadoTexto = 'B';");

            // Nota: en LotesEmboquille no hace falta backfill — LoteId/EmboquilleId
            // son columnas con FK (garantizadas por integridad referencial) y eran
            // las que EF usaba en todas las consultas; IdLote/IdEmboquille eran la
            // copia muerta (idéntica en los datos existentes).

            migrationBuilder.DropColumn(
                name: "IdBascula",
                table: "Volcados");

            migrationBuilder.DropColumn(
                name: "IdTipoLote",
                table: "TiposLote");

            migrationBuilder.DropColumn(
                name: "IdResponsable",
                table: "Responsables");

            migrationBuilder.DropColumn(
                name: "EstadoTexto",
                table: "Pallets");

            migrationBuilder.DropColumn(
                name: "IdEmboquille",
                table: "LotesEmboquille");

            migrationBuilder.DropColumn(
                name: "IdLote",
                table: "LotesEmboquille");

            migrationBuilder.DropColumn(
                name: "IdEspecie",
                table: "Especies");

            migrationBuilder.DropColumn(
                name: "IdEmpaque",
                table: "Empaques");

            migrationBuilder.DropColumn(
                name: "IdEmboquille",
                table: "Emboquilles");

            migrationBuilder.DropColumn(
                name: "IdDestinoInterno",
                table: "DestinosInternos");

            migrationBuilder.DropColumn(
                name: "IdBascula",
                table: "Basculas");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IdBascula",
                table: "Volcados",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IdTipoLote",
                table: "TiposLote",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "IdResponsable",
                table: "Responsables",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "EstadoTexto",
                table: "Pallets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IdEmboquille",
                table: "LotesEmboquille",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "IdLote",
                table: "LotesEmboquille",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Restauración de datos en el rollback: las columnas renacidas en 0 se
            // rellenan desde las FKs/texto sobrevivientes, y el texto del estado se
            // reconstruye solo donde había valor semántico ('F'/'B').
            migrationBuilder.Sql(@"
UPDATE LotesEmboquille SET IdLote = LoteId, IdEmboquille = EmboquilleId;
UPDATE Pallets SET EstadoTexto = CASE Estado WHEN 2 THEN 'F' WHEN 5 THEN 'B' ELSE NULL END;");

            migrationBuilder.AddColumn<int>(
                name: "IdEspecie",
                table: "Especies",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "IdEmpaque",
                table: "Empaques",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "IdEmboquille",
                table: "Emboquilles",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "IdDestinoInterno",
                table: "DestinosInternos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "IdBascula",
                table: "Basculas",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
