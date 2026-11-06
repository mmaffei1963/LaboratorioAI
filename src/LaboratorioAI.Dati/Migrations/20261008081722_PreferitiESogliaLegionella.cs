using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class PreferitiESogliaLegionella : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "LegionellaSogliaCalo",
                schema: "clienti",
                table: "ImpostazioniAndamento",
                type: "decimal(6,2)",
                precision: 6,
                scale: 2,
                nullable: false,
                defaultValue: -20m);

            migrationBuilder.CreateTable(
                name: "PreferitiUtenti",
                schema: "app",
                columns: table => new
                {
                    IdPreferitoUtente = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdUtente = table.Column<int>(type: "int", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Ordine = table.Column<int>(type: "int", nullable: false),
                    Attivo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    DataUltimaModifica = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreferitiUtenti", x => x.IdPreferitoUtente);
                    table.ForeignKey(
                        name: "FK_PreferitiUtenti_Utenti_IdUtente",
                        column: x => x.IdUtente,
                        principalSchema: "sec",
                        principalTable: "Utenti",
                        principalColumn: "IdUtente",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PreferitiUtenti_IdUtente_Url",
                schema: "app",
                table: "PreferitiUtenti",
                columns: new[] { "IdUtente", "Url" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PreferitiUtenti",
                schema: "app");

            migrationBuilder.DropColumn(
                name: "LegionellaSogliaCalo",
                schema: "clienti",
                table: "ImpostazioniAndamento");
        }
    }
}
