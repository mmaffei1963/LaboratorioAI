using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class PreventiviConsulenzaFasiPersonalizzate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Fase",
                schema: "cons",
                table: "RighePreventivoConsulenza",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "DueFasi",
                schema: "cons",
                table: "PreventiviConsulenza",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Fase1Descrizione",
                schema: "cons",
                table: "PreventiviConsulenza",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Fase1Titolo",
                schema: "cons",
                table: "PreventiviConsulenza",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Fase2Condizione",
                schema: "cons",
                table: "PreventiviConsulenza",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Fase2Descrizione",
                schema: "cons",
                table: "PreventiviConsulenza",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Fase2Titolo",
                schema: "cons",
                table: "PreventiviConsulenza",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotaleFase2AnniSuccessivi",
                schema: "cons",
                table: "PreventiviConsulenza",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotaleFase2PrimoAnno",
                schema: "cons",
                table: "PreventiviConsulenza",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "Personalizzata",
                schema: "cons",
                table: "AttivitaConsulenza",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Fase",
                schema: "cons",
                table: "RighePreventivoConsulenza");

            migrationBuilder.DropColumn(
                name: "DueFasi",
                schema: "cons",
                table: "PreventiviConsulenza");

            migrationBuilder.DropColumn(
                name: "Fase1Descrizione",
                schema: "cons",
                table: "PreventiviConsulenza");

            migrationBuilder.DropColumn(
                name: "Fase1Titolo",
                schema: "cons",
                table: "PreventiviConsulenza");

            migrationBuilder.DropColumn(
                name: "Fase2Condizione",
                schema: "cons",
                table: "PreventiviConsulenza");

            migrationBuilder.DropColumn(
                name: "Fase2Descrizione",
                schema: "cons",
                table: "PreventiviConsulenza");

            migrationBuilder.DropColumn(
                name: "Fase2Titolo",
                schema: "cons",
                table: "PreventiviConsulenza");

            migrationBuilder.DropColumn(
                name: "TotaleFase2AnniSuccessivi",
                schema: "cons",
                table: "PreventiviConsulenza");

            migrationBuilder.DropColumn(
                name: "TotaleFase2PrimoAnno",
                schema: "cons",
                table: "PreventiviConsulenza");

            migrationBuilder.DropColumn(
                name: "Personalizzata",
                schema: "cons",
                table: "AttivitaConsulenza");
        }
    }
}
