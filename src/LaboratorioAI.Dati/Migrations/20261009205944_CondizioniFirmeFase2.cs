using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class CondizioniFirmeFase2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CognomeDichiarato",
                schema: "contratti",
                table: "RichiesteApprovazione",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DataInvioRicevuta",
                schema: "contratti",
                table: "RichiesteApprovazione",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ErroreInvioRicevuta",
                schema: "contratti",
                table: "RichiesteApprovazione",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImprontaTesti",
                schema: "contratti",
                table: "RichiesteApprovazione",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NomeDichiarato",
                schema: "contratti",
                table: "RichiesteApprovazione",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NumeroRicevuta",
                schema: "contratti",
                table: "RichiesteApprovazione",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OffertaImpronta",
                schema: "contratti",
                table: "RichiesteApprovazione",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "OffertaPdf",
                schema: "contratti",
                table: "RichiesteApprovazione",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OffertaRiferimento",
                schema: "contratti",
                table: "RichiesteApprovazione",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QualificaDichiarata",
                schema: "contratti",
                table: "RichiesteApprovazione",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RicevutaImpronta",
                schema: "contratti",
                table: "RichiesteApprovazione",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RicevutaPdf",
                schema: "contratti",
                table: "RichiesteApprovazione",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FirmeGraficheApprovazione",
                schema: "contratti",
                columns: table => new
                {
                    IdFirmaGraficaApprovazione = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdRichiestaApprovazione = table.Column<int>(type: "int", nullable: false),
                    Riquadro = table.Column<int>(type: "int", nullable: false),
                    ImmaginePng = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    Tracciato = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Puntatore = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Larghezza = table.Column<int>(type: "int", nullable: false),
                    Altezza = table.Column<int>(type: "int", nullable: false),
                    Impronta = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    DataUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FirmeGraficheApprovazione", x => x.IdFirmaGraficaApprovazione);
                    table.ForeignKey(
                        name: "FK_FirmeGraficheApprovazione_RichiesteApprovazione_IdRichiestaApprovazione",
                        column: x => x.IdRichiestaApprovazione,
                        principalSchema: "contratti",
                        principalTable: "RichiesteApprovazione",
                        principalColumn: "IdRichiestaApprovazione",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FirmeGraficheApprovazione_IdRichiestaApprovazione_Riquadro",
                schema: "contratti",
                table: "FirmeGraficheApprovazione",
                columns: new[] { "IdRichiestaApprovazione", "Riquadro" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FirmeGraficheApprovazione",
                schema: "contratti");

            migrationBuilder.DropColumn(
                name: "CognomeDichiarato",
                schema: "contratti",
                table: "RichiesteApprovazione");

            migrationBuilder.DropColumn(
                name: "DataInvioRicevuta",
                schema: "contratti",
                table: "RichiesteApprovazione");

            migrationBuilder.DropColumn(
                name: "ErroreInvioRicevuta",
                schema: "contratti",
                table: "RichiesteApprovazione");

            migrationBuilder.DropColumn(
                name: "ImprontaTesti",
                schema: "contratti",
                table: "RichiesteApprovazione");

            migrationBuilder.DropColumn(
                name: "NomeDichiarato",
                schema: "contratti",
                table: "RichiesteApprovazione");

            migrationBuilder.DropColumn(
                name: "NumeroRicevuta",
                schema: "contratti",
                table: "RichiesteApprovazione");

            migrationBuilder.DropColumn(
                name: "OffertaImpronta",
                schema: "contratti",
                table: "RichiesteApprovazione");

            migrationBuilder.DropColumn(
                name: "OffertaPdf",
                schema: "contratti",
                table: "RichiesteApprovazione");

            migrationBuilder.DropColumn(
                name: "OffertaRiferimento",
                schema: "contratti",
                table: "RichiesteApprovazione");

            migrationBuilder.DropColumn(
                name: "QualificaDichiarata",
                schema: "contratti",
                table: "RichiesteApprovazione");

            migrationBuilder.DropColumn(
                name: "RicevutaImpronta",
                schema: "contratti",
                table: "RichiesteApprovazione");

            migrationBuilder.DropColumn(
                name: "RicevutaPdf",
                schema: "contratti",
                table: "RichiesteApprovazione");
        }
    }
}
