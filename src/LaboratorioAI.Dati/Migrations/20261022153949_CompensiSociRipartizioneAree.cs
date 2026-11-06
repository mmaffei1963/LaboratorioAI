using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class CompensiSociRipartizioneAree : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "QuotaFissaAmministrazione",
                schema: "soci",
                table: "CompensiSociAnno",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RipartizioneAree",
                schema: "soci",
                table: "CompensiSociAnno",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QuotaFissaAmministrazione",
                schema: "soci",
                table: "CompensiSociAnno");

            migrationBuilder.DropColumn(
                name: "RipartizioneAree",
                schema: "soci",
                table: "CompensiSociAnno");
        }
    }
}
