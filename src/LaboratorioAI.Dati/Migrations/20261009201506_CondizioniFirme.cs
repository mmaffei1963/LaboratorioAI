using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class CondizioniFirme : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "contratti");

            migrationBuilder.CreateTable(
                name: "EventiApprovazione",
                schema: "contratti",
                columns: table => new
                {
                    IdEventoApprovazione = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdRichiestaApprovazione = table.Column<int>(type: "int", nullable: true),
                    IdClienteArcLab = table.Column<int>(type: "int", nullable: true),
                    Tipo = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    DataUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    Dati = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NomeUtente = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IndirizzoIp = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ImprontaPrecedente = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    Impronta = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventiApprovazione", x => x.IdEventoApprovazione);
                });

            migrationBuilder.CreateTable(
                name: "FirmatariClienti",
                schema: "contratti",
                columns: table => new
                {
                    IdFirmatarioCliente = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdClienteArcLab = table.Column<int>(type: "int", nullable: false),
                    ClienteRagioneSociale = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ClientePartitaIva = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Cognome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Qualifica = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    CodiceFiscale = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Attivo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    DataDesignazione = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IdUtenteDesignazione = table.Column<int>(type: "int", nullable: true),
                    NomeUtenteDesignazione = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DataCancellazione = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FirmatariClienti", x => x.IdFirmatarioCliente);
                });

            migrationBuilder.CreateTable(
                name: "RevisioniCondizioni",
                schema: "contratti",
                columns: table => new
                {
                    IdRevisioneCondizioni = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codice = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NumeroRevisione = table.Column<int>(type: "int", nullable: false),
                    DataRevisione = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DataEntrataVigore = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Stato = table.Column<int>(type: "int", nullable: false),
                    RichiedeNuovaApprovazione = table.Column<bool>(type: "bit", nullable: false),
                    TestoCosaCambia = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NomeFilePdf = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    ContenutoPdf = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    DimensionePdf = table.Column<long>(type: "bigint", nullable: true),
                    ImprontaPdf = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    DataPubblicazione = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IdUtentePubblicazione = table.Column<int>(type: "int", nullable: true),
                    NomeUtentePubblicazione = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Attivo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IdUtenteCreazione = table.Column<int>(type: "int", nullable: true),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    DataUltimaModifica = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RevisioniCondizioni", x => x.IdRevisioneCondizioni);
                });

            migrationBuilder.CreateTable(
                name: "ClausoleCondizioni",
                schema: "contratti",
                columns: table => new
                {
                    IdClausolaCondizioni = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdRevisioneCondizioni = table.Column<int>(type: "int", nullable: false),
                    Ordine = table.Column<int>(type: "int", nullable: false),
                    Articolo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ContenutoBreve = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    TestoIntegrale = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Attivo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClausoleCondizioni", x => x.IdClausolaCondizioni);
                    table.ForeignKey(
                        name: "FK_ClausoleCondizioni_RevisioniCondizioni_IdRevisioneCondizioni",
                        column: x => x.IdRevisioneCondizioni,
                        principalSchema: "contratti",
                        principalTable: "RevisioniCondizioni",
                        principalColumn: "IdRevisioneCondizioni",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RichiesteApprovazione",
                schema: "contratti",
                columns: table => new
                {
                    IdRichiestaApprovazione = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdClienteArcLab = table.Column<int>(type: "int", nullable: false),
                    ClienteRagioneSociale = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ClientePartitaIva = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    IdRevisioneCondizioni = table.Column<int>(type: "int", nullable: false),
                    IdPreventivo = table.Column<int>(type: "int", nullable: true),
                    IdFirmatarioCliente = table.Column<int>(type: "int", nullable: true),
                    Stato = table.Column<int>(type: "int", nullable: false),
                    Modalita = table.Column<int>(type: "int", nullable: true),
                    ImprontaCodiceLink = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    ScadenzaLink = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DataInvio = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DataApertura = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DataPerfezionamento = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NumeroPromemoria = table.Column<int>(type: "int", nullable: false),
                    DataUltimoPromemoria = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IdUtenteCreazione = table.Column<int>(type: "int", nullable: true),
                    NomeUtenteCreazione = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    DataUltimaModifica = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RichiesteApprovazione", x => x.IdRichiestaApprovazione);
                    table.ForeignKey(
                        name: "FK_RichiesteApprovazione_FirmatariClienti_IdFirmatarioCliente",
                        column: x => x.IdFirmatarioCliente,
                        principalSchema: "contratti",
                        principalTable: "FirmatariClienti",
                        principalColumn: "IdFirmatarioCliente",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RichiesteApprovazione_RevisioniCondizioni_IdRevisioneCondizioni",
                        column: x => x.IdRevisioneCondizioni,
                        principalSchema: "contratti",
                        principalTable: "RevisioniCondizioni",
                        principalColumn: "IdRevisioneCondizioni",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClausoleCondizioni_IdRevisioneCondizioni_Ordine",
                schema: "contratti",
                table: "ClausoleCondizioni",
                columns: new[] { "IdRevisioneCondizioni", "Ordine" });

            migrationBuilder.CreateIndex(
                name: "IX_EventiApprovazione_IdClienteArcLab",
                schema: "contratti",
                table: "EventiApprovazione",
                column: "IdClienteArcLab");

            migrationBuilder.CreateIndex(
                name: "IX_EventiApprovazione_IdRichiestaApprovazione",
                schema: "contratti",
                table: "EventiApprovazione",
                column: "IdRichiestaApprovazione");

            migrationBuilder.CreateIndex(
                name: "IX_FirmatariClienti_IdClienteArcLab_Attivo",
                schema: "contratti",
                table: "FirmatariClienti",
                columns: new[] { "IdClienteArcLab", "Attivo" });

            migrationBuilder.CreateIndex(
                name: "IX_RevisioniCondizioni_NumeroRevisione_Attivo",
                schema: "contratti",
                table: "RevisioniCondizioni",
                columns: new[] { "NumeroRevisione", "Attivo" });

            migrationBuilder.CreateIndex(
                name: "IX_RichiesteApprovazione_IdClienteArcLab_IdRevisioneCondizioni",
                schema: "contratti",
                table: "RichiesteApprovazione",
                columns: new[] { "IdClienteArcLab", "IdRevisioneCondizioni" });

            migrationBuilder.CreateIndex(
                name: "IX_RichiesteApprovazione_IdFirmatarioCliente",
                schema: "contratti",
                table: "RichiesteApprovazione",
                column: "IdFirmatarioCliente");

            migrationBuilder.CreateIndex(
                name: "IX_RichiesteApprovazione_IdRevisioneCondizioni",
                schema: "contratti",
                table: "RichiesteApprovazione",
                column: "IdRevisioneCondizioni");

            migrationBuilder.CreateIndex(
                name: "IX_RichiesteApprovazione_ImprontaCodiceLink",
                schema: "contratti",
                table: "RichiesteApprovazione",
                column: "ImprontaCodiceLink",
                unique: true,
                filter: "[ImprontaCodiceLink] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RichiesteApprovazione_Stato",
                schema: "contratti",
                table: "RichiesteApprovazione",
                column: "Stato");

            // Registro delle evidenze a sola aggiunta (09/10/2026): il database rifiuta qualsiasi modifica o
            // cancellazione delle righe, anche fatta a mano o da un altro programma (Specifica v0.4 §8).
            migrationBuilder.Sql(@"
CREATE TRIGGER [contratti].[TR_EventiApprovazione_SolaAggiunta]
ON [contratti].[EventiApprovazione]
INSTEAD OF UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    THROW 51000, N'Il registro delle evidenze delle approvazioni e'' a sola aggiunta: modifiche e cancellazioni non sono ammesse.', 1;
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [contratti].[TR_EventiApprovazione_SolaAggiunta];");

            migrationBuilder.DropTable(
                name: "ClausoleCondizioni",
                schema: "contratti");

            migrationBuilder.DropTable(
                name: "EventiApprovazione",
                schema: "contratti");

            migrationBuilder.DropTable(
                name: "RichiesteApprovazione",
                schema: "contratti");

            migrationBuilder.DropTable(
                name: "FirmatariClienti",
                schema: "contratti");

            migrationBuilder.DropTable(
                name: "RevisioniCondizioni",
                schema: "contratti");
        }
    }
}
