using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class GiudiziDaRiportare : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "scambio");

            migrationBuilder.CreateTable(
                name: "GiudiziDaRiportare",
                schema: "scambio",
                columns: table => new
                {
                    IdGiudizioDaRiportare = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdCampione = table.Column<int>(type: "int", nullable: false),
                    NumeroCampione = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IdGiudizio = table.Column<int>(type: "int", nullable: false),
                    CodGiudizio = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    GiudizioTesto = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Automatico = table.Column<bool>(type: "bit", nullable: false),
                    ColoreLimiti = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ConfermaColore = table.Column<bool>(type: "bit", nullable: false),
                    ValidatoDa = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IdUtenteValidazione = table.Column<int>(type: "int", nullable: true),
                    DataValidazione = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Stato = table.Column<int>(type: "int", nullable: false),
                    DataRiportato = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AnnullatoDa = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    DataAnnullamento = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Nota = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GiudiziDaRiportare", x => x.IdGiudizioDaRiportare);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GiudiziDaRiportare_Stato",
                schema: "scambio",
                table: "GiudiziDaRiportare",
                column: "Stato");

            migrationBuilder.CreateIndex(
                name: "UX_GiudiziDaRiportare_IdCampione_Attivo",
                schema: "scambio",
                table: "GiudiziDaRiportare",
                column: "IdCampione",
                unique: true,
                filter: "[Stato] <> 3");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GiudiziDaRiportare",
                schema: "scambio");
        }
    }
}
