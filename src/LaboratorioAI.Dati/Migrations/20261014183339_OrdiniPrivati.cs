using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class OrdiniPrivati : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrdiniPrivati",
                schema: "accettazione",
                columns: table => new
                {
                    IdOrdinePrivato = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdLotto = table.Column<int>(type: "int", nullable: false),
                    CodLotto = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IdClienteArcLab = table.Column<int>(type: "int", nullable: true),
                    Cliente = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    NumeroCampioni = table.Column<int>(type: "int", nullable: false),
                    DataRicezione = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EmailRapporto = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    Consegna = table.Column<int>(type: "int", nullable: false),
                    FirmatoIl = table.Column<DateTime>(type: "date", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DataStampa = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Stampe = table.Column<int>(type: "int", nullable: false),
                    IdUtente = table.Column<int>(type: "int", nullable: true),
                    NomeUtente = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    NomeFile = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PercorsoFile = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Hash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    DataUltimaModifica = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UltimaModificaDa = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Annullato = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrdiniPrivati", x => x.IdOrdinePrivato);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrdiniPrivati_IdClienteArcLab",
                schema: "accettazione",
                table: "OrdiniPrivati",
                column: "IdClienteArcLab");

            migrationBuilder.CreateIndex(
                name: "IX_OrdiniPrivati_IdLotto",
                schema: "accettazione",
                table: "OrdiniPrivati",
                column: "IdLotto",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrdiniPrivati",
                schema: "accettazione");
        }
    }
}
