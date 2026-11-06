using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class ClientiDaSeguire : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "clienti");

            migrationBuilder.CreateTable(
                name: "AssegnazioniClienti",
                schema: "clienti",
                columns: table => new
                {
                    IdAssegnazioneCliente = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CodiceCliente = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IdCommerciale = table.Column<int>(type: "int", nullable: false),
                    Stato = table.Column<int>(type: "int", nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    FatturatoLabAllaProposta = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    DataProposta = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    PropostaDa = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DataDecisione = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecisaDa = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    NotaDecisione = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DataInizio = table.Column<DateTime>(type: "date", nullable: true),
                    DataFine = table.Column<DateTime>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssegnazioniClienti", x => x.IdAssegnazioneCliente);
                    table.ForeignKey(
                        name: "FK_AssegnazioniClienti_Commerciali_IdCommerciale",
                        column: x => x.IdCommerciale,
                        principalSchema: "provv",
                        principalTable: "Commerciali",
                        principalColumn: "IdCommerciale",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttributiClienti",
                schema: "clienti",
                columns: table => new
                {
                    IdAttributiCliente = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CodiceCliente = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Intermediario = table.Column<bool>(type: "bit", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DataUltimaModifica = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UltimaModificaDa = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttributiClienti", x => x.IdAttributiCliente);
                });

            migrationBuilder.CreateTable(
                name: "AzioniClienti",
                schema: "clienti",
                columns: table => new
                {
                    IdAzioneCliente = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CodiceCliente = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DataAzione = table.Column<DateTime>(type: "date", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Esito = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ProssimoContatto = table.Column<DateTime>(type: "date", nullable: true),
                    IdUtente = table.Column<int>(type: "int", nullable: true),
                    NomeUtente = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    Annullata = table.Column<bool>(type: "bit", nullable: false),
                    AnnullataDa = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DataAnnullamento = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AzioniClienti", x => x.IdAzioneCliente);
                });

            migrationBuilder.CreateTable(
                name: "CampioniMensiliArcLab",
                schema: "clienti",
                columns: table => new
                {
                    IdCampioneMensileArcLab = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CodiceCliente = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Anno = table.Column<int>(type: "int", nullable: false),
                    Mese = table.Column<int>(type: "int", nullable: false),
                    IdControllo = table.Column<int>(type: "int", nullable: true),
                    Controllo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Campioni = table.Column<int>(type: "int", nullable: false),
                    ValoreListino = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    UltimoCampione = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampioniMensiliArcLab", x => x.IdCampioneMensileArcLab);
                });

            migrationBuilder.CreateTable(
                name: "ImpostazioniAndamento",
                schema: "clienti",
                columns: table => new
                {
                    IdImpostazioniAndamento = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MinimoFatturato = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    SogliaRosso = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    SogliaGiallo = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    ValoreAnalisiPerse = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    CampioniTrimestreMinimo = table.Column<int>(type: "int", nullable: false),
                    LegionellaTolleranzaGiorni = table.Column<int>(type: "int", nullable: false),
                    LegionellaPreavvisoGiorni = table.Column<int>(type: "int", nullable: false),
                    SoloConsulenzaMinimo = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    SoloConsulenzaLabMassimo = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    TestoControlloLegionella = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    VociLegionella = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IdBaseCalcoloLaboratorio = table.Column<int>(type: "int", nullable: true),
                    DataUltimaModifica = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UltimaModificaDa = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImpostazioniAndamento", x => x.IdImpostazioniAndamento);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssegnazioniClienti_CodiceCliente",
                schema: "clienti",
                table: "AssegnazioniClienti",
                column: "CodiceCliente");

            migrationBuilder.CreateIndex(
                name: "IX_AssegnazioniClienti_IdCommerciale",
                schema: "clienti",
                table: "AssegnazioniClienti",
                column: "IdCommerciale");

            migrationBuilder.CreateIndex(
                name: "IX_AttributiClienti_CodiceCliente",
                schema: "clienti",
                table: "AttributiClienti",
                column: "CodiceCliente",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AzioniClienti_CodiceCliente",
                schema: "clienti",
                table: "AzioniClienti",
                column: "CodiceCliente");

            migrationBuilder.CreateIndex(
                name: "IX_AzioniClienti_ProssimoContatto",
                schema: "clienti",
                table: "AzioniClienti",
                column: "ProssimoContatto");

            migrationBuilder.CreateIndex(
                name: "IX_CampioniMensiliArcLab_Anno_Mese",
                schema: "clienti",
                table: "CampioniMensiliArcLab",
                columns: new[] { "Anno", "Mese" });

            migrationBuilder.CreateIndex(
                name: "IX_CampioniMensiliArcLab_CodiceCliente",
                schema: "clienti",
                table: "CampioniMensiliArcLab",
                column: "CodiceCliente");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssegnazioniClienti",
                schema: "clienti");

            migrationBuilder.DropTable(
                name: "AttributiClienti",
                schema: "clienti");

            migrationBuilder.DropTable(
                name: "AzioniClienti",
                schema: "clienti");

            migrationBuilder.DropTable(
                name: "CampioniMensiliArcLab",
                schema: "clienti");

            migrationBuilder.DropTable(
                name: "ImpostazioniAndamento",
                schema: "clienti");
        }
    }
}
