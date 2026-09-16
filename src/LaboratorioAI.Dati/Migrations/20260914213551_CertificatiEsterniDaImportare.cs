using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class CertificatiEsterniDaImportare : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EsterniDaImportare",
                schema: "certificati",
                columns: table => new
                {
                    IdCertificatoEsterno = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CodiceLocale = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Stato = table.Column<int>(type: "int", nullable: false),
                    IdCampioneArcLab = table.Column<int>(type: "int", nullable: false),
                    NumeroCertificato = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DataCertificato = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataSped = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NomeFile = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Laboratorio = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Attivo = table.Column<bool>(type: "bit", nullable: false),
                    DataCancellazione = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    DataUltimaModifica = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    IdUtente = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EsterniDaImportare", x => x.IdCertificatoEsterno);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EsterniDaImportare_CodiceLocale",
                schema: "certificati",
                table: "EsterniDaImportare",
                column: "CodiceLocale",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EsterniDaImportare_IdCampioneArcLab",
                schema: "certificati",
                table: "EsterniDaImportare",
                column: "IdCampioneArcLab");

            migrationBuilder.CreateIndex(
                name: "IX_EsterniDaImportare_Stato_Attivo",
                schema: "certificati",
                table: "EsterniDaImportare",
                columns: new[] { "Stato", "Attivo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EsterniDaImportare",
                schema: "certificati");
        }
    }
}
