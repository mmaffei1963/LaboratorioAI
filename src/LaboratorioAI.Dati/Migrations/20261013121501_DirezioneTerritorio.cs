using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class DirezioneTerritorio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CodiceVenditore",
                schema: "provv",
                table: "AzwClienti",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FatturatoClienti",
                schema: "dir",
                columns: table => new
                {
                    IdFatturatoCliente = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Anno = table.Column<int>(type: "int", nullable: false),
                    Mese = table.Column<int>(type: "int", nullable: false),
                    CodiceCliente = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Centro = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Voce = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Commessa = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Importo = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FatturatoClienti", x => x.IdFatturatoCliente);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FatturatoClienti_Anno_CodiceCliente",
                schema: "dir",
                table: "FatturatoClienti",
                columns: new[] { "Anno", "CodiceCliente" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FatturatoClienti",
                schema: "dir");

            migrationBuilder.DropColumn(
                name: "CodiceVenditore",
                schema: "provv",
                table: "AzwClienti");
        }
    }
}
