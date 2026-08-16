using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class MonitoraggioTempLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "templog");

            migrationBuilder.CreateTable(
                name: "Caricamenti",
                schema: "templog",
                columns: table => new
                {
                    IdCaricamento = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NomeFile = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    DataDal = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DataAl = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NumeroCanaliMonitorati = table.Column<int>(type: "int", nullable: false),
                    NumeroCanaliConEscursioni = table.Column<int>(type: "int", nullable: false),
                    NumeroEscursioni = table.Column<int>(type: "int", nullable: false),
                    TuttoConforme = table.Column<bool>(type: "bit", nullable: false),
                    EscursioniJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DaSistemare = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    IdUtente = table.Column<int>(type: "int", nullable: true),
                    VersioneApplicazione = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Caricamenti", x => x.IdCaricamento);
                });

            migrationBuilder.CreateTable(
                name: "MappatureCanali",
                schema: "templog",
                columns: table => new
                {
                    IdMappatura = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Canale = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IdStrumento = table.Column<int>(type: "int", nullable: false),
                    Sonda = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Attivo = table.Column<bool>(type: "bit", nullable: false),
                    ValidaDal = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ValidaAl = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DataCancellazione = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    DataUltimaModifica = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    IdUtente = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MappatureCanali", x => x.IdMappatura);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Caricamenti_DataCreazione",
                schema: "templog",
                table: "Caricamenti",
                column: "DataCreazione");

            migrationBuilder.CreateIndex(
                name: "IX_MappatureCanali_Canale_Attivo",
                schema: "templog",
                table: "MappatureCanali",
                columns: new[] { "Canale", "Attivo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Caricamenti",
                schema: "templog");

            migrationBuilder.DropTable(
                name: "MappatureCanali",
                schema: "templog");
        }
    }
}
