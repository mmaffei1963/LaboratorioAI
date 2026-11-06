using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class CompensiSociAnalisiEsterneRitardi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CampioniNascostiRitardi",
                schema: "app",
                columns: table => new
                {
                    IdCampioneNascostoRitardi = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdCampioneArcLab = table.Column<int>(type: "int", nullable: false),
                    NumeroCampione = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    NascostoDa = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    DataNascosto = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Attivo = table.Column<bool>(type: "bit", nullable: false),
                    RipristinatoDa = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    DataRipristino = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampioniNascostiRitardi", x => x.IdCampioneNascostoRitardi);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CampioniNascostiRitardi_IdCampioneArcLab_Attivo",
                schema: "app",
                table: "CampioniNascostiRitardi",
                columns: new[] { "IdCampioneArcLab", "Attivo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CampioniNascostiRitardi",
                schema: "app");
        }
    }
}
