using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class ProveSospeseLaboratoriEsterni : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProveSospese",
                schema: "labesterni",
                columns: table => new
                {
                    IdProvaSospesa = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdAccreditamento = table.Column<int>(type: "int", nullable: false),
                    ChiaveProva = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Matrice = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Prova = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Norma = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Tecnica = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    DataRilevazione = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    DataUltimaLettura = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    DataFineSospensione = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProveSospese", x => x.IdProvaSospesa);
                    table.ForeignKey(
                        name: "FK_ProveSospese_Accreditamenti_IdAccreditamento",
                        column: x => x.IdAccreditamento,
                        principalSchema: "labesterni",
                        principalTable: "Accreditamenti",
                        principalColumn: "IdAccreditamento",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProveSospese_DataFineSospensione",
                schema: "labesterni",
                table: "ProveSospese",
                column: "DataFineSospensione");

            migrationBuilder.CreateIndex(
                name: "IX_ProveSospese_IdAccreditamento_ChiaveProva",
                schema: "labesterni",
                table: "ProveSospese",
                columns: new[] { "IdAccreditamento", "ChiaveProva" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProveSospese",
                schema: "labesterni");
        }
    }
}
