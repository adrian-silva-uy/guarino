using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace GuarinoApp.Infrastructure.Migrations;

/// <summary>
/// Repara instalaciones en las que la migración original quedó registrada como
/// aplicada pero la tabla fue eliminada o nunca llegó a crearse. Es idempotente y
/// no elimina datos existentes al revertirse.
/// </summary>
[Migration("20261125130000_RepairCostosDesgloseKgSchema")]
public partial class RepairCostosDesgloseKgSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF OBJECT_ID(N'[dbo].[CostosDesgloseKg]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[CostosDesgloseKg]
                (
                    [Id] int NOT NULL IDENTITY,
                    [Zafra] int NOT NULL,
                    [CostoFrutaPlanta] decimal(18,2) NOT NULL,
                    [CostoPackingManoObra] decimal(18,2) NOT NULL,
                    [CostoCartonEnvases] decimal(18,2) NOT NULL,
                    [CostoEnergiaFrio] decimal(18,2) NOT NULL,
                    [CostoFletePuerto] decimal(18,2) NOT NULL,
                    [CostoOtros] decimal(18,2) NOT NULL,
                    [KgExportablesProyectados] decimal(18,2) NOT NULL,
                    [FechaCarga] datetime2 NOT NULL,
                    [CargadoPor] nvarchar(max) NULL,
                    [Observaciones] nvarchar(max) NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    [CreatedBy] nvarchar(max) NULL,
                    [UpdatedAt] datetime2 NULL,
                    [UpdatedBy] nvarchar(max) NULL,
                    [IsDeleted] bit NOT NULL,
                    CONSTRAINT [PK_CostosDesgloseKg] PRIMARY KEY ([Id])
                );

                CREATE INDEX [IX_CostosDesgloseKg_Zafra]
                    ON [dbo].[CostosDesgloseKg] ([Zafra]);
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Intencionalmente no elimina la tabla: puede contener datos existentes
        // en instalaciones reparadas.
    }
}
