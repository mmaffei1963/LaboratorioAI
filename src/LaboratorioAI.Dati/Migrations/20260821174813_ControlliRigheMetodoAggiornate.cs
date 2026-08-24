using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class ControlliRigheMetodoAggiornate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CodMetodo",
                schema: "controlli",
                table: "RigheMetodo");

            migrationBuilder.AddColumn<int>(
                name: "CifreSignificative",
                schema: "controlli",
                table: "RigheMetodo",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IdLabEsternoArcLab",
                schema: "controlli",
                table: "RigheMetodo",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LabEsterno",
                schema: "controlli",
                table: "RigheMetodo",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxDecimali",
                schema: "controlli",
                table: "RigheMetodo",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Metodo",
                schema: "controlli",
                table: "RigheMetodo",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RifBiblio",
                schema: "controlli",
                table: "RigheMetodo",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CifreSignificative",
                schema: "controlli",
                table: "RigheMetodo");

            migrationBuilder.DropColumn(
                name: "IdLabEsternoArcLab",
                schema: "controlli",
                table: "RigheMetodo");

            migrationBuilder.DropColumn(
                name: "LabEsterno",
                schema: "controlli",
                table: "RigheMetodo");

            migrationBuilder.DropColumn(
                name: "MaxDecimali",
                schema: "controlli",
                table: "RigheMetodo");

            migrationBuilder.DropColumn(
                name: "Metodo",
                schema: "controlli",
                table: "RigheMetodo");

            migrationBuilder.DropColumn(
                name: "RifBiblio",
                schema: "controlli",
                table: "RigheMetodo");

            migrationBuilder.AddColumn<string>(
                name: "CodMetodo",
                schema: "controlli",
                table: "RigheMetodo",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }
    }
}
