using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class AggiuntaVerificheNutrizionali : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VerificheNutrizionali",
                schema: "nutri",
                columns: table => new
                {
                    IdVerifica = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdRicetta = table.Column<int>(type: "int", nullable: false),
                    DataVerifica = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    Firma = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Denominazione = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Esito = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Testo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModelloAi = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TokenInput = table.Column<int>(type: "int", nullable: false),
                    TokenOutput = table.Column<int>(type: "int", nullable: false),
                    CostoStimato = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    VersioneApplicazione = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IdUtente = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VerificheNutrizionali", x => x.IdVerifica);
                    table.ForeignKey(
                        name: "FK_VerificheNutrizionali_Ricette_IdRicetta",
                        column: x => x.IdRicetta,
                        principalSchema: "nutri",
                        principalTable: "Ricette",
                        principalColumn: "IdRicetta",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VerificheNutrizionali_Utenti_IdUtente",
                        column: x => x.IdUtente,
                        principalSchema: "sec",
                        principalTable: "Utenti",
                        principalColumn: "IdUtente",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VerificheNutrizionali_IdRicetta_Firma",
                schema: "nutri",
                table: "VerificheNutrizionali",
                columns: new[] { "IdRicetta", "Firma" });

            migrationBuilder.CreateIndex(
                name: "IX_VerificheNutrizionali_IdUtente",
                schema: "nutri",
                table: "VerificheNutrizionali",
                column: "IdUtente");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VerificheNutrizionali",
                schema: "nutri");
        }
    }
}
