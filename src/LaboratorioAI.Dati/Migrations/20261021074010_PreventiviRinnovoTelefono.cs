using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class PreventiviRinnovoTelefono : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClienteTelefono",
                schema: "preventivi",
                table: "Preventivi",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SenzaRinnovoTacito",
                schema: "preventivi",
                table: "Preventivi",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClienteTelefono",
                schema: "preventivi",
                table: "Preventivi");

            migrationBuilder.DropColumn(
                name: "SenzaRinnovoTacito",
                schema: "preventivi",
                table: "Preventivi");
        }
    }
}
