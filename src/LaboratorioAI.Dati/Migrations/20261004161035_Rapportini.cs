using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class Rapportini : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "cons");

            migrationBuilder.CreateTable(
                name: "ConsulentiUtenti",
                schema: "cons",
                columns: table => new
                {
                    IdConsulenteUtente = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdUtente = table.Column<int>(type: "int", nullable: false),
                    IdOrganicoArcLab = table.Column<int>(type: "int", nullable: false),
                    DataUltimaModifica = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsulentiUtenti", x => x.IdConsulenteUtente);
                });

            migrationBuilder.CreateTable(
                name: "Rapportini",
                schema: "cons",
                columns: table => new
                {
                    IdRapportino = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdClienteArcLab = table.Column<int>(type: "int", nullable: false),
                    RagioneSociale = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IdInsediamentoArcLab = table.Column<int>(type: "int", nullable: false),
                    NomeInsediamento = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    IndirizzoInsediamento = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    EmailInsediamento = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    DataIntervento = table.Column<DateTime>(type: "date", nullable: false),
                    IdOrganicoArcLab = table.Column<int>(type: "int", nullable: false),
                    Consulente = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CognomeConsulente = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CommessaConsulente = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IdCommessaArcLab = table.Column<int>(type: "int", nullable: true),
                    DescrizioneCommessa = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    IdTipoIncarico = table.Column<int>(type: "int", nullable: true),
                    PrezzoCommessa = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    Voce = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DescrizioneFattura = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Ore = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    Luogo = table.Column<int>(type: "int", nullable: false),
                    ModalitaImporto = table.Column<int>(type: "int", nullable: false),
                    TariffaOraria = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    Importo = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    ImportoCorretto = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    MotivoCorrezione = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IdUtenteCorrezione = table.Column<int>(type: "int", nullable: true),
                    DataCorrezione = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModalitaFatturazione = table.Column<int>(type: "int", nullable: false),
                    Prepagato = table.Column<bool>(type: "bit", nullable: false),
                    DataUltimoScalo = table.Column<DateTime>(type: "date", nullable: true),
                    OreResiduePrima = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    NoteInterne = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Stato = table.Column<int>(type: "int", nullable: false),
                    IdUtenteCreazione = table.Column<int>(type: "int", nullable: true),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    DataUltimaModifica = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataInvio = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DataContestazione = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NotaContestazione = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DataConferma = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IdUtenteConferma = table.Column<int>(type: "int", nullable: true),
                    DataAnnullamento = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MotivoAnnullamento = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IdLottoPrefatturato = table.Column<int>(type: "int", nullable: true),
                    NomeFilePdf = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PercorsoPdf = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FilePalomar = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rapportini", x => x.IdRapportino);
                });

            migrationBuilder.CreateTable(
                name: "VociIncarico",
                schema: "cons",
                columns: table => new
                {
                    IdVoceIncarico = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdTipoIncaricoDa = table.Column<int>(type: "int", nullable: false),
                    IdTipoIncaricoA = table.Column<int>(type: "int", nullable: false),
                    Voce = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Attivo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VociIncarico", x => x.IdVoceIncarico);
                });

            migrationBuilder.InsertData(
                schema: "cons",
                table: "VociIncarico",
                columns: new[] { "IdVoceIncarico", "Attivo", "IdTipoIncaricoA", "IdTipoIncaricoDa", "Voce" },
                values: new object[,]
                {
                    { 1, true, 94, 93, "626" },
                    { 2, true, 95, 95, "RSP" },
                    { 3, true, 111, 96, "626" },
                    { 4, true, 113, 112, "HAC" },
                    { 5, true, 117, 114, "AT" },
                    { 6, true, 123, 120, "RIF" },
                    { 7, true, 125, 124, "PRY" },
                    { 8, true, 153, 126, "F62" },
                    { 9, true, 158, 154, "FHA" },
                    { 10, true, 183, 159, "FON" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConsulentiUtenti_IdUtente",
                schema: "cons",
                table: "ConsulentiUtenti",
                column: "IdUtente",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rapportini_IdClienteArcLab_DataIntervento",
                schema: "cons",
                table: "Rapportini",
                columns: new[] { "IdClienteArcLab", "DataIntervento" });

            migrationBuilder.CreateIndex(
                name: "IX_Rapportini_IdOrganicoArcLab_DataIntervento",
                schema: "cons",
                table: "Rapportini",
                columns: new[] { "IdOrganicoArcLab", "DataIntervento" });

            migrationBuilder.CreateIndex(
                name: "IX_Rapportini_Stato",
                schema: "cons",
                table: "Rapportini",
                column: "Stato");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConsulentiUtenti",
                schema: "cons");

            migrationBuilder.DropTable(
                name: "Rapportini",
                schema: "cons");

            migrationBuilder.DropTable(
                name: "VociIncarico",
                schema: "cons");
        }
    }
}
