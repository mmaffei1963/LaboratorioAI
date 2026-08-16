using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class TempLogValidazione : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DataValidazione",
                schema: "templog",
                table: "Caricamenti",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EsitoValidazione",
                schema: "templog",
                table: "Caricamenti",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NoteValidazione",
                schema: "templog",
                table: "Caricamenti",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Validatore",
                schema: "templog",
                table: "Caricamenti",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DataValidazione",
                schema: "templog",
                table: "Caricamenti");

            migrationBuilder.DropColumn(
                name: "EsitoValidazione",
                schema: "templog",
                table: "Caricamenti");

            migrationBuilder.DropColumn(
                name: "NoteValidazione",
                schema: "templog",
                table: "Caricamenti");

            migrationBuilder.DropColumn(
                name: "Validatore",
                schema: "templog",
                table: "Caricamenti");
        }
    }
}
