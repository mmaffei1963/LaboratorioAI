using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Tracciabilita.Dati.Migrations
{
    /// <inheritdoc />
    public partial class ModoQuantitaConsumi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ModoQuantitaConsumi",
                schema: "trac",
                table: "ImpostazioniAziende",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ModoQuantitaConsumi",
                schema: "trac",
                table: "ImpostazioniAziende");
        }
    }
}
