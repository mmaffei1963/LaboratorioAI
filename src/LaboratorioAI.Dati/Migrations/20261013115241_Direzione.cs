using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class Direzione : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dir");

            migrationBuilder.CreateTable(
                name: "CentriCosto",
                schema: "dir",
                columns: table => new
                {
                    IdCentroCosto = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codice = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Descrizione = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CentriCosto", x => x.IdCentroCosto);
                });

            migrationBuilder.CreateTable(
                name: "ClassificazioniVoci",
                schema: "dir",
                columns: table => new
                {
                    IdClassificazioneVoce = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CodiceVoce = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Descrizione = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Ricavo = table.Column<bool>(type: "bit", nullable: false),
                    TipoCosto = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Macrocategoria = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    MateriaPrima = table.Column<bool>(type: "bit", nullable: false),
                    DaVerificare = table.Column<bool>(type: "bit", nullable: false),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataUltimaModifica = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IdUtente = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassificazioniVoci", x => x.IdClassificazioneVoce);
                });

            migrationBuilder.CreateTable(
                name: "CommesseAzw",
                schema: "dir",
                columns: table => new
                {
                    IdCommessaAzw = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codice = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Descrizione = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommesseAzw", x => x.IdCommessaAzw);
                });

            migrationBuilder.CreateTable(
                name: "MovimentiAnalitici",
                schema: "dir",
                columns: table => new
                {
                    IdMovimentoAnalitico = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Anno = table.Column<int>(type: "int", nullable: false),
                    Mese = table.Column<int>(type: "int", nullable: false),
                    Centro = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Voce = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Commessa = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Conto = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Importo = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Righe = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimentiAnalitici", x => x.IdMovimentoAnalitico);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClassificazioniVoci_CodiceVoce",
                schema: "dir",
                table: "ClassificazioniVoci",
                column: "CodiceVoce",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MovimentiAnalitici_Anno_Centro",
                schema: "dir",
                table: "MovimentiAnalitici",
                columns: new[] { "Anno", "Centro" });

            migrationBuilder.CreateIndex(
                name: "IX_MovimentiAnalitici_Commessa",
                schema: "dir",
                table: "MovimentiAnalitici",
                column: "Commessa");

            migrationBuilder.CreateIndex(
                name: "IX_MovimentiAnalitici_Voce",
                schema: "dir",
                table: "MovimentiAnalitici",
                column: "Voce");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CentriCosto",
                schema: "dir");

            migrationBuilder.DropTable(
                name: "ClassificazioniVoci",
                schema: "dir");

            migrationBuilder.DropTable(
                name: "CommesseAzw",
                schema: "dir");

            migrationBuilder.DropTable(
                name: "MovimentiAnalitici",
                schema: "dir");
        }
    }
}
