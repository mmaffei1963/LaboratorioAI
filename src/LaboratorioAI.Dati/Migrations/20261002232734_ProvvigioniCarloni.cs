using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class ProvvigioniCarloni : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "NettoTotale",
                schema: "provv",
                table: "AzwNoteCredito",
                type: "decimal(14,2)",
                precision: 14,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "Convenzioni",
                schema: "provv",
                columns: table => new
                {
                    IdConvenzione = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdCommerciale = table.Column<int>(type: "int", nullable: false),
                    CodiceCliente = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DataInizio = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Origine = table.Column<int>(type: "int", nullable: false),
                    Descrizione = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Attivo = table.Column<bool>(type: "bit", nullable: false),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Convenzioni", x => x.IdConvenzione);
                    table.ForeignKey(
                        name: "FK_Convenzioni_Commerciali_IdCommerciale",
                        column: x => x.IdCommerciale,
                        principalSchema: "provv",
                        principalTable: "Commerciali",
                        principalColumn: "IdCommerciale",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Convenzioni_IdCommerciale_CodiceCliente",
                schema: "provv",
                table: "Convenzioni",
                columns: new[] { "IdCommerciale", "CodiceCliente" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Convenzioni",
                schema: "provv");

            migrationBuilder.DropColumn(
                name: "NettoTotale",
                schema: "provv",
                table: "AzwNoteCredito");
        }
    }
}
