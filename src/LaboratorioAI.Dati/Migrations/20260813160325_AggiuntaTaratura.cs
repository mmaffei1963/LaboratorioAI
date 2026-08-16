using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class AggiuntaTaratura : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "taratura");

            migrationBuilder.CreateTable(
                name: "CampioniRiferimento",
                schema: "taratura",
                columns: table => new
                {
                    IdCampione = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Identificazione = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    TipoTermometro = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CodiceInterno = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CentroLat = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    NumeroCertificato = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DataTaratura = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DataScadenza = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Risoluzione = table.Column<double>(type: "float", nullable: false),
                    FattoreCoperturaK = table.Column<double>(type: "float", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Attivo = table.Column<bool>(type: "bit", nullable: false),
                    DataCancellazione = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    DataUltimaModifica = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampioniRiferimento", x => x.IdCampione);
                });

            migrationBuilder.CreateTable(
                name: "PuntiCertificato",
                schema: "taratura",
                columns: table => new
                {
                    IdPunto = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdCampione = table.Column<int>(type: "int", nullable: false),
                    TIndicata = table.Column<double>(type: "float", nullable: false),
                    TRiferimento = table.Column<double>(type: "float", nullable: false),
                    Differenza = table.Column<double>(type: "float", nullable: false),
                    IncertezzaEstesa = table.Column<double>(type: "float", nullable: false),
                    IncertezzaTipo = table.Column<double>(type: "float", nullable: false),
                    Ordine = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PuntiCertificato", x => x.IdPunto);
                    table.ForeignKey(
                        name: "FK_PuntiCertificato_CampioniRiferimento_IdCampione",
                        column: x => x.IdCampione,
                        principalSchema: "taratura",
                        principalTable: "CampioniRiferimento",
                        principalColumn: "IdCampione",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Tarature",
                schema: "taratura",
                columns: table => new
                {
                    IdTaratura = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdCampioneRiferimento = table.Column<int>(type: "int", nullable: false),
                    StrumentoIdentificazione = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    StrumentoCodice = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RisoluzioneStrumento = table.Column<double>(type: "float", nullable: false),
                    FattoreCopertura = table.Column<double>(type: "float", nullable: false),
                    IdClienteArcLab = table.Column<int>(type: "int", nullable: true),
                    Committente = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    NumeroRapporto = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DataTaratura = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataScadenza = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RiferimentoIstruzione = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("PK_Tarature", x => x.IdTaratura);
                    table.ForeignKey(
                        name: "FK_Tarature_CampioniRiferimento_IdCampioneRiferimento",
                        column: x => x.IdCampioneRiferimento,
                        principalSchema: "taratura",
                        principalTable: "CampioniRiferimento",
                        principalColumn: "IdCampione",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tarature_Utenti_IdUtente",
                        column: x => x.IdUtente,
                        principalSchema: "sec",
                        principalTable: "Utenti",
                        principalColumn: "IdUtente",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CampioniRiferimento_DataScadenza",
                schema: "taratura",
                table: "CampioniRiferimento",
                column: "DataScadenza");

            migrationBuilder.CreateIndex(
                name: "IX_CampioniRiferimento_Identificazione",
                schema: "taratura",
                table: "CampioniRiferimento",
                column: "Identificazione");

            migrationBuilder.CreateIndex(
                name: "IX_PuntiCertificato_IdCampione",
                schema: "taratura",
                table: "PuntiCertificato",
                column: "IdCampione");

            migrationBuilder.CreateIndex(
                name: "IX_Tarature_DataScadenza",
                schema: "taratura",
                table: "Tarature",
                column: "DataScadenza");

            migrationBuilder.CreateIndex(
                name: "IX_Tarature_DataTaratura",
                schema: "taratura",
                table: "Tarature",
                column: "DataTaratura",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_Tarature_IdCampioneRiferimento",
                schema: "taratura",
                table: "Tarature",
                column: "IdCampioneRiferimento");

            migrationBuilder.CreateIndex(
                name: "IX_Tarature_IdClienteArcLab",
                schema: "taratura",
                table: "Tarature",
                column: "IdClienteArcLab");

            migrationBuilder.CreateIndex(
                name: "IX_Tarature_IdUtente",
                schema: "taratura",
                table: "Tarature",
                column: "IdUtente");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PuntiCertificato",
                schema: "taratura");

            migrationBuilder.DropTable(
                name: "Tarature",
                schema: "taratura");

            migrationBuilder.DropTable(
                name: "CampioniRiferimento",
                schema: "taratura");
        }
    }
}
