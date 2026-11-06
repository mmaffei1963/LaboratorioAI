using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class RapportiniDocumentazione : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AttivitaSvolta",
                schema: "cons",
                table: "Rapportini",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FollowUpAzione",
                schema: "cons",
                table: "Rapportini",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FollowUpResponsabile",
                schema: "cons",
                table: "Rapportini",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FollowUpTermine",
                schema: "cons",
                table: "Rapportini",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FonteInformazioni",
                schema: "cons",
                table: "Rapportini",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NotaPianoAnalitico",
                schema: "cons",
                table: "Rapportini",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RiferimentoDelega",
                schema: "cons",
                table: "Rapportini",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RiferimentoOfferta",
                schema: "cons",
                table: "Rapportini",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "VerificaRichiamiLaboratorio",
                schema: "cons",
                table: "Rapportini",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "DelegheClienti",
                schema: "cons",
                columns: table => new
                {
                    IdDelegaCliente = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdClienteArcLab = table.Column<int>(type: "int", nullable: false),
                    RagioneSociale = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Estremi = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    DataSottoscrizione = table.Column<DateTime>(type: "date", nullable: false),
                    DataFine = table.Column<DateTime>(type: "date", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Attivo = table.Column<bool>(type: "bit", nullable: false),
                    IdUtenteModifica = table.Column<int>(type: "int", nullable: true),
                    DataModifica = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DelegheClienti", x => x.IdDelegaCliente);
                });

            migrationBuilder.CreateTable(
                name: "DocumentiTipo",
                schema: "cons",
                columns: table => new
                {
                    IdDocumentoTipo = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Area = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RichiamaLaboratorio = table.Column<bool>(type: "bit", nullable: false),
                    PianoAnalitico = table.Column<bool>(type: "bit", nullable: false),
                    Ordine = table.Column<int>(type: "int", nullable: false),
                    Attivo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentiTipo", x => x.IdDocumentoTipo);
                });

            migrationBuilder.CreateTable(
                name: "RapportiniDocumenti",
                schema: "cons",
                columns: table => new
                {
                    IdRapportinoDocumento = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdRapportino = table.Column<int>(type: "int", nullable: false),
                    IdDocumentoTipo = table.Column<int>(type: "int", nullable: true),
                    Nome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Revisione = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    DataDocumento = table.Column<DateTime>(type: "date", nullable: true),
                    Azione = table.Column<int>(type: "int", nullable: false),
                    Consegna = table.Column<int>(type: "int", nullable: true),
                    Ordine = table.Column<int>(type: "int", nullable: false),
                    RichiamaLaboratorio = table.Column<bool>(type: "bit", nullable: false),
                    PianoAnalitico = table.Column<bool>(type: "bit", nullable: false),
                    Attivo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RapportiniDocumenti", x => x.IdRapportinoDocumento);
                    table.ForeignKey(
                        name: "FK_RapportiniDocumenti_Rapportini_IdRapportino",
                        column: x => x.IdRapportino,
                        principalSchema: "cons",
                        principalTable: "Rapportini",
                        principalColumn: "IdRapportino",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "cons",
                table: "DocumentiTipo",
                columns: new[] { "IdDocumentoTipo", "Area", "Attivo", "Nome", "Ordine", "PianoAnalitico", "RichiamaLaboratorio" },
                values: new object[,]
                {
                    { 1, "HACCP", true, "Manuale di autocontrollo (HACCP)", 10, false, true },
                    { 2, "HACCP", true, "Piano di autocontrollo analitico", 20, true, true },
                    { 3, "HACCP", true, "Procedura di richiamo/ritiro prodotti", 30, false, true },
                    { 4, "Igiene", true, "Documento di valutazione del rischio Legionella", 40, false, true },
                    { 5, "Sicurezza", true, "Documento di valutazione dei rischi (D.Lgs. 81/08)", 50, false, false },
                    { 6, "HACCP", true, "Schede e registri di autocontrollo", 60, false, false }
                });

            migrationBuilder.CreateIndex(
                name: "IX_DelegheClienti_IdClienteArcLab",
                schema: "cons",
                table: "DelegheClienti",
                column: "IdClienteArcLab");

            migrationBuilder.CreateIndex(
                name: "IX_RapportiniDocumenti_IdRapportino",
                schema: "cons",
                table: "RapportiniDocumenti",
                column: "IdRapportino");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DelegheClienti",
                schema: "cons");

            migrationBuilder.DropTable(
                name: "DocumentiTipo",
                schema: "cons");

            migrationBuilder.DropTable(
                name: "RapportiniDocumenti",
                schema: "cons");

            migrationBuilder.DropColumn(
                name: "AttivitaSvolta",
                schema: "cons",
                table: "Rapportini");

            migrationBuilder.DropColumn(
                name: "FollowUpAzione",
                schema: "cons",
                table: "Rapportini");

            migrationBuilder.DropColumn(
                name: "FollowUpResponsabile",
                schema: "cons",
                table: "Rapportini");

            migrationBuilder.DropColumn(
                name: "FollowUpTermine",
                schema: "cons",
                table: "Rapportini");

            migrationBuilder.DropColumn(
                name: "FonteInformazioni",
                schema: "cons",
                table: "Rapportini");

            migrationBuilder.DropColumn(
                name: "NotaPianoAnalitico",
                schema: "cons",
                table: "Rapportini");

            migrationBuilder.DropColumn(
                name: "RiferimentoDelega",
                schema: "cons",
                table: "Rapportini");

            migrationBuilder.DropColumn(
                name: "RiferimentoOfferta",
                schema: "cons",
                table: "Rapportini");

            migrationBuilder.DropColumn(
                name: "VerificaRichiamiLaboratorio",
                schema: "cons",
                table: "Rapportini");
        }
    }
}
