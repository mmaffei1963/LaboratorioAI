using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class TempLogRifiniture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Sincronizzazioni",
                schema: "templog",
                columns: table => new
                {
                    IdSincronizzazione = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DataSincronizzazione = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    NumeroSonde = table.Column<int>(type: "int", nullable: false),
                    IdUtente = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sincronizzazioni", x => x.IdSincronizzazione);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sincronizzazioni_DataSincronizzazione",
                schema: "templog",
                table: "Sincronizzazioni",
                column: "DataSincronizzazione");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Sincronizzazioni",
                schema: "templog");
        }
    }
}
