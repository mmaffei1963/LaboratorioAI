using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class ProvvigioniArcLab : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DataDaVerificare",
                schema: "provv",
                table: "Convenzioni",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Fonte",
                schema: "provv",
                table: "Convenzioni",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "OrigineConfermata",
                schema: "provv",
                table: "Convenzioni",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "IdMansioneArcLab",
                schema: "provv",
                table: "Commerciali",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CommesseArcLab",
                schema: "provv",
                columns: table => new
                {
                    IdCommessaArcLab = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdCommessa = table.Column<int>(type: "int", nullable: false),
                    CodiceCliente = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IdTipoCommessa = table.Column<int>(type: "int", nullable: false),
                    TipoRapporto = table.Column<short>(type: "smallint", nullable: true),
                    DataCommessa = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Descrizione = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IdMansioneRiferimento = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommesseArcLab", x => x.IdCommessaArcLab);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommesseArcLab_CodiceCliente",
                schema: "provv",
                table: "CommesseArcLab",
                column: "CodiceCliente");

            migrationBuilder.CreateIndex(
                name: "IX_CommesseArcLab_IdMansioneRiferimento",
                schema: "provv",
                table: "CommesseArcLab",
                column: "IdMansioneRiferimento");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommesseArcLab",
                schema: "provv");

            migrationBuilder.DropColumn(
                name: "DataDaVerificare",
                schema: "provv",
                table: "Convenzioni");

            migrationBuilder.DropColumn(
                name: "Fonte",
                schema: "provv",
                table: "Convenzioni");

            migrationBuilder.DropColumn(
                name: "OrigineConfermata",
                schema: "provv",
                table: "Convenzioni");

            migrationBuilder.DropColumn(
                name: "IdMansioneArcLab",
                schema: "provv",
                table: "Commerciali");
        }
    }
}
