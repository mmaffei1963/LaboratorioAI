using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class SpettanzeConsulenti : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Commessa",
                schema: "provv",
                table: "AzwRigheNoteCredito",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Commessa",
                schema: "provv",
                table: "AzwRigheFattura",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AzwFattureConsulenti",
                schema: "provv",
                columns: table => new
                {
                    IdAzwFatturaConsulente = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CodiceFornitore = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Documento = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    DataRegistrazione = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Centro = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Voce = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Importo = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AzwFattureConsulenti", x => x.IdAzwFatturaConsulente);
                });

            migrationBuilder.CreateTable(
                name: "ConsulentiPercentuale",
                schema: "provv",
                columns: table => new
                {
                    IdConsulentePercentuale = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CodiceZona = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CodiceCommessa = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    VoceCosto = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CodiceFornitore = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CentriDocenzeOrarie = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IdUtente = table.Column<int>(type: "int", nullable: true),
                    Attivo = table.Column<bool>(type: "bit", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsulentiPercentuale", x => x.IdConsulentePercentuale);
                });

            migrationBuilder.CreateTable(
                name: "VociGruppiSpettanze",
                schema: "provv",
                columns: table => new
                {
                    IdVociGruppoSpettanze = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Gruppo = table.Column<int>(type: "int", nullable: false),
                    Voci = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ModificatoDa = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DataModifica = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VociGruppiSpettanze", x => x.IdVociGruppoSpettanze);
                });

            migrationBuilder.CreateTable(
                name: "AnniSpettanzeChiusi",
                schema: "provv",
                columns: table => new
                {
                    IdAnnoSpettanzeChiuso = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdConsulentePercentuale = table.Column<int>(type: "int", nullable: false),
                    Anno = table.Column<int>(type: "int", nullable: false),
                    Maturato = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    Acconti = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    DocenzeOrarie = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    Conguaglio = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    DataChiusura = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ChiusoDa = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DataImportazioneAzw = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnniSpettanzeChiusi", x => x.IdAnnoSpettanzeChiuso);
                    table.ForeignKey(
                        name: "FK_AnniSpettanzeChiusi_ConsulentiPercentuale_IdConsulentePercentuale",
                        column: x => x.IdConsulentePercentuale,
                        principalSchema: "provv",
                        principalTable: "ConsulentiPercentuale",
                        principalColumn: "IdConsulentePercentuale",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContrattiConsulenti",
                schema: "provv",
                columns: table => new
                {
                    IdContrattoConsulente = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdConsulentePercentuale = table.Column<int>(type: "int", nullable: false),
                    ValidoDal = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ValidoAl = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Riferimento = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PercConsulenza = table.Column<decimal>(type: "decimal(6,3)", precision: 6, scale: 3, nullable: false),
                    PercCampionamento = table.Column<decimal>(type: "decimal(6,3)", precision: 6, scale: 3, nullable: false),
                    PercAnalisiClienti = table.Column<decimal>(type: "decimal(6,3)", precision: 6, scale: 3, nullable: false),
                    PercPrimoAnnoServizi = table.Column<decimal>(type: "decimal(6,3)", precision: 6, scale: 3, nullable: false),
                    PercFormazioneAltri = table.Column<decimal>(type: "decimal(6,3)", precision: 6, scale: 3, nullable: false),
                    PercDocenza = table.Column<decimal>(type: "decimal(6,3)", precision: 6, scale: 3, nullable: false),
                    PercTarature = table.Column<decimal>(type: "decimal(6,3)", precision: 6, scale: 3, nullable: true),
                    MesiPrimoAnno = table.Column<int>(type: "int", nullable: false),
                    Annullato = table.Column<bool>(type: "bit", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ModificatoDa = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DataModifica = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContrattiConsulenti", x => x.IdContrattoConsulente);
                    table.ForeignKey(
                        name: "FK_ContrattiConsulenti_ConsulentiPercentuale_IdConsulentePercentuale",
                        column: x => x.IdConsulentePercentuale,
                        principalSchema: "provv",
                        principalTable: "ConsulentiPercentuale",
                        principalColumn: "IdConsulentePercentuale",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MovimentiSpettanze",
                schema: "provv",
                columns: table => new
                {
                    IdMovimentoSpettanza = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdAnnoSpettanzeChiuso = table.Column<int>(type: "int", nullable: false),
                    Data = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Voce = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    CodiceCliente = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Documento = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    VoceAzw = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Commessa = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Origine = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Base = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    Percentuale = table.Column<decimal>(type: "decimal(6,3)", precision: 6, scale: 3, nullable: false),
                    Importo = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimentiSpettanze", x => x.IdMovimentoSpettanza);
                    table.ForeignKey(
                        name: "FK_MovimentiSpettanze_AnniSpettanzeChiusi_IdAnnoSpettanzeChiuso",
                        column: x => x.IdAnnoSpettanzeChiuso,
                        principalSchema: "provv",
                        principalTable: "AnniSpettanzeChiusi",
                        principalColumn: "IdAnnoSpettanzeChiuso",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnniSpettanzeChiusi_IdConsulentePercentuale_Anno",
                schema: "provv",
                table: "AnniSpettanzeChiusi",
                columns: new[] { "IdConsulentePercentuale", "Anno" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AzwFattureConsulenti_CodiceFornitore_DataRegistrazione",
                schema: "provv",
                table: "AzwFattureConsulenti",
                columns: new[] { "CodiceFornitore", "DataRegistrazione" });

            migrationBuilder.CreateIndex(
                name: "IX_ContrattiConsulenti_IdConsulentePercentuale",
                schema: "provv",
                table: "ContrattiConsulenti",
                column: "IdConsulentePercentuale");

            migrationBuilder.CreateIndex(
                name: "IX_MovimentiSpettanze_IdAnnoSpettanzeChiuso",
                schema: "provv",
                table: "MovimentiSpettanze",
                column: "IdAnnoSpettanzeChiuso");

            migrationBuilder.CreateIndex(
                name: "IX_VociGruppiSpettanze_Gruppo",
                schema: "provv",
                table: "VociGruppiSpettanze",
                column: "Gruppo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AzwFattureConsulenti",
                schema: "provv");

            migrationBuilder.DropTable(
                name: "ContrattiConsulenti",
                schema: "provv");

            migrationBuilder.DropTable(
                name: "MovimentiSpettanze",
                schema: "provv");

            migrationBuilder.DropTable(
                name: "VociGruppiSpettanze",
                schema: "provv");

            migrationBuilder.DropTable(
                name: "AnniSpettanzeChiusi",
                schema: "provv");

            migrationBuilder.DropTable(
                name: "ConsulentiPercentuale",
                schema: "provv");

            migrationBuilder.DropColumn(
                name: "Commessa",
                schema: "provv",
                table: "AzwRigheNoteCredito");

            migrationBuilder.DropColumn(
                name: "Commessa",
                schema: "provv",
                table: "AzwRigheFattura");
        }
    }
}
