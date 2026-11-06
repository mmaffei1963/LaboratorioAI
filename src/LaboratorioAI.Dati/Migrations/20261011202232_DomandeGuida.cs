using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class DomandeGuida : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DomandeGuida",
                schema: "log",
                columns: table => new
                {
                    IdDomandaGuida = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DataOra = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    IdUtente = table.Column<int>(type: "int", nullable: true),
                    EmailUtente = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NomeUtente = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Percorso = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CodiceGuida = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Domanda = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Risposta = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Valutazione = table.Column<short>(type: "smallint", nullable: true),
                    TokenInput = table.Column<int>(type: "int", nullable: false),
                    TokenOutput = table.Column<int>(type: "int", nullable: false),
                    TokenCacheScritti = table.Column<int>(type: "int", nullable: false),
                    TokenCacheLetti = table.Column<int>(type: "int", nullable: false),
                    CostoStimato = table.Column<decimal>(type: "decimal(12,6)", precision: 12, scale: 6, nullable: false),
                    Modello = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    DurataMs = table.Column<int>(type: "int", nullable: false),
                    Errore = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DataAnonimizzazione = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DomandeGuida", x => x.IdDomandaGuida);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DomandeGuida_DataOra",
                schema: "log",
                table: "DomandeGuida",
                column: "DataOra",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_DomandeGuida_IdUtente_DataOra",
                schema: "log",
                table: "DomandeGuida",
                columns: new[] { "IdUtente", "DataOra" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DomandeGuida",
                schema: "log");
        }
    }
}
