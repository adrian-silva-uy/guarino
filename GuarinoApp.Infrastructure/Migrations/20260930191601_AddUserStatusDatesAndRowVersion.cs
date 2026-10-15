using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GuarinoApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserStatusDatesAndRowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ActivatedAt",
                table: "AppUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeactivatedAt",
                table: "AppUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "AppUsers",
                type: "rowversion",
                rowVersion: true,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActivatedAt",
                table: "AppUsers");

            migrationBuilder.DropColumn(
                name: "DeactivatedAt",
                table: "AppUsers");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "AppUsers");
        }
    }
}
