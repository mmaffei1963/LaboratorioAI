using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class PasswordPortaleClienti : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CodiciRecuperoPassword",
                schema: "portale",
                columns: table => new
                {
                    IdCodiceRecuperoPassword = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdClienteArcLab = table.Column<int>(type: "int", nullable: false),
                    Sale = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ImprontaCodice = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DataCreazioneUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    ScadenzaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Tentativi = table.Column<int>(type: "int", nullable: false),
                    Usato = table.Column<bool>(type: "bit", nullable: false),
                    IndirizzoIp = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodiciRecuperoPassword", x => x.IdCodiceRecuperoPassword);
                });

            migrationBuilder.CreateTable(
                name: "EventiPasswordClienti",
                schema: "portale",
                columns: table => new
                {
                    IdEventoPasswordCliente = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdClienteArcLab = table.Column<int>(type: "int", nullable: true),
                    CodiceContabilita = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Evento = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DataUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    IndirizzoIp = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Dettaglio = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventiPasswordClienti", x => x.IdEventoPasswordCliente);
                });

            migrationBuilder.CreateTable(
                name: "PasswordClienti",
                schema: "portale",
                columns: table => new
                {
                    IdPasswordCliente = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdClienteArcLab = table.Column<int>(type: "int", nullable: false),
                    ImprontaPassword = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Origine = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DataImpostazioneUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    Annullata = table.Column<bool>(type: "bit", nullable: false),
                    DataAnnullamentoUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MotivoAnnullamento = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasswordClienti", x => x.IdPasswordCliente);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CodiciRecuperoPassword_IdClienteArcLab_DataCreazioneUtc",
                schema: "portale",
                table: "CodiciRecuperoPassword",
                columns: new[] { "IdClienteArcLab", "DataCreazioneUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CodiciRecuperoPassword_IndirizzoIp_DataCreazioneUtc",
                schema: "portale",
                table: "CodiciRecuperoPassword",
                columns: new[] { "IndirizzoIp", "DataCreazioneUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_EventiPasswordClienti_IdClienteArcLab_DataUtc",
                schema: "portale",
                table: "EventiPasswordClienti",
                columns: new[] { "IdClienteArcLab", "DataUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PasswordClienti_IdClienteArcLab_Annullata",
                schema: "portale",
                table: "PasswordClienti",
                columns: new[] { "IdClienteArcLab", "Annullata" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CodiciRecuperoPassword",
                schema: "portale");

            migrationBuilder.DropTable(
                name: "EventiPasswordClienti",
                schema: "portale");

            migrationBuilder.DropTable(
                name: "PasswordClienti",
                schema: "portale");
        }
    }
}
