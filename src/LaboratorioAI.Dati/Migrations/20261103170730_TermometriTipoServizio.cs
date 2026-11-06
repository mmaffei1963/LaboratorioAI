using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class TermometriTipoServizio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Servizio",
                schema: "tarature",
                table: "TermometriEsterni",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "TipoTermometro",
                schema: "tarature",
                table: "TermometriEsterni",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Servizio",
                schema: "taratura",
                table: "Tarature",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "TipoTermometro",
                schema: "taratura",
                table: "Tarature",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Servizio",
                schema: "tarature",
                table: "TermometriEsterni");

            migrationBuilder.DropColumn(
                name: "TipoTermometro",
                schema: "tarature",
                table: "TermometriEsterni");

            migrationBuilder.DropColumn(
                name: "Servizio",
                schema: "taratura",
                table: "Tarature");

            migrationBuilder.DropColumn(
                name: "TipoTermometro",
                schema: "taratura",
                table: "Tarature");
        }
    }
}
