using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class AccordiProvvigioni : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Accordi",
                schema: "provv",
                columns: table => new
                {
                    IdAccordo = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdCommerciale = table.Column<int>(type: "int", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Stato = table.Column<int>(type: "int", nullable: false),
                    ValidoDal = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ValidoAl = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RiferimentoDocumento = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    MesiPrimoAnno = table.Column<int>(type: "int", nullable: false),
                    MesiPausa = table.Column<int>(type: "int", nullable: false),
                    DataClientiEsistenti = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AnniConvenzione = table.Column<int>(type: "int", nullable: false),
                    DataFineRapporto = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FattoreOrario = table.Column<decimal>(type: "decimal(6,4)", precision: 6, scale: 4, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accordi", x => x.IdAccordo);
                    table.ForeignKey(
                        name: "FK_Accordi_Commerciali_IdCommerciale",
                        column: x => x.IdCommerciale,
                        principalSchema: "provv",
                        principalTable: "Commerciali",
                        principalColumn: "IdCommerciale",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AzwRigheFattura",
                schema: "provv",
                columns: table => new
                {
                    IdAzwRigaFattura = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChiaveFattura = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Centro = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Voce = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Netto = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AzwRigheFattura", x => x.IdAzwRigaFattura);
                });

            migrationBuilder.CreateTable(
                name: "AzwRigheNoteCredito",
                schema: "provv",
                columns: table => new
                {
                    IdAzwRigaNotaCredito = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CodiceCliente = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Riferimento = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Data = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Centro = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Voce = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Netto = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AzwRigheNoteCredito", x => x.IdAzwRigaNotaCredito);
                });

            migrationBuilder.CreateTable(
                name: "BasiCalcolo",
                schema: "provv",
                columns: table => new
                {
                    IdBaseCalcolo = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descrizione = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TuttoIlFatturato = table.Column<bool>(type: "bit", nullable: false),
                    Elementi = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Attivo = table.Column<bool>(type: "bit", nullable: false),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BasiCalcolo", x => x.IdBaseCalcolo);
                });

            migrationBuilder.CreateTable(
                name: "SoglieAccordo",
                schema: "provv",
                columns: table => new
                {
                    IdSogliaAccordo = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdAccordo = table.Column<int>(type: "int", nullable: false),
                    Soglia = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    Premio = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SoglieAccordo", x => x.IdSogliaAccordo);
                    table.ForeignKey(
                        name: "FK_SoglieAccordo_Accordi_IdAccordo",
                        column: x => x.IdAccordo,
                        principalSchema: "provv",
                        principalTable: "Accordi",
                        principalColumn: "IdAccordo",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VociAccordo",
                schema: "provv",
                columns: table => new
                {
                    IdVoceAccordo = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdAccordo = table.Column<int>(type: "int", nullable: false),
                    Voce = table.Column<int>(type: "int", nullable: false),
                    Percentuale = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: false),
                    IdBaseCalcolo = table.Column<int>(type: "int", nullable: false),
                    Descrizione = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VociAccordo", x => x.IdVoceAccordo);
                    table.ForeignKey(
                        name: "FK_VociAccordo_Accordi_IdAccordo",
                        column: x => x.IdAccordo,
                        principalSchema: "provv",
                        principalTable: "Accordi",
                        principalColumn: "IdAccordo",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VociAccordo_BasiCalcolo_IdBaseCalcolo",
                        column: x => x.IdBaseCalcolo,
                        principalSchema: "provv",
                        principalTable: "BasiCalcolo",
                        principalColumn: "IdBaseCalcolo",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Accordi_IdCommerciale_ValidoDal",
                schema: "provv",
                table: "Accordi",
                columns: new[] { "IdCommerciale", "ValidoDal" });

            migrationBuilder.CreateIndex(
                name: "IX_AzwRigheFattura_ChiaveFattura",
                schema: "provv",
                table: "AzwRigheFattura",
                column: "ChiaveFattura");

            migrationBuilder.CreateIndex(
                name: "IX_AzwRigheNoteCredito_CodiceCliente",
                schema: "provv",
                table: "AzwRigheNoteCredito",
                column: "CodiceCliente");

            migrationBuilder.CreateIndex(
                name: "IX_BasiCalcolo_Nome",
                schema: "provv",
                table: "BasiCalcolo",
                column: "Nome");

            migrationBuilder.CreateIndex(
                name: "IX_SoglieAccordo_IdAccordo",
                schema: "provv",
                table: "SoglieAccordo",
                column: "IdAccordo");

            migrationBuilder.CreateIndex(
                name: "IX_VociAccordo_IdAccordo_Voce",
                schema: "provv",
                table: "VociAccordo",
                columns: new[] { "IdAccordo", "Voce" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VociAccordo_IdBaseCalcolo",
                schema: "provv",
                table: "VociAccordo",
                column: "IdBaseCalcolo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AzwRigheFattura",
                schema: "provv");

            migrationBuilder.DropTable(
                name: "AzwRigheNoteCredito",
                schema: "provv");

            migrationBuilder.DropTable(
                name: "SoglieAccordo",
                schema: "provv");

            migrationBuilder.DropTable(
                name: "VociAccordo",
                schema: "provv");

            migrationBuilder.DropTable(
                name: "Accordi",
                schema: "provv");

            migrationBuilder.DropTable(
                name: "BasiCalcolo",
                schema: "provv");
        }
    }
}
