using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class ModuloBilance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "bilance");

            migrationBuilder.CreateTable(
                name: "Pesiere",
                schema: "bilance",
                columns: table => new
                {
                    IdPesiera = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Identificazione = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Attivo = table.Column<bool>(type: "bit", nullable: false),
                    DataCancellazione = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    DataUltimaModifica = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pesiere", x => x.IdPesiera);
                });

            migrationBuilder.CreateTable(
                name: "ValidazioniCalcoloBilance",
                schema: "bilance",
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
                    table.PrimaryKey("PK_ValidazioniCalcoloBilance", x => x.IdEsito);
                    table.ForeignKey(
                        name: "FK_ValidazioniCalcoloBilance_Utenti_IdUtente",
                        column: x => x.IdUtente,
                        principalSchema: "sec",
                        principalTable: "Utenti",
                        principalColumn: "IdUtente",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "MasseCampione",
                schema: "bilance",
                columns: table => new
                {
                    IdMassa = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdPesiera = table.Column<int>(type: "int", nullable: false),
                    ValoreNominale = table.Column<double>(type: "float", nullable: false),
                    ValoreConvenzionale = table.Column<double>(type: "float", nullable: false),
                    IncertezzaEstesa = table.Column<double>(type: "float", nullable: false),
                    IncertezzaTipo = table.Column<double>(type: "float", nullable: false),
                    CertificatoLat = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DataCertificato = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DataScadenza = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CentroLat = table.Column<int>(type: "int", nullable: true),
                    Ordine = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MasseCampione", x => x.IdMassa);
                    table.ForeignKey(
                        name: "FK_MasseCampione_Pesiere_IdPesiera",
                        column: x => x.IdPesiera,
                        principalSchema: "bilance",
                        principalTable: "Pesiere",
                        principalColumn: "IdPesiera",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaratureBilance",
                schema: "bilance",
                columns: table => new
                {
                    IdTaratura = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdPesiera = table.Column<int>(type: "int", nullable: true),
                    BilanciaIdentificazione = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    BilanciaModello = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    BilanciaInterna = table.Column<bool>(type: "bit", nullable: false),
                    BilanciaIdStrumentoArcLab = table.Column<int>(type: "int", nullable: true),
                    BilanciaIdClienteArcLab = table.Column<int>(type: "int", nullable: true),
                    Portata = table.Column<double>(type: "float", nullable: false),
                    UnitaFormato = table.Column<double>(type: "float", nullable: false),
                    CoefficienteSensibilitaTermica = table.Column<double>(type: "float", nullable: false),
                    MaxIncertezzaAmmessa = table.Column<double>(type: "float", nullable: false),
                    TemperaturaTaratura = table.Column<double>(type: "float", nullable: false),
                    TemperaturaUtilizzo = table.Column<double>(type: "float", nullable: false),
                    NumeroRapporto = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DataTaratura = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataScadenza = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RiferimentoIstruzione = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Responsabile = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    DatiProve = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                    table.PrimaryKey("PK_TaratureBilance", x => x.IdTaratura);
                    table.ForeignKey(
                        name: "FK_TaratureBilance_Pesiere_IdPesiera",
                        column: x => x.IdPesiera,
                        principalSchema: "bilance",
                        principalTable: "Pesiere",
                        principalColumn: "IdPesiera",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TaratureBilance_Utenti_IdUtente",
                        column: x => x.IdUtente,
                        principalSchema: "sec",
                        principalTable: "Utenti",
                        principalColumn: "IdUtente",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AnalisiBilance",
                schema: "bilance",
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
                    table.PrimaryKey("PK_AnalisiBilance", x => x.IdAnalisi);
                    table.ForeignKey(
                        name: "FK_AnalisiBilance_TaratureBilance_IdTaratura",
                        column: x => x.IdTaratura,
                        principalSchema: "bilance",
                        principalTable: "TaratureBilance",
                        principalColumn: "IdTaratura",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AnalisiBilance_Utenti_IdUtente",
                        column: x => x.IdUtente,
                        principalSchema: "sec",
                        principalTable: "Utenti",
                        principalColumn: "IdUtente",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnalisiBilance_IdTaratura_Firma",
                schema: "bilance",
                table: "AnalisiBilance",
                columns: new[] { "IdTaratura", "Firma" });

            migrationBuilder.CreateIndex(
                name: "IX_AnalisiBilance_IdUtente",
                schema: "bilance",
                table: "AnalisiBilance",
                column: "IdUtente");

            migrationBuilder.CreateIndex(
                name: "IX_MasseCampione_IdPesiera",
                schema: "bilance",
                table: "MasseCampione",
                column: "IdPesiera");

            migrationBuilder.CreateIndex(
                name: "IX_Pesiere_Identificazione",
                schema: "bilance",
                table: "Pesiere",
                column: "Identificazione");

            migrationBuilder.CreateIndex(
                name: "IX_TaratureBilance_DataScadenza",
                schema: "bilance",
                table: "TaratureBilance",
                column: "DataScadenza");

            migrationBuilder.CreateIndex(
                name: "IX_TaratureBilance_DataTaratura",
                schema: "bilance",
                table: "TaratureBilance",
                column: "DataTaratura",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_TaratureBilance_IdPesiera",
                schema: "bilance",
                table: "TaratureBilance",
                column: "IdPesiera");

            migrationBuilder.CreateIndex(
                name: "IX_TaratureBilance_IdUtente",
                schema: "bilance",
                table: "TaratureBilance",
                column: "IdUtente");

            migrationBuilder.CreateIndex(
                name: "IX_ValidazioniCalcoloBilance_DataEsecuzione",
                schema: "bilance",
                table: "ValidazioniCalcoloBilance",
                column: "DataEsecuzione",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_ValidazioniCalcoloBilance_IdUtente",
                schema: "bilance",
                table: "ValidazioniCalcoloBilance",
                column: "IdUtente");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnalisiBilance",
                schema: "bilance");

            migrationBuilder.DropTable(
                name: "MasseCampione",
                schema: "bilance");

            migrationBuilder.DropTable(
                name: "ValidazioniCalcoloBilance",
                schema: "bilance");

            migrationBuilder.DropTable(
                name: "TaratureBilance",
                schema: "bilance");

            migrationBuilder.DropTable(
                name: "Pesiere",
                schema: "bilance");
        }
    }
}
