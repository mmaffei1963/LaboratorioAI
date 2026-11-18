using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class VerificaBrowserPortale : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DispositiviFidati",
                schema: "portale",
                columns: table => new
                {
                    IdDispositivoFidatoPortale = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdClienteArcLab = table.Column<int>(type: "int", nullable: false),
                    ImprontaToken = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DataCreazioneUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    ScadenzaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UltimoUsoUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Revocato = table.Column<bool>(type: "bit", nullable: false),
                    DataRevocaUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IndirizzoIp = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DispositiviFidati", x => x.IdDispositivoFidatoPortale);
                });

            migrationBuilder.CreateTable(
                name: "VerificheAccesso",
                schema: "portale",
                columns: table => new
                {
                    IdVerificaAccessoPortale = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdClienteArcLab = table.Column<int>(type: "int", nullable: false),
                    CodiceContabilita = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RagioneSociale = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ImprontaToken = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Sale = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ImprontaCodice = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DataCreazioneUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    ScadenzaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Tentativi = table.Column<int>(type: "int", nullable: false),
                    Usata = table.Column<bool>(type: "bit", nullable: false),
                    IndirizzoIp = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VerificheAccesso", x => x.IdVerificaAccessoPortale);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DispositiviFidati_IdClienteArcLab_Revocato",
                schema: "portale",
                table: "DispositiviFidati",
                columns: new[] { "IdClienteArcLab", "Revocato" });

            migrationBuilder.CreateIndex(
                name: "IX_DispositiviFidati_ImprontaToken",
                schema: "portale",
                table: "DispositiviFidati",
                column: "ImprontaToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VerificheAccesso_IdClienteArcLab_DataCreazioneUtc",
                schema: "portale",
                table: "VerificheAccesso",
                columns: new[] { "IdClienteArcLab", "DataCreazioneUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_VerificheAccesso_ImprontaToken",
                schema: "portale",
                table: "VerificheAccesso",
                column: "ImprontaToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DispositiviFidati",
                schema: "portale");

            migrationBuilder.DropTable(
                name: "VerificheAccesso",
                schema: "portale");
        }
    }
}
