using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class MpeClassePesiera : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClasseOiml",
                schema: "bilance",
                table: "MasseCampione",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Mpe",
                schema: "bilance",
                table: "MasseCampione",
                type: "float",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClasseOiml",
                schema: "bilance",
                table: "MasseCampione");

            migrationBuilder.DropColumn(
                name: "Mpe",
                schema: "bilance",
                table: "MasseCampione");
        }
    }
}
