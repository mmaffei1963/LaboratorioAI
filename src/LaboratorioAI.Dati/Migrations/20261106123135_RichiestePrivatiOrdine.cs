using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class RichiestePrivatiOrdine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ComunicazioneFuoriLimite",
                schema: "preventivi",
                table: "RichiesteAnalisiPrivati",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ConsensoComunicazioni",
                schema: "preventivi",
                table: "RichiesteAnalisiPrivati",
                type: "bit",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ComunicazioneFuoriLimite",
                schema: "preventivi",
                table: "RichiesteAnalisiPrivati");

            migrationBuilder.DropColumn(
                name: "ConsensoComunicazioni",
                schema: "preventivi",
                table: "RichiesteAnalisiPrivati");
        }
    }
}
