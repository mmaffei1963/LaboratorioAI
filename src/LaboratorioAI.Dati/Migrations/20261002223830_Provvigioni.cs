using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class Provvigioni : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "provv");

            migrationBuilder.CreateTable(
                name: "AzwFatture",
                schema: "provv",
                columns: table => new
                {
                    IdAzwFattura = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Chiave = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    CodiceCliente = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RagioneSociale = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CodiceVenditore = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    DataFattura = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NettoPerimetro = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    NettoTotale = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    Lordo = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    LordoStimato = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AzwFatture", x => x.IdAzwFattura);
                });

            migrationBuilder.CreateTable(
                name: "AzwIncassi",
                schema: "provv",
                columns: table => new
                {
                    IdAzwIncasso = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChiaveFattura = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    DataIncasso = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ImportoLordo = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AzwIncassi", x => x.IdAzwIncasso);
                });

            migrationBuilder.CreateTable(
                name: "AzwNoteCredito",
                schema: "provv",
                columns: table => new
                {
                    IdAzwNotaCredito = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CodiceCliente = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Riferimento = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Data = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NettoPerimetro = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AzwNoteCredito", x => x.IdAzwNotaCredito);
                });

            migrationBuilder.CreateTable(
                name: "Commerciali",
                schema: "provv",
                columns: table => new
                {
                    IdCommerciale = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IdUtente = table.Column<int>(type: "int", nullable: true),
                    Attivo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Commerciali", x => x.IdCommerciale);
                });

            migrationBuilder.CreateTable(
                name: "ImportazioniAzw",
                schema: "provv",
                columns: table => new
                {
                    IdImportazione = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DataOra = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PercorsoFile = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DataModificaFile = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Riuscita = table.Column<bool>(type: "bit", nullable: false),
                    Fatture = table.Column<int>(type: "int", nullable: false),
                    Incassi = table.Column<int>(type: "int", nullable: false),
                    NoteCredito = table.Column<int>(type: "int", nullable: false),
                    Messaggio = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportazioniAzw", x => x.IdImportazione);
                });

            migrationBuilder.CreateTable(
                name: "Trimestri",
                schema: "provv",
                columns: table => new
                {
                    IdTrimestre = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Anno = table.Column<int>(type: "int", nullable: false),
                    Numero = table.Column<int>(type: "int", nullable: false),
                    Stato = table.Column<int>(type: "int", nullable: false),
                    DataChiusura = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IdUtenteChiusura = table.Column<int>(type: "int", nullable: true),
                    RegoleUsate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VersioneApplicazione = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trimestri", x => x.IdTrimestre);
                });

            migrationBuilder.CreateTable(
                name: "CodiciVenditore",
                schema: "provv",
                columns: table => new
                {
                    IdCodiceVenditore = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdCommerciale = table.Column<int>(type: "int", nullable: false),
                    CodiceVenditore = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ValidoDal = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ValidoAl = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodiciVenditore", x => x.IdCodiceVenditore);
                    table.ForeignKey(
                        name: "FK_CodiciVenditore_Commerciali_IdCommerciale",
                        column: x => x.IdCommerciale,
                        principalSchema: "provv",
                        principalTable: "Commerciali",
                        principalColumn: "IdCommerciale",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ElencoSviluppo",
                schema: "provv",
                columns: table => new
                {
                    IdElencoSviluppo = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdCommerciale = table.Column<int>(type: "int", nullable: false),
                    CodiceCliente = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Anno = table.Column<int>(type: "int", nullable: false),
                    ValidoDal = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Obiettivo = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AzionePrevista = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ApprovatoDa = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Attivo = table.Column<bool>(type: "bit", nullable: false),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElencoSviluppo", x => x.IdElencoSviluppo);
                    table.ForeignKey(
                        name: "FK_ElencoSviluppo_Commerciali_IdCommerciale",
                        column: x => x.IdCommerciale,
                        principalSchema: "provv",
                        principalTable: "Commerciali",
                        principalColumn: "IdCommerciale",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Premi",
                schema: "provv",
                columns: table => new
                {
                    IdPremio = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdCommerciale = table.Column<int>(type: "int", nullable: false),
                    Anno = table.Column<int>(type: "int", nullable: false),
                    BaseIncassata = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    Premio = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    DataCalcolo = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Premi", x => x.IdPremio);
                    table.ForeignKey(
                        name: "FK_Premi_Commerciali_IdCommerciale",
                        column: x => x.IdCommerciale,
                        principalSchema: "provv",
                        principalTable: "Commerciali",
                        principalColumn: "IdCommerciale",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Movimenti",
                schema: "provv",
                columns: table => new
                {
                    IdMovimento = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdTrimestre = table.Column<int>(type: "int", nullable: false),
                    IdCommerciale = table.Column<int>(type: "int", nullable: false),
                    CodiceCliente = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Riferimento = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Data = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Voce = table.Column<int>(type: "int", nullable: false),
                    Categoria = table.Column<int>(type: "int", nullable: false),
                    Base = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    Aliquota = table.Column<decimal>(type: "decimal(6,4)", precision: 6, scale: 4, nullable: false),
                    Importo = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Movimenti", x => x.IdMovimento);
                    table.ForeignKey(
                        name: "FK_Movimenti_Commerciali_IdCommerciale",
                        column: x => x.IdCommerciale,
                        principalSchema: "provv",
                        principalTable: "Commerciali",
                        principalColumn: "IdCommerciale",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Movimenti_Trimestri_IdTrimestre",
                        column: x => x.IdTrimestre,
                        principalSchema: "provv",
                        principalTable: "Trimestri",
                        principalColumn: "IdTrimestre",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AzwFatture_Chiave",
                schema: "provv",
                table: "AzwFatture",
                column: "Chiave",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AzwFatture_CodiceCliente_DataFattura",
                schema: "provv",
                table: "AzwFatture",
                columns: new[] { "CodiceCliente", "DataFattura" });

            migrationBuilder.CreateIndex(
                name: "IX_AzwIncassi_ChiaveFattura_DataIncasso",
                schema: "provv",
                table: "AzwIncassi",
                columns: new[] { "ChiaveFattura", "DataIncasso" });

            migrationBuilder.CreateIndex(
                name: "IX_AzwIncassi_DataIncasso",
                schema: "provv",
                table: "AzwIncassi",
                column: "DataIncasso");

            migrationBuilder.CreateIndex(
                name: "IX_AzwNoteCredito_CodiceCliente_Data",
                schema: "provv",
                table: "AzwNoteCredito",
                columns: new[] { "CodiceCliente", "Data" });

            migrationBuilder.CreateIndex(
                name: "IX_CodiciVenditore_CodiceVenditore_ValidoDal",
                schema: "provv",
                table: "CodiciVenditore",
                columns: new[] { "CodiceVenditore", "ValidoDal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CodiciVenditore_IdCommerciale",
                schema: "provv",
                table: "CodiciVenditore",
                column: "IdCommerciale");

            migrationBuilder.CreateIndex(
                name: "IX_Commerciali_Nome",
                schema: "provv",
                table: "Commerciali",
                column: "Nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ElencoSviluppo_IdCommerciale_Anno_CodiceCliente",
                schema: "provv",
                table: "ElencoSviluppo",
                columns: new[] { "IdCommerciale", "Anno", "CodiceCliente" });

            migrationBuilder.CreateIndex(
                name: "IX_ImportazioniAzw_DataOra",
                schema: "provv",
                table: "ImportazioniAzw",
                column: "DataOra");

            migrationBuilder.CreateIndex(
                name: "IX_Movimenti_IdCommerciale",
                schema: "provv",
                table: "Movimenti",
                column: "IdCommerciale");

            migrationBuilder.CreateIndex(
                name: "IX_Movimenti_IdTrimestre_IdCommerciale",
                schema: "provv",
                table: "Movimenti",
                columns: new[] { "IdTrimestre", "IdCommerciale" });

            migrationBuilder.CreateIndex(
                name: "IX_Premi_IdCommerciale_Anno",
                schema: "provv",
                table: "Premi",
                columns: new[] { "IdCommerciale", "Anno" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Trimestri_Anno_Numero",
                schema: "provv",
                table: "Trimestri",
                columns: new[] { "Anno", "Numero" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AzwFatture",
                schema: "provv");

            migrationBuilder.DropTable(
                name: "AzwIncassi",
                schema: "provv");

            migrationBuilder.DropTable(
                name: "AzwNoteCredito",
                schema: "provv");

            migrationBuilder.DropTable(
                name: "CodiciVenditore",
                schema: "provv");

            migrationBuilder.DropTable(
                name: "ElencoSviluppo",
                schema: "provv");

            migrationBuilder.DropTable(
                name: "ImportazioniAzw",
                schema: "provv");

            migrationBuilder.DropTable(
                name: "Movimenti",
                schema: "provv");

            migrationBuilder.DropTable(
                name: "Premi",
                schema: "provv");

            migrationBuilder.DropTable(
                name: "Trimestri",
                schema: "provv");

            migrationBuilder.DropTable(
                name: "Commerciali",
                schema: "provv");
        }
    }
}
