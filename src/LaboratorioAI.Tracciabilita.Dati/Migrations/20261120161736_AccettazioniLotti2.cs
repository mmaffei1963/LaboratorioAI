using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Tracciabilita.Dati.Migrations
{
    /// <inheritdoc />
    public partial class AccettazioniLotti2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IdAccettazione",
                schema: "trac",
                table: "LottiInUso",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PropostaPerFornitore",
                schema: "trac",
                table: "CategorieMateriaPrima",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Annullata",
                schema: "trac",
                table: "Accettazioni",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Colli",
                schema: "trac",
                table: "Accettazioni",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Progressivo",
                schema: "trac",
                table: "Accettazioni",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IdAccettazione",
                schema: "trac",
                table: "LottiInUso");

            migrationBuilder.DropColumn(
                name: "PropostaPerFornitore",
                schema: "trac",
                table: "CategorieMateriaPrima");

            migrationBuilder.DropColumn(
                name: "Annullata",
                schema: "trac",
                table: "Accettazioni");

            migrationBuilder.DropColumn(
                name: "Colli",
                schema: "trac",
                table: "Accettazioni");

            migrationBuilder.DropColumn(
                name: "Progressivo",
                schema: "trac",
                table: "Accettazioni");
        }
    }
}
