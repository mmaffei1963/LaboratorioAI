using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class ModuloMappatura : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Mappature",
                schema: "taratura",
                columns: table => new
                {
                    IdMappatura = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdCampioneRiferimento = table.Column<int>(type: "int", nullable: false),
                    ApparecchiaturaIdentificazione = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ApparecchiaturaCodice = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ApparecchiaturaInterna = table.Column<bool>(type: "bit", nullable: false),
                    ApparecchiaturaIdStrumentoArcLab = table.Column<int>(type: "int", nullable: true),
                    ApparecchiaturaIdClienteArcLab = table.Column<int>(type: "int", nullable: true),
                    ValoreNominale = table.Column<double>(type: "float", nullable: false),
                    Tolleranza = table.Column<double>(type: "float", nullable: false),
                    RisoluzioneCdr = table.Column<double>(type: "float", nullable: false),
                    NumeroRapporto = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DataMappatura = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataScadenza = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RiferimentoIstruzione = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Responsabile = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    DatiMisure = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Attivo = table.Column<bool>(type: "bit", nullable: false),
                    DataCancellazione = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    DataUltimaModifica = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    IdUtente = table.Column<int>(type: "int", nullable: true),
                    VersioneApplicazione = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mappature", x => x.IdMappatura);
                    table.ForeignKey(
                        name: "FK_Mappature_CampioniRiferimento_IdCampioneRiferimento",
                        column: x => x.IdCampioneRiferimento,
                        principalSchema: "taratura",
                        principalTable: "CampioniRiferimento",
                        principalColumn: "IdCampione",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Mappature_Utenti_IdUtente",
                        column: x => x.IdUtente,
                        principalSchema: "sec",
                        principalTable: "Utenti",
                        principalColumn: "IdUtente",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ValidazioniCalcoloMappatura",
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
                    table.PrimaryKey("PK_ValidazioniCalcoloMappatura", x => x.IdEsito);
                    table.ForeignKey(
                        name: "FK_ValidazioniCalcoloMappatura_Utenti_IdUtente",
                        column: x => x.IdUtente,
                        principalSchema: "sec",
                        principalTable: "Utenti",
                        principalColumn: "IdUtente",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AnalisiMappature",
                schema: "taratura",
                columns: table => new
                {
                    IdAnalisi = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdMappatura = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_AnalisiMappature", x => x.IdAnalisi);
                    table.ForeignKey(
                        name: "FK_AnalisiMappature_Mappature_IdMappatura",
                        column: x => x.IdMappatura,
                        principalSchema: "taratura",
                        principalTable: "Mappature",
                        principalColumn: "IdMappatura",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AnalisiMappature_Utenti_IdUtente",
                        column: x => x.IdUtente,
                        principalSchema: "sec",
                        principalTable: "Utenti",
                        principalColumn: "IdUtente",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnalisiMappature_IdMappatura_Firma",
                schema: "taratura",
                table: "AnalisiMappature",
                columns: new[] { "IdMappatura", "Firma" });

            migrationBuilder.CreateIndex(
                name: "IX_AnalisiMappature_IdUtente",
                schema: "taratura",
                table: "AnalisiMappature",
                column: "IdUtente");

            migrationBuilder.CreateIndex(
                name: "IX_Mappature_DataMappatura",
                schema: "taratura",
                table: "Mappature",
                column: "DataMappatura",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_Mappature_DataScadenza",
                schema: "taratura",
                table: "Mappature",
                column: "DataScadenza");

            migrationBuilder.CreateIndex(
                name: "IX_Mappature_IdCampioneRiferimento",
                schema: "taratura",
                table: "Mappature",
                column: "IdCampioneRiferimento");

            migrationBuilder.CreateIndex(
                name: "IX_Mappature_IdUtente",
                schema: "taratura",
                table: "Mappature",
                column: "IdUtente");

            migrationBuilder.CreateIndex(
                name: "IX_ValidazioniCalcoloMappatura_DataEsecuzione",
                schema: "taratura",
                table: "ValidazioniCalcoloMappatura",
                column: "DataEsecuzione",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_ValidazioniCalcoloMappatura_IdUtente",
                schema: "taratura",
                table: "ValidazioniCalcoloMappatura",
                column: "IdUtente");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnalisiMappature",
                schema: "taratura");

            migrationBuilder.DropTable(
                name: "ValidazioniCalcoloMappatura",
                schema: "taratura");

            migrationBuilder.DropTable(
                name: "Mappature",
                schema: "taratura");
        }
    }
}
