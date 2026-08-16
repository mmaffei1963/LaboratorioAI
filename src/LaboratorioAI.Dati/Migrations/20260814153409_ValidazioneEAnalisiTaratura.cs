using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class ValidazioneEAnalisiTaratura : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnalisiTarature",
                schema: "taratura",
                columns: table => new
                {
                    IdAnalisi = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdTaratura = table.Column<int>(type: "int", nullable: false),
                    DataAnalisi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Firma = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AnomalieDeterministiche = table.Column<bool>(type: "bit", nullable: false),
                    Controlli = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Esito = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Testo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModelloAi = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TokenInput = table.Column<int>(type: "int", nullable: false),
                    TokenOutput = table.Column<int>(type: "int", nullable: false),
                    VersioneApplicazione = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IdUtente = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalisiTarature", x => x.IdAnalisi);
                    table.ForeignKey(
                        name: "FK_AnalisiTarature_Tarature_IdTaratura",
                        column: x => x.IdTaratura,
                        principalSchema: "taratura",
                        principalTable: "Tarature",
                        principalColumn: "IdTaratura",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AnalisiTarature_Utenti_IdUtente",
                        column: x => x.IdUtente,
                        principalSchema: "sec",
                        principalTable: "Utenti",
                        principalColumn: "IdUtente",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ValidazioniCalcolo",
                schema: "taratura",
                columns: table => new
                {
                    IdEsito = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DataEsecuzione = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Validato = table.Column<bool>(type: "bit", nullable: false),
                    NumeroOk = table.Column<int>(type: "int", nullable: false),
                    NumeroKo = table.Column<int>(type: "int", nullable: false),
                    Tolleranza = table.Column<double>(type: "float", nullable: false),
                    VersioneSet = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    VersioneApplicazione = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Dettaglio = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IdUtente = table.Column<int>(type: "int", nullable: true),
                    Operatore = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValidazioniCalcolo", x => x.IdEsito);
                    table.ForeignKey(
                        name: "FK_ValidazioniCalcolo_Utenti_IdUtente",
                        column: x => x.IdUtente,
                        principalSchema: "sec",
                        principalTable: "Utenti",
                        principalColumn: "IdUtente",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnalisiTarature_IdTaratura_Firma",
                schema: "taratura",
                table: "AnalisiTarature",
                columns: new[] { "IdTaratura", "Firma" });

            migrationBuilder.CreateIndex(
                name: "IX_AnalisiTarature_IdUtente",
                schema: "taratura",
                table: "AnalisiTarature",
                column: "IdUtente");

            migrationBuilder.CreateIndex(
                name: "IX_ValidazioniCalcolo_DataEsecuzione",
                schema: "taratura",
                table: "ValidazioniCalcolo",
                column: "DataEsecuzione",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_ValidazioniCalcolo_IdUtente",
                schema: "taratura",
                table: "ValidazioniCalcolo",
                column: "IdUtente");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnalisiTarature",
                schema: "taratura");

            migrationBuilder.DropTable(
                name: "ValidazioniCalcolo",
                schema: "taratura");
        }
    }
}
