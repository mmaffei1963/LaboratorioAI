using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class PrestazioniENoteRighe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Note",
                schema: "preventivi",
                table: "Righe",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "PrestazioniInsediamentiArcLab",
                schema: "clienti",
                columns: table => new
                {
                    IdPrestazioneInsediamentoArcLab = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdInsediamento = table.Column<int>(type: "int", nullable: false),
                    Anno = table.Column<int>(type: "int", nullable: false),
                    Mese = table.Column<int>(type: "int", nullable: false),
                    IdTipoCommessa = table.Column<int>(type: "int", nullable: false),
                    IdTipoIncarico = table.Column<int>(type: "int", nullable: true),
                    Prestazioni = table.Column<int>(type: "int", nullable: false),
                    Valore = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    PrimaPrestazione = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UltimaPrestazione = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrestazioniInsediamentiArcLab", x => x.IdPrestazioneInsediamentoArcLab);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PrestazioniInsediamentiArcLab_IdInsediamento",
                schema: "clienti",
                table: "PrestazioniInsediamentiArcLab",
                column: "IdInsediamento");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PrestazioniInsediamentiArcLab",
                schema: "clienti");

            migrationBuilder.AlterColumn<string>(
                name: "Note",
                schema: "preventivi",
                table: "Righe",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000,
                oldNullable: true);
        }
    }
}
