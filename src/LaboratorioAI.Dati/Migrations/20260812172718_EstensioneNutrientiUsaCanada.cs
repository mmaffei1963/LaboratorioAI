using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class EstensioneNutrientiUsaCanada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Calcio",
                schema: "nutri",
                table: "ValoriNutrizionali",
                type: "decimal(10,4)",
                precision: 10,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Colesterolo",
                schema: "nutri",
                table: "ValoriNutrizionali",
                type: "decimal(10,4)",
                precision: 10,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Ferro",
                schema: "nutri",
                table: "ValoriNutrizionali",
                type: "decimal(10,4)",
                precision: 10,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GrassiMonoinsaturi",
                schema: "nutri",
                table: "ValoriNutrizionali",
                type: "decimal(10,4)",
                precision: 10,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GrassiPolinsaturi",
                schema: "nutri",
                table: "ValoriNutrizionali",
                type: "decimal(10,4)",
                precision: 10,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GrassiTrans",
                schema: "nutri",
                table: "ValoriNutrizionali",
                type: "decimal(10,4)",
                precision: 10,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Potassio",
                schema: "nutri",
                table: "ValoriNutrizionali",
                type: "decimal(10,4)",
                precision: 10,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Sodio",
                schema: "nutri",
                table: "ValoriNutrizionali",
                type: "decimal(10,4)",
                precision: 10,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VitaminaD",
                schema: "nutri",
                table: "ValoriNutrizionali",
                type: "decimal(10,4)",
                precision: 10,
                scale: 4,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Calcio",
                schema: "nutri",
                table: "ValoriNutrizionali");

            migrationBuilder.DropColumn(
                name: "Colesterolo",
                schema: "nutri",
                table: "ValoriNutrizionali");

            migrationBuilder.DropColumn(
                name: "Ferro",
                schema: "nutri",
                table: "ValoriNutrizionali");

            migrationBuilder.DropColumn(
                name: "GrassiMonoinsaturi",
                schema: "nutri",
                table: "ValoriNutrizionali");

            migrationBuilder.DropColumn(
                name: "GrassiPolinsaturi",
                schema: "nutri",
                table: "ValoriNutrizionali");

            migrationBuilder.DropColumn(
                name: "GrassiTrans",
                schema: "nutri",
                table: "ValoriNutrizionali");

            migrationBuilder.DropColumn(
                name: "Potassio",
                schema: "nutri",
                table: "ValoriNutrizionali");

            migrationBuilder.DropColumn(
                name: "Sodio",
                schema: "nutri",
                table: "ValoriNutrizionali");

            migrationBuilder.DropColumn(
                name: "VitaminaD",
                schema: "nutri",
                table: "ValoriNutrizionali");
        }
    }
}
