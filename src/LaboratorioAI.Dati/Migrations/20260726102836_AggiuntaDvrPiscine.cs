using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class AggiuntaDvrPiscine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DvrPiscine",
                schema: "app",
                columns: table => new
                {
                    IdDvrPiscina = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdInsediamentoArcLab = table.Column<int>(type: "int", nullable: false),
                    IdClienteArcLab = table.Column<int>(type: "int", nullable: true),
                    RagioneSociale = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    NomeInsediamento = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Comune = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Provincia = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Regione = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Stato = table.Column<int>(type: "int", nullable: false),
                    DatiScheda = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    DataUltimaModifica = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    IdUtente = table.Column<int>(type: "int", nullable: true),
                    VersioneApplicazione = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DataUltimaGenerazione = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NomeFileGenerato = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PercorsoArchivio = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DvrPiscine", x => x.IdDvrPiscina);
                    table.ForeignKey(
                        name: "FK_DvrPiscine_Utenti_IdUtente",
                        column: x => x.IdUtente,
                        principalSchema: "sec",
                        principalTable: "Utenti",
                        principalColumn: "IdUtente",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DvrPiscine_DataUltimaModifica",
                schema: "app",
                table: "DvrPiscine",
                column: "DataUltimaModifica",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_DvrPiscine_IdInsediamentoArcLab",
                schema: "app",
                table: "DvrPiscine",
                column: "IdInsediamentoArcLab");

            migrationBuilder.CreateIndex(
                name: "IX_DvrPiscine_IdUtente",
                schema: "app",
                table: "DvrPiscine",
                column: "IdUtente");

            migrationBuilder.CreateIndex(
                name: "IX_DvrPiscine_Stato",
                schema: "app",
                table: "DvrPiscine",
                column: "Stato");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DvrPiscine",
                schema: "app");
        }
    }
}
