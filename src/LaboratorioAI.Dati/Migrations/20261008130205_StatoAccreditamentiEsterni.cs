using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class StatoAccreditamentiEsterni : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DataStatoAccredia",
                schema: "labesterni",
                table: "Accreditamenti",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StatoAccredia",
                schema: "labesterni",
                table: "Accreditamenti",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DataStatoAccredia",
                schema: "labesterni",
                table: "Accreditamenti");

            migrationBuilder.DropColumn(
                name: "StatoAccredia",
                schema: "labesterni",
                table: "Accreditamenti");
        }
    }
}
