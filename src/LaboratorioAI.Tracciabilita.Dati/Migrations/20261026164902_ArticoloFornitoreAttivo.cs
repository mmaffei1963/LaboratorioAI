using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Tracciabilita.Dati.Migrations
{
    /// <inheritdoc />
    public partial class ArticoloFornitoreAttivo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Attivo",
                schema: "trac",
                table: "ArticoliFornitori",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Attivo",
                schema: "trac",
                table: "ArticoliFornitori");
        }
    }
}
