using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class LettureDocumentiPortale : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LettureDocumenti",
                schema: "portale",
                columns: table => new
                {
                    IdLetturaDocumentoPortale = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdEntry = table.Column<int>(type: "int", nullable: false),
                    IdClienteArcLab = table.Column<int>(type: "int", nullable: false),
                    Letto = table.Column<bool>(type: "bit", nullable: false),
                    DataAggiornamentoUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    Origine = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LettureDocumenti", x => x.IdLetturaDocumentoPortale);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LettureDocumenti_IdClienteArcLab_IdEntry",
                schema: "portale",
                table: "LettureDocumenti",
                columns: new[] { "IdClienteArcLab", "IdEntry" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LettureDocumenti",
                schema: "portale");
        }
    }
}
