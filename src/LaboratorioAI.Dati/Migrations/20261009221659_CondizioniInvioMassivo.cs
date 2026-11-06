using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class CondizioniInvioMassivo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ErroreInvioLink",
                schema: "contratti",
                table: "RichiesteApprovazione",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InvioAccodato",
                schema: "contratti",
                table: "RichiesteApprovazione",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RichiesteApprovazione_InvioAccodato",
                schema: "contratti",
                table: "RichiesteApprovazione",
                column: "InvioAccodato",
                filter: "[InvioAccodato] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RichiesteApprovazione_InvioAccodato",
                schema: "contratti",
                table: "RichiesteApprovazione");

            migrationBuilder.DropColumn(
                name: "ErroreInvioLink",
                schema: "contratti",
                table: "RichiesteApprovazione");

            migrationBuilder.DropColumn(
                name: "InvioAccodato",
                schema: "contratti",
                table: "RichiesteApprovazione");
        }
    }
}
