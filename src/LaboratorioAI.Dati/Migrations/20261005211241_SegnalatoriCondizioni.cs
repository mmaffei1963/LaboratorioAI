using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class SegnalatoriCondizioni : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SegnalazioniClienti",
                schema: "provv");

            migrationBuilder.AlterColumn<int>(
                name: "IdMansioneRiferimento",
                schema: "provv",
                table: "CommesseArcLab",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateTable(
                name: "CondizioniSegnalatori",
                schema: "provv",
                columns: table => new
                {
                    IdCondizioneSegnalatore = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdSegnalatore = table.Column<int>(type: "int", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Percentuale = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    Ambito = table.Column<int>(type: "int", nullable: false),
                    IdBaseCalcolo = table.Column<int>(type: "int", nullable: true),
                    MesiInterruzione = table.Column<int>(type: "int", nullable: false),
                    AnniDurataMassima = table.Column<int>(type: "int", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Attivo = table.Column<bool>(type: "bit", nullable: false),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CondizioniSegnalatori", x => x.IdCondizioneSegnalatore);
                    table.ForeignKey(
                        name: "FK_CondizioniSegnalatori_BasiCalcolo_IdBaseCalcolo",
                        column: x => x.IdBaseCalcolo,
                        principalSchema: "provv",
                        principalTable: "BasiCalcolo",
                        principalColumn: "IdBaseCalcolo",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CondizioniSegnalatori_Segnalatori_IdSegnalatore",
                        column: x => x.IdSegnalatore,
                        principalSchema: "provv",
                        principalTable: "Segnalatori",
                        principalColumn: "IdSegnalatore",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EccezioniArea",
                schema: "provv",
                columns: table => new
                {
                    IdEccezioneArea = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdCommerciale = table.Column<int>(type: "int", nullable: false),
                    CodiceCliente = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IdBaseCalcolo = table.Column<int>(type: "int", nullable: false),
                    DataInizio = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataFine = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Motivo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreataDa = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Attivo = table.Column<bool>(type: "bit", nullable: false),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EccezioniArea", x => x.IdEccezioneArea);
                    table.ForeignKey(
                        name: "FK_EccezioniArea_BasiCalcolo_IdBaseCalcolo",
                        column: x => x.IdBaseCalcolo,
                        principalSchema: "provv",
                        principalTable: "BasiCalcolo",
                        principalColumn: "IdBaseCalcolo",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EccezioniArea_Commerciali_IdCommerciale",
                        column: x => x.IdCommerciale,
                        principalSchema: "provv",
                        principalTable: "Commerciali",
                        principalColumn: "IdCommerciale",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MovimentiSegnalatori",
                schema: "provv",
                columns: table => new
                {
                    IdMovimentoSegnalatore = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdTrimestre = table.Column<int>(type: "int", nullable: false),
                    IdSegnalazione = table.Column<int>(type: "int", nullable: false),
                    IdSegnalatore = table.Column<int>(type: "int", nullable: false),
                    CodiceCliente = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Riferimento = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Data = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Base = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    Aliquota = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: false),
                    Importo = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimentiSegnalatori", x => x.IdMovimentoSegnalatore);
                    table.ForeignKey(
                        name: "FK_MovimentiSegnalatori_Trimestri_IdTrimestre",
                        column: x => x.IdTrimestre,
                        principalSchema: "provv",
                        principalTable: "Trimestri",
                        principalColumn: "IdTrimestre",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Segnalazioni",
                schema: "provv",
                columns: table => new
                {
                    IdSegnalazione = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CodiceCliente = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IdCondizioneSegnalatore = table.Column<int>(type: "int", nullable: false),
                    DataInizio = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataFine = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IdCommessa = table.Column<int>(type: "int", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Attivo = table.Column<bool>(type: "bit", nullable: false),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Segnalazioni", x => x.IdSegnalazione);
                    table.ForeignKey(
                        name: "FK_Segnalazioni_CondizioniSegnalatori_IdCondizioneSegnalatore",
                        column: x => x.IdCondizioneSegnalatore,
                        principalSchema: "provv",
                        principalTable: "CondizioniSegnalatori",
                        principalColumn: "IdCondizioneSegnalatore",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CondizioniSegnalatori_IdBaseCalcolo",
                schema: "provv",
                table: "CondizioniSegnalatori",
                column: "IdBaseCalcolo");

            migrationBuilder.CreateIndex(
                name: "IX_CondizioniSegnalatori_IdSegnalatore",
                schema: "provv",
                table: "CondizioniSegnalatori",
                column: "IdSegnalatore");

            migrationBuilder.CreateIndex(
                name: "IX_EccezioniArea_IdBaseCalcolo",
                schema: "provv",
                table: "EccezioniArea",
                column: "IdBaseCalcolo");

            migrationBuilder.CreateIndex(
                name: "IX_EccezioniArea_IdCommerciale_CodiceCliente",
                schema: "provv",
                table: "EccezioniArea",
                columns: new[] { "IdCommerciale", "CodiceCliente" });

            migrationBuilder.CreateIndex(
                name: "IX_MovimentiSegnalatori_IdTrimestre_IdSegnalatore",
                schema: "provv",
                table: "MovimentiSegnalatori",
                columns: new[] { "IdTrimestre", "IdSegnalatore" });

            migrationBuilder.CreateIndex(
                name: "IX_Segnalazioni_CodiceCliente",
                schema: "provv",
                table: "Segnalazioni",
                column: "CodiceCliente");

            migrationBuilder.CreateIndex(
                name: "IX_Segnalazioni_IdCondizioneSegnalatore",
                schema: "provv",
                table: "Segnalazioni",
                column: "IdCondizioneSegnalatore");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EccezioniArea",
                schema: "provv");

            migrationBuilder.DropTable(
                name: "MovimentiSegnalatori",
                schema: "provv");

            migrationBuilder.DropTable(
                name: "Segnalazioni",
                schema: "provv");

            migrationBuilder.DropTable(
                name: "CondizioniSegnalatori",
                schema: "provv");

            migrationBuilder.AlterColumn<int>(
                name: "IdMansioneRiferimento",
                schema: "provv",
                table: "CommesseArcLab",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "SegnalazioniClienti",
                schema: "provv",
                columns: table => new
                {
                    IdSegnalazioneCliente = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Attivo = table.Column<bool>(type: "bit", nullable: false),
                    CodiceCliente = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    IdCommerciale = table.Column<int>(type: "int", nullable: false),
                    IdSegnalatore = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SegnalazioniClienti", x => x.IdSegnalazioneCliente);
                    table.ForeignKey(
                        name: "FK_SegnalazioniClienti_Commerciali_IdCommerciale",
                        column: x => x.IdCommerciale,
                        principalSchema: "provv",
                        principalTable: "Commerciali",
                        principalColumn: "IdCommerciale",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SegnalazioniClienti_Segnalatori_IdSegnalatore",
                        column: x => x.IdSegnalatore,
                        principalSchema: "provv",
                        principalTable: "Segnalatori",
                        principalColumn: "IdSegnalatore",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SegnalazioniClienti_IdCommerciale_CodiceCliente",
                schema: "provv",
                table: "SegnalazioniClienti",
                columns: new[] { "IdCommerciale", "CodiceCliente" });

            migrationBuilder.CreateIndex(
                name: "IX_SegnalazioniClienti_IdSegnalatore",
                schema: "provv",
                table: "SegnalazioniClienti",
                column: "IdSegnalatore");
        }
    }
}
