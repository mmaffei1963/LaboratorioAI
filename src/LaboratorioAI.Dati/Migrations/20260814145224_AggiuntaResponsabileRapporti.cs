using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class AggiuntaResponsabileRapporti : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Responsabile",
                schema: "taratura",
                table: "Tarature",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RedattoDa",
                schema: "nutri",
                table: "Ricette",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Responsabile",
                schema: "taratura",
                table: "Tarature");

            migrationBuilder.DropColumn(
                name: "RedattoDa",
                schema: "nutri",
                table: "Ricette");
        }
    }
}
