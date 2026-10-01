using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GuarinoApp.Infrastructure.Migrations
{
    /// <summary>
    /// Normalización de roles: tabla maestra Roles (fuente única de verdad)
    /// + FK AppUsers.RoleId. Incluye el seed de los 4 roles y la migración de
    /// los datos existentes (AppUsers.Role string → RoleId) ANTES de dropear
    /// la columna, de modo que ningún usuario queda sin rol.
    /// </summary>
    public partial class AddRolesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            // Seed de los roles de la aplicación (se ejecuta una sola vez:
            // la migración queda registrada en __EFMigrationsHistory).
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM Roles)
BEGIN
    INSERT INTO Roles (Name, IsActive, CreatedAt, CreatedBy, IsDeleted)
    VALUES ('Admin', 1, SYSUTCDATETIME(), 'migracion AddRolesTable', 0),
           ('Gerencia', 1, SYSUTCDATETIME(), 'migracion AddRolesTable', 0),
           ('Contador', 1, SYSUTCDATETIME(), 'migracion AddRolesTable', 0),
           ('Operador', 1, SYSUTCDATETIME(), 'migracion AddRolesTable', 0);
END");

            migrationBuilder.AddColumn<int>(
                name: "RoleId",
                table: "AppUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Migración de datos: cada usuario existente queda asociado a su
            // rol por Id antes de eliminar la columna de texto.
            migrationBuilder.Sql(@"
UPDATE u
SET RoleId = r.Id
FROM AppUsers u
INNER JOIN Roles r ON r.Name = u.Role;");

            migrationBuilder.CreateIndex(
                name: "IX_AppUsers_RoleId",
                table: "AppUsers",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Name",
                table: "Roles",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AppUsers_Roles_RoleId",
                table: "AppUsers",
                column: "RoleId",
                principalTable: "Roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Al final, después del backfill: sin esta orden se perdería la
            // información del rol de cada usuario.
            migrationBuilder.DropColumn(
                name: "Role",
                table: "AppUsers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Se restaura la columna de texto primero y se rellena desde la
            // FK, para que el rollback no deje usuarios sin rol.
            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "AppUsers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(@"
UPDATE u
SET Role = r.Name
FROM AppUsers u
INNER JOIN Roles r ON r.Id = u.RoleId;");

            migrationBuilder.DropForeignKey(
                name: "FK_AppUsers_Roles_RoleId",
                table: "AppUsers");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_AppUsers_RoleId",
                table: "AppUsers");

            migrationBuilder.DropColumn(
                name: "RoleId",
                table: "AppUsers");
        }
    }
}
