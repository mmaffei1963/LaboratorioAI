using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class DirezioneCommentiAi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CommentiAi",
                schema: "dir",
                columns: table => new
                {
                    IdCommentoAiDirezione = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Pagina = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Filtri = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DescrizioneFiltri = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DatiInviati = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Testo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Modello = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TokenInput = table.Column<int>(type: "int", nullable: false),
                    TokenOutput = table.Column<int>(type: "int", nullable: false),
                    CostoStimato = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    IdUtente = table.Column<int>(type: "int", nullable: true),
                    NomeUtente = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommentiAi", x => x.IdCommentoAiDirezione);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommentiAi_Pagina_DataCreazione",
                schema: "dir",
                table: "CommentiAi",
                columns: new[] { "Pagina", "DataCreazione" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommentiAi",
                schema: "dir");
        }
    }
}
