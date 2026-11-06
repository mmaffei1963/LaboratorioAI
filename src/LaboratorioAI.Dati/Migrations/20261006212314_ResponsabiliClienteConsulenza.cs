using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class ResponsabiliClienteConsulenza : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CommercialeNome",
                schema: "cons",
                table: "PreventiviConsulenza",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResponsabileCliente",
                schema: "cons",
                table: "PreventiviConsulenza",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ZonaCliente",
                schema: "cons",
                table: "PreventiviConsulenza",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Zona",
                schema: "provv",
                table: "AzwClienti",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ResponsabiliCliente",
                schema: "cons",
                columns: table => new
                {
                    IdResponsabileCliente = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Sigla = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Attivo = table.Column<bool>(type: "bit", nullable: false),
                    DataUltimaModifica = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResponsabiliCliente", x => x.IdResponsabileCliente);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ResponsabiliCliente_Sigla",
                schema: "cons",
                table: "ResponsabiliCliente",
                column: "Sigla",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ResponsabiliCliente",
                schema: "cons");

            migrationBuilder.DropColumn(
                name: "CommercialeNome",
                schema: "cons",
                table: "PreventiviConsulenza");

            migrationBuilder.DropColumn(
                name: "ResponsabileCliente",
                schema: "cons",
                table: "PreventiviConsulenza");

            migrationBuilder.DropColumn(
                name: "ZonaCliente",
                schema: "cons",
                table: "PreventiviConsulenza");

            migrationBuilder.DropColumn(
                name: "Zona",
                schema: "provv",
                table: "AzwClienti");
        }
    }
}
