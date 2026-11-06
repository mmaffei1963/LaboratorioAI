using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class AggiuntaAccreditamentiEsterni : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "labesterni");

            migrationBuilder.CreateTable(
                name: "Accreditamenti",
                schema: "labesterni",
                columns: table => new
                {
                    IdAccreditamento = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdLabEsternoArcLab = table.Column<int>(type: "int", nullable: false),
                    NomeLaboratorio = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NumeroAccreditamento = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Sede = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    UltimaRevisioneConfermata = table.Column<int>(type: "int", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Attivo = table.Column<bool>(type: "bit", nullable: false),
                    DataCancellazione = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    DataUltimaModifica = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    IdUtente = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accreditamenti", x => x.IdAccreditamento);
                });

            migrationBuilder.CreateTable(
                name: "Revisioni",
                schema: "labesterni",
                columns: table => new
                {
                    IdRevisione = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdAccreditamento = table.Column<int>(type: "int", nullable: false),
                    NumeroRevisione = table.Column<int>(type: "int", nullable: false),
                    DataEmissione = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PercorsoFile = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    HashFileSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    DataRilevazione = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    Confermata = table.Column<bool>(type: "bit", nullable: false),
                    DataConferma = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IdUtenteConferma = table.Column<int>(type: "int", nullable: true),
                    NoteConferma = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Revisioni", x => x.IdRevisione);
                    table.ForeignKey(
                        name: "FK_Revisioni_Accreditamenti_IdAccreditamento",
                        column: x => x.IdAccreditamento,
                        principalSchema: "labesterni",
                        principalTable: "Accreditamenti",
                        principalColumn: "IdAccreditamento",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Accreditamenti_Attivo",
                schema: "labesterni",
                table: "Accreditamenti",
                column: "Attivo");

            migrationBuilder.CreateIndex(
                name: "IX_Accreditamenti_IdLabEsternoArcLab",
                schema: "labesterni",
                table: "Accreditamenti",
                column: "IdLabEsternoArcLab",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Accreditamenti_NumeroAccreditamento_Sede",
                schema: "labesterni",
                table: "Accreditamenti",
                columns: new[] { "NumeroAccreditamento", "Sede" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Revisioni_Confermata",
                schema: "labesterni",
                table: "Revisioni",
                column: "Confermata");

            migrationBuilder.CreateIndex(
                name: "IX_Revisioni_IdAccreditamento_NumeroRevisione",
                schema: "labesterni",
                table: "Revisioni",
                columns: new[] { "IdAccreditamento", "NumeroRevisione" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Revisioni",
                schema: "labesterni");

            migrationBuilder.DropTable(
                name: "Accreditamenti",
                schema: "labesterni");
        }
    }
}
