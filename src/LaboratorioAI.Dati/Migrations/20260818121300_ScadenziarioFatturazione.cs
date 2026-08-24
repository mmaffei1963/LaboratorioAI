using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class ScadenziarioFatturazione : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Fatturato",
                schema: "tarature",
                table: "TermometriEsterni",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "IdTaraturaOrigine",
                schema: "tarature",
                table: "TermometriEsterni",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TermometriEsterni_IdTaraturaOrigine",
                schema: "tarature",
                table: "TermometriEsterni",
                column: "IdTaraturaOrigine");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TermometriEsterni_IdTaraturaOrigine",
                schema: "tarature",
                table: "TermometriEsterni");

            migrationBuilder.DropColumn(
                name: "Fatturato",
                schema: "tarature",
                table: "TermometriEsterni");

            migrationBuilder.DropColumn(
                name: "IdTaraturaOrigine",
                schema: "tarature",
                table: "TermometriEsterni");
        }
    }
}
