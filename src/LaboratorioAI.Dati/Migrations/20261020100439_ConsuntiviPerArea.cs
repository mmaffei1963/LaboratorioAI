using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class ConsuntiviPerArea : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RegistrazioniAnalitiche",
                schema: "dir",
                columns: table => new
                {
                    IdRegistrazioneAnalitica = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DataRegistrazione = table.Column<DateTime>(type: "date", nullable: false),
                    AnnoRegistrazione = table.Column<int>(type: "int", nullable: false),
                    NumeroRegistrazione = table.Column<int>(type: "int", nullable: false),
                    TipoDocumento = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    NumeroDocumento = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Centro = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Voce = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Commessa = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Conto = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Descrizione = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Importo = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrazioniAnalitiche", x => x.IdRegistrazioneAnalitica);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RegistrazioniAnalitiche_DataRegistrazione",
                schema: "dir",
                table: "RegistrazioniAnalitiche",
                column: "DataRegistrazione");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrazioniAnalitiche_Voce_Centro",
                schema: "dir",
                table: "RegistrazioniAnalitiche",
                columns: new[] { "Voce", "Centro" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RegistrazioniAnalitiche",
                schema: "dir");
        }
    }
}
