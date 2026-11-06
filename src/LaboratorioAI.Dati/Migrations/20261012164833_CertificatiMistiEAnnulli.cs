using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class CertificatiMistiEAnnulli : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IdCampioneArcLab",
                schema: "log",
                table: "RegistroDocumenti",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IdLabEsternoArcLab",
                schema: "log",
                table: "RegistroDocumenti",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IdRegistroCollegato",
                schema: "log",
                table: "RegistroDocumenti",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Tipo",
                schema: "log",
                table: "RegistroDocumenti",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RegistroDocumenti_IdRegistroCollegato",
                schema: "log",
                table: "RegistroDocumenti",
                column: "IdRegistroCollegato");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RegistroDocumenti_IdRegistroCollegato",
                schema: "log",
                table: "RegistroDocumenti");

            migrationBuilder.DropColumn(
                name: "IdCampioneArcLab",
                schema: "log",
                table: "RegistroDocumenti");

            migrationBuilder.DropColumn(
                name: "IdLabEsternoArcLab",
                schema: "log",
                table: "RegistroDocumenti");

            migrationBuilder.DropColumn(
                name: "IdRegistroCollegato",
                schema: "log",
                table: "RegistroDocumenti");

            migrationBuilder.DropColumn(
                name: "Tipo",
                schema: "log",
                table: "RegistroDocumenti");
        }
    }
}
