using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class DettagliRapportoBilancia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "CampoTaratura",
                schema: "bilance",
                table: "TaratureBilance",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "MaxVariazioneTemperatura",
                schema: "bilance",
                table: "TaratureBilance",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "Sda",
                schema: "bilance",
                table: "TaratureBilance",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "TempoIntervalloLettura",
                schema: "bilance",
                table: "TaratureBilance",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "TempoStabilizzazione",
                schema: "bilance",
                table: "TaratureBilance",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "Tipo",
                schema: "bilance",
                table: "TaratureBilance",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CampoTaratura",
                schema: "bilance",
                table: "TaratureBilance");

            migrationBuilder.DropColumn(
                name: "MaxVariazioneTemperatura",
                schema: "bilance",
                table: "TaratureBilance");

            migrationBuilder.DropColumn(
                name: "Sda",
                schema: "bilance",
                table: "TaratureBilance");

            migrationBuilder.DropColumn(
                name: "TempoIntervalloLettura",
                schema: "bilance",
                table: "TaratureBilance");

            migrationBuilder.DropColumn(
                name: "TempoStabilizzazione",
                schema: "bilance",
                table: "TaratureBilance");

            migrationBuilder.DropColumn(
                name: "Tipo",
                schema: "bilance",
                table: "TaratureBilance");
        }
    }
}
