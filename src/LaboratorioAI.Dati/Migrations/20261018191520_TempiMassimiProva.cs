using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class TempiMassimiProva : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TempiMassimiProva",
                schema: "limiti",
                columns: table => new
                {
                    IdTempoMassimoProva = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Famiglia = table.Column<int>(type: "int", nullable: false),
                    IdParametroArcLab = table.Column<int>(type: "int", nullable: true),
                    NomeParametro = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Ore = table.Column<int>(type: "int", nullable: false),
                    Attivo = table.Column<bool>(type: "bit", nullable: false),
                    Nota = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModificatoDa = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IdUtenteModifica = table.Column<int>(type: "int", nullable: true),
                    DataModifica = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TempiMassimiProva", x => x.IdTempoMassimoProva);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TempiMassimiProva_Famiglia_IdParametroArcLab",
                schema: "limiti",
                table: "TempiMassimiProva",
                columns: new[] { "Famiglia", "IdParametroArcLab" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TempiMassimiProva",
                schema: "limiti");
        }
    }
}
