using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class CompensiSociEAnalisiEsterne : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "soci");

            migrationBuilder.AddColumn<decimal>(
                name: "PercCampionamentoEsterne",
                schema: "provv",
                table: "ContrattiConsulenti",
                type: "decimal(6,3)",
                precision: 6,
                scale: 3,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AnniCompensi",
                schema: "soci",
                columns: table => new
                {
                    IdAnnoCompensi = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Anno = table.Column<int>(type: "int", nullable: false),
                    BaseTempoPieno = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OreTempoPieno = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    CompensoAmministratore = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CompensoResponsabileArea = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AssicurazioneAuto = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Premio = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PercentualeRspp = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    PercentualeIndennitaLaboratorio = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    BaseLaboratorioManuale = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    NotaBaseLaboratorio = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Chiuso = table.Column<bool>(type: "bit", nullable: false),
                    DataChiusura = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ChiusoDa = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    BaseLaboratorioAzwChiusura = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataUltimaModifica = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnniCompensi", x => x.IdAnnoCompensi);
                });

            migrationBuilder.CreateTable(
                name: "CategorieRimborsi",
                schema: "soci",
                columns: table => new
                {
                    IdCategoriaRimborso = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Maggiorazione = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    ConKm = table.Column<bool>(type: "bit", nullable: false),
                    Ordine = table.Column<int>(type: "int", nullable: false),
                    Attivo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategorieRimborsi", x => x.IdCategoriaRimborso);
                });

            migrationBuilder.CreateTable(
                name: "PercentualiVociLaboratorio",
                schema: "soci",
                columns: table => new
                {
                    IdPercentualeVoceLaboratorio = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Anno = table.Column<int>(type: "int", nullable: false),
                    Voce = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Percentuale = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    Attivo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PercentualiVociLaboratorio", x => x.IdPercentualeVoceLaboratorio);
                });

            migrationBuilder.CreateTable(
                name: "Soci",
                schema: "soci",
                columns: table => new
                {
                    IdSocio = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    VoceCompensiAzw = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CommessaRsppAzw = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Gestore = table.Column<bool>(type: "bit", nullable: false),
                    Ordine = table.Column<int>(type: "int", nullable: false),
                    Attivo = table.Column<bool>(type: "bit", nullable: false),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataUltimaModifica = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Soci", x => x.IdSocio);
                });

            migrationBuilder.CreateTable(
                name: "CompensiSociAnno",
                schema: "soci",
                columns: table => new
                {
                    IdCompensoSocioAnno = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Anno = table.Column<int>(type: "int", nullable: false),
                    IdSocio = table.Column<int>(type: "int", nullable: false),
                    OreSettimanali = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    MesiAmministratore = table.Column<int>(type: "int", nullable: false),
                    ResponsabileArea = table.Column<bool>(type: "bit", nullable: false),
                    PercentualeAutoPropria = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    QuotaRspp = table.Column<bool>(type: "bit", nullable: false),
                    IndennitaLaboratorio = table.Column<bool>(type: "bit", nullable: false),
                    Premio = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    RecuperoManuale = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    RettificaPercepito = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    NotaRettificaPercepito = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RsppAzwChiusura = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    PercepitoAzwChiusura = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataUltimaModifica = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompensiSociAnno", x => x.IdCompensoSocioAnno);
                    table.ForeignKey(
                        name: "FK_CompensiSociAnno_Soci_IdSocio",
                        column: x => x.IdSocio,
                        principalSchema: "soci",
                        principalTable: "Soci",
                        principalColumn: "IdSocio",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PremiAreaLaboratorio",
                schema: "soci",
                columns: table => new
                {
                    IdPremioAreaLaboratorio = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Anno = table.Column<int>(type: "int", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Importo = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IdSocio = table.Column<int>(type: "int", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Attivo = table.Column<bool>(type: "bit", nullable: false),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PremiAreaLaboratorio", x => x.IdPremioAreaLaboratorio);
                    table.ForeignKey(
                        name: "FK_PremiAreaLaboratorio_Soci_IdSocio",
                        column: x => x.IdSocio,
                        principalSchema: "soci",
                        principalTable: "Soci",
                        principalColumn: "IdSocio",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RimborsiSpese",
                schema: "soci",
                columns: table => new
                {
                    IdRimborsoSpesa = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdSocio = table.Column<int>(type: "int", nullable: false),
                    Data = table.Column<DateTime>(type: "date", nullable: false),
                    IdCategoriaRimborso = table.Column<int>(type: "int", nullable: false),
                    Descrizione = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Importo = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Maggiorazione = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    Km = table.Column<decimal>(type: "decimal(10,1)", precision: 10, scale: 1, nullable: true),
                    Attivo = table.Column<bool>(type: "bit", nullable: false),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataUltimaModifica = table.Column<DateTime>(type: "datetime2", nullable: false),
                    InseritoDa = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RimborsiSpese", x => x.IdRimborsoSpesa);
                    table.ForeignKey(
                        name: "FK_RimborsiSpese_CategorieRimborsi_IdCategoriaRimborso",
                        column: x => x.IdCategoriaRimborso,
                        principalSchema: "soci",
                        principalTable: "CategorieRimborsi",
                        principalColumn: "IdCategoriaRimborso",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RimborsiSpese_Soci_IdSocio",
                        column: x => x.IdSocio,
                        principalSchema: "soci",
                        principalTable: "Soci",
                        principalColumn: "IdSocio",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RicevuteRimborsi",
                schema: "soci",
                columns: table => new
                {
                    IdRicevutaRimborso = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdRimborsoSpesa = table.Column<int>(type: "int", nullable: false),
                    NomeFile = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    TipoContenuto = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Dimensione = table.Column<int>(type: "int", nullable: false),
                    Contenuto = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    DataCaricamento = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RicevuteRimborsi", x => x.IdRicevutaRimborso);
                    table.ForeignKey(
                        name: "FK_RicevuteRimborsi_RimborsiSpese_IdRimborsoSpesa",
                        column: x => x.IdRimborsoSpesa,
                        principalSchema: "soci",
                        principalTable: "RimborsiSpese",
                        principalColumn: "IdRimborsoSpesa",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnniCompensi_Anno",
                schema: "soci",
                table: "AnniCompensi",
                column: "Anno",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompensiSociAnno_Anno_IdSocio",
                schema: "soci",
                table: "CompensiSociAnno",
                columns: new[] { "Anno", "IdSocio" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompensiSociAnno_IdSocio",
                schema: "soci",
                table: "CompensiSociAnno",
                column: "IdSocio");

            migrationBuilder.CreateIndex(
                name: "IX_PercentualiVociLaboratorio_Anno_Voce",
                schema: "soci",
                table: "PercentualiVociLaboratorio",
                columns: new[] { "Anno", "Voce" });

            migrationBuilder.CreateIndex(
                name: "IX_PremiAreaLaboratorio_Anno",
                schema: "soci",
                table: "PremiAreaLaboratorio",
                column: "Anno");

            migrationBuilder.CreateIndex(
                name: "IX_PremiAreaLaboratorio_IdSocio",
                schema: "soci",
                table: "PremiAreaLaboratorio",
                column: "IdSocio");

            migrationBuilder.CreateIndex(
                name: "IX_RicevuteRimborsi_IdRimborsoSpesa",
                schema: "soci",
                table: "RicevuteRimborsi",
                column: "IdRimborsoSpesa",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RimborsiSpese_IdCategoriaRimborso",
                schema: "soci",
                table: "RimborsiSpese",
                column: "IdCategoriaRimborso");

            migrationBuilder.CreateIndex(
                name: "IX_RimborsiSpese_IdSocio_Data",
                schema: "soci",
                table: "RimborsiSpese",
                columns: new[] { "IdSocio", "Data" });

            migrationBuilder.CreateIndex(
                name: "IX_Soci_Email",
                schema: "soci",
                table: "Soci",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnniCompensi",
                schema: "soci");

            migrationBuilder.DropTable(
                name: "CompensiSociAnno",
                schema: "soci");

            migrationBuilder.DropTable(
                name: "PercentualiVociLaboratorio",
                schema: "soci");

            migrationBuilder.DropTable(
                name: "PremiAreaLaboratorio",
                schema: "soci");

            migrationBuilder.DropTable(
                name: "RicevuteRimborsi",
                schema: "soci");

            migrationBuilder.DropTable(
                name: "RimborsiSpese",
                schema: "soci");

            migrationBuilder.DropTable(
                name: "CategorieRimborsi",
                schema: "soci");

            migrationBuilder.DropTable(
                name: "Soci",
                schema: "soci");

            migrationBuilder.DropColumn(
                name: "PercCampionamentoEsterne",
                schema: "provv",
                table: "ContrattiConsulenti");
        }
    }
}
