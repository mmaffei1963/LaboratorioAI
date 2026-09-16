using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class OrdiniAccettazioneInvio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InviatoPortale",
                schema: "accettazione",
                table: "OrdiniEmessi");

            migrationBuilder.RenameColumn(
                name: "DataInvioPortale",
                schema: "accettazione",
                table: "OrdiniEmessi",
                newName: "DataInvio");

            migrationBuilder.AddColumn<string>(
                name: "Hash",
                schema: "accettazione",
                table: "OrdiniEmessi",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IdInsediamento",
                schema: "accettazione",
                table: "OrdiniEmessi",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NomeFile",
                schema: "accettazione",
                table: "OrdiniEmessi",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PercorsoFile",
                schema: "accettazione",
                table: "OrdiniEmessi",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StatoInvio",
                schema: "accettazione",
                table: "OrdiniEmessi",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Tentativi",
                schema: "accettazione",
                table: "OrdiniEmessi",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "UltimoErrore",
                schema: "accettazione",
                table: "OrdiniEmessi",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrdiniEmessi_StatoInvio",
                schema: "accettazione",
                table: "OrdiniEmessi",
                column: "StatoInvio");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrdiniEmessi_StatoInvio",
                schema: "accettazione",
                table: "OrdiniEmessi");

            migrationBuilder.DropColumn(
                name: "Hash",
                schema: "accettazione",
                table: "OrdiniEmessi");

            migrationBuilder.DropColumn(
                name: "IdInsediamento",
                schema: "accettazione",
                table: "OrdiniEmessi");

            migrationBuilder.DropColumn(
                name: "NomeFile",
                schema: "accettazione",
                table: "OrdiniEmessi");

            migrationBuilder.DropColumn(
                name: "PercorsoFile",
                schema: "accettazione",
                table: "OrdiniEmessi");

            migrationBuilder.DropColumn(
                name: "StatoInvio",
                schema: "accettazione",
                table: "OrdiniEmessi");

            migrationBuilder.DropColumn(
                name: "Tentativi",
                schema: "accettazione",
                table: "OrdiniEmessi");

            migrationBuilder.DropColumn(
                name: "UltimoErrore",
                schema: "accettazione",
                table: "OrdiniEmessi");

            migrationBuilder.RenameColumn(
                name: "DataInvio",
                schema: "accettazione",
                table: "OrdiniEmessi",
                newName: "DataInvioPortale");

            migrationBuilder.AddColumn<bool>(
                name: "InviatoPortale",
                schema: "accettazione",
                table: "OrdiniEmessi",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
