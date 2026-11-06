using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class DenominazioniMetodoVerificate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DenominazioniMetodoVerificate",
                schema: "app",
                columns: table => new
                {
                    IdDenominazioneVerificata = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdMetodoArcLab = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    DenominazioneVerificata = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ParametroVerificato = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IdUtente = table.Column<int>(type: "int", nullable: true),
                    NomeUtente = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Attivo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    DataCancellazione = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DenominazioniMetodoVerificate", x => x.IdDenominazioneVerificata);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DenominazioniMetodoVerificate_IdMetodoArcLab_Tipo_Attivo",
                schema: "app",
                table: "DenominazioniMetodoVerificate",
                columns: new[] { "IdMetodoArcLab", "Tipo", "Attivo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DenominazioniMetodoVerificate",
                schema: "app");
        }
    }
}
