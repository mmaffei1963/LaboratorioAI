using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class ArchivioEstrazioniCertificato : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "certificati");

            migrationBuilder.CreateTable(
                name: "EstrazioniEsterni",
                schema: "certificati",
                columns: table => new
                {
                    IdEstrazione = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Hash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    NomeFileOriginale = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    EstrazioneJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModelloAi = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    IdUtente = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EstrazioniEsterni", x => x.IdEstrazione);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EstrazioniEsterni_Hash",
                schema: "certificati",
                table: "EstrazioniEsterni",
                column: "Hash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EstrazioniEsterni",
                schema: "certificati");
        }
    }
}
