using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class PreventiviRigaInsediamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IdInsediamentoArcLab",
                schema: "preventivi",
                table: "Righe",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Insediamento",
                schema: "preventivi",
                table: "Righe",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IdInsediamentoArcLab",
                schema: "preventivi",
                table: "Righe");

            migrationBuilder.DropColumn(
                name: "Insediamento",
                schema: "preventivi",
                table: "Righe");
        }
    }
}
