using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Tracciabilita.Dati.Migrations
{
    /// <inheritdoc />
    public partial class InvitiUtenti : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImprontaInvito",
                schema: "trac",
                table: "Utenti",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ScadenzaInvitoUtc",
                schema: "trac",
                table: "Utenti",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImprontaInvito",
                schema: "trac",
                table: "Utenti");

            migrationBuilder.DropColumn(
                name: "ScadenzaInvitoUtc",
                schema: "trac",
                table: "Utenti");
        }
    }
}
