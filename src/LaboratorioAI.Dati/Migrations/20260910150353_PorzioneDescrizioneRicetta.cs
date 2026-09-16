using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class PorzioneDescrizioneRicetta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PorzioneDescrizioneUsa",
                schema: "nutri",
                table: "Ricette",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PorzioneDescrizioneUsa",
                schema: "nutri",
                table: "Ricette");
        }
    }
}
