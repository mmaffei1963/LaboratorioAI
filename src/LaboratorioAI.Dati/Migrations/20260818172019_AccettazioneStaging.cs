using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class AccettazioneStaging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "accettazione");

            migrationBuilder.CreateTable(
                name: "Lotti",
                schema: "accettazione",
                columns: table => new
                {
                    IdAccettazioneLotto = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CodiceLocale = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Stato = table.Column<int>(type: "int", nullable: false),
                    IdClienteArcLab = table.Column<int>(type: "int", nullable: true),
                    Cliente = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    IdInsediamentoArcLab = table.Column<int>(type: "int", nullable: true),
                    Insediamento = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    IdCommessaArcLab = table.Column<int>(type: "int", nullable: true),
                    Commessa = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    DataConferimento = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IdPrelevatore = table.Column<int>(type: "int", nullable: true),
                    Prelevatore = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    IdRicevitore = table.Column<int>(type: "int", nullable: true),
                    Ricevitore = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Origine = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DataTrasmissione = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NomeFileTracciato = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Attivo = table.Column<bool>(type: "bit", nullable: false),
                    DataCancellazione = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    DataUltimaModifica = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    IdUtente = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lotti", x => x.IdAccettazioneLotto);
                });

            migrationBuilder.CreateTable(
                name: "Campioni",
                schema: "accettazione",
                columns: table => new
                {
                    IdAccettazioneCampione = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdAccettazioneLotto = table.Column<int>(type: "int", nullable: false),
                    Progressivo = table.Column<int>(type: "int", nullable: false),
                    IdMatriceArcLab = table.Column<int>(type: "int", nullable: true),
                    Matrice = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Descrizione = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DataPrelievo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OraPrelievo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    PuntoPrelievo = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Temperatura = table.Column<double>(type: "float", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Campioni", x => x.IdAccettazioneCampione);
                    table.ForeignKey(
                        name: "FK_Campioni_Lotti_IdAccettazioneLotto",
                        column: x => x.IdAccettazioneLotto,
                        principalSchema: "accettazione",
                        principalTable: "Lotti",
                        principalColumn: "IdAccettazioneLotto",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Prove",
                schema: "accettazione",
                columns: table => new
                {
                    IdAccettazioneProva = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdAccettazioneCampione = table.Column<int>(type: "int", nullable: false),
                    IdControlloArcLab = table.Column<int>(type: "int", nullable: true),
                    Controllo = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    IdMetodoArcLab = table.Column<int>(type: "int", nullable: true),
                    Metodo = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prove", x => x.IdAccettazioneProva);
                    table.ForeignKey(
                        name: "FK_Prove_Campioni_IdAccettazioneCampione",
                        column: x => x.IdAccettazioneCampione,
                        principalSchema: "accettazione",
                        principalTable: "Campioni",
                        principalColumn: "IdAccettazioneCampione",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Campioni_IdAccettazioneLotto",
                schema: "accettazione",
                table: "Campioni",
                column: "IdAccettazioneLotto");

            migrationBuilder.CreateIndex(
                name: "IX_Lotti_CodiceLocale",
                schema: "accettazione",
                table: "Lotti",
                column: "CodiceLocale",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Lotti_Stato_Attivo",
                schema: "accettazione",
                table: "Lotti",
                columns: new[] { "Stato", "Attivo" });

            migrationBuilder.CreateIndex(
                name: "IX_Prove_IdAccettazioneCampione",
                schema: "accettazione",
                table: "Prove",
                column: "IdAccettazioneCampione");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Prove",
                schema: "accettazione");

            migrationBuilder.DropTable(
                name: "Campioni",
                schema: "accettazione");

            migrationBuilder.DropTable(
                name: "Lotti",
                schema: "accettazione");
        }
    }
}
