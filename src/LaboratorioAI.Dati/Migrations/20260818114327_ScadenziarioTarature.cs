using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class ScadenziarioTarature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "tarature");

            migrationBuilder.CreateTable(
                name: "TermometriEsterni",
                schema: "tarature",
                columns: table => new
                {
                    IdTermometroEsterno = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NumeroRapporto = table.Column<int>(type: "int", nullable: true),
                    Tipologia = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DataTaratura = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DataScadenza = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Cliente = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Riferimento = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DataConsegna = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OperatoreConsegna = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    RifFattura = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Attivo = table.Column<bool>(type: "bit", nullable: false),
                    DataCancellazione = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    DataUltimaModifica = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    IdUtente = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TermometriEsterni", x => x.IdTermometroEsterno);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TermometriEsterni_DataScadenza",
                schema: "tarature",
                table: "TermometriEsterni",
                column: "DataScadenza");

            migrationBuilder.CreateIndex(
                name: "IX_TermometriEsterni_NumeroRapporto",
                schema: "tarature",
                table: "TermometriEsterni",
                column: "NumeroRapporto");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TermometriEsterni",
                schema: "tarature");
        }
    }
}
