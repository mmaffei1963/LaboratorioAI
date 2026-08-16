using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class ModuloIncubatori : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "IncertezzaInstabilita",
                schema: "taratura",
                table: "CampioniRiferimento",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.CreateTable(
                name: "TaratureIncubatori",
                schema: "taratura",
                columns: table => new
                {
                    IdTaratura = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdCampioneRiferimento = table.Column<int>(type: "int", nullable: false),
                    MisuratoreIdentificazione = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    MisuratoreCodice = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MisuratoreInterno = table.Column<bool>(type: "bit", nullable: false),
                    MisuratoreIdStrumentoArcLab = table.Column<int>(type: "int", nullable: true),
                    MisuratoreIdClienteArcLab = table.Column<int>(type: "int", nullable: true),
                    ApparecchiaturaIdentificazione = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ApparecchiaturaCodice = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ApparecchiaturaInterna = table.Column<bool>(type: "bit", nullable: false),
                    ApparecchiaturaIdStrumentoArcLab = table.Column<int>(type: "int", nullable: true),
                    ApparecchiaturaIdClienteArcLab = table.Column<int>(type: "int", nullable: true),
                    ValoreNominale = table.Column<double>(type: "float", nullable: false),
                    Tolleranza = table.Column<double>(type: "float", nullable: false),
                    RisoluzioneStrumento = table.Column<double>(type: "float", nullable: false),
                    DispersioneMappatura = table.Column<double>(type: "float", nullable: false),
                    NumeroRapporto = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DataTaratura = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_TaratureIncubatori", x => x.IdTaratura);
                    table.ForeignKey(
                        name: "FK_TaratureIncubatori_CampioniRiferimento_IdCampioneRiferimento",
                        column: x => x.IdCampioneRiferimento,
                        principalSchema: "taratura",
                        principalTable: "CampioniRiferimento",
                        principalColumn: "IdCampione",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaratureIncubatori_Utenti_IdUtente",
                        column: x => x.IdUtente,
                        principalSchema: "sec",
                        principalTable: "Utenti",
                        principalColumn: "IdUtente",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ValidazioniCalcoloIncubatori",
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
                    table.PrimaryKey("PK_ValidazioniCalcoloIncubatori", x => x.IdEsito);
                    table.ForeignKey(
                        name: "FK_ValidazioniCalcoloIncubatori_Utenti_IdUtente",
                        column: x => x.IdUtente,
                        principalSchema: "sec",
                        principalTable: "Utenti",
                        principalColumn: "IdUtente",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AnalisiIncubatori",
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
                    table.PrimaryKey("PK_AnalisiIncubatori", x => x.IdAnalisi);
                    table.ForeignKey(
                        name: "FK_AnalisiIncubatori_TaratureIncubatori_IdTaratura",
                        column: x => x.IdTaratura,
                        principalSchema: "taratura",
                        principalTable: "TaratureIncubatori",
                        principalColumn: "IdTaratura",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AnalisiIncubatori_Utenti_IdUtente",
                        column: x => x.IdUtente,
                        principalSchema: "sec",
                        principalTable: "Utenti",
                        principalColumn: "IdUtente",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnalisiIncubatori_IdTaratura_Firma",
                schema: "taratura",
                table: "AnalisiIncubatori",
                columns: new[] { "IdTaratura", "Firma" });

            migrationBuilder.CreateIndex(
                name: "IX_AnalisiIncubatori_IdUtente",
                schema: "taratura",
                table: "AnalisiIncubatori",
                column: "IdUtente");

            migrationBuilder.CreateIndex(
                name: "IX_TaratureIncubatori_DataScadenza",
                schema: "taratura",
                table: "TaratureIncubatori",
                column: "DataScadenza");

            migrationBuilder.CreateIndex(
                name: "IX_TaratureIncubatori_DataTaratura",
                schema: "taratura",
                table: "TaratureIncubatori",
                column: "DataTaratura",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_TaratureIncubatori_IdCampioneRiferimento",
                schema: "taratura",
                table: "TaratureIncubatori",
                column: "IdCampioneRiferimento");

            migrationBuilder.CreateIndex(
                name: "IX_TaratureIncubatori_IdUtente",
                schema: "taratura",
                table: "TaratureIncubatori",
                column: "IdUtente");

            migrationBuilder.CreateIndex(
                name: "IX_ValidazioniCalcoloIncubatori_DataEsecuzione",
                schema: "taratura",
                table: "ValidazioniCalcoloIncubatori",
                column: "DataEsecuzione",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_ValidazioniCalcoloIncubatori_IdUtente",
                schema: "taratura",
                table: "ValidazioniCalcoloIncubatori",
                column: "IdUtente");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnalisiIncubatori",
                schema: "taratura");

            migrationBuilder.DropTable(
                name: "ValidazioniCalcoloIncubatori",
                schema: "taratura");

            migrationBuilder.DropTable(
                name: "TaratureIncubatori",
                schema: "taratura");

            migrationBuilder.DropColumn(
                name: "IncertezzaInstabilita",
                schema: "taratura",
                table: "CampioniRiferimento");
        }
    }
}
